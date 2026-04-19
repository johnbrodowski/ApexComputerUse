using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;

namespace ApexComputerUse
{
    /// <summary>
    /// Lightweight HTTP server exposing the Apex command set via JSON REST endpoints.
    /// Runs on a background thread; thread-safe via CommandProcessor's lock.
    /// </summary>
    public class HttpCommandServer : IDisposable
    {
        private readonly HttpListener        _listener  = new();
        private readonly CommandProcessor    _processor;
        private readonly CommandDispatcher   _dispatcher;
        private readonly SceneStore          _store;
        private readonly AiChatService?      _chatService;
        private readonly string?             _apiKey;
        private readonly bool                _enableShellRun;
        private readonly bool                _bindAll;
        private readonly string?             _testRunnerExePath;
        private readonly string?             _testRunnerConfigPath;
        private readonly DateTime            _startTime = DateTime.UtcNow;
        private          CancellationTokenSource? _cts;
        private          Task?              _listenTask;
        private          int                _activeRequests;   // Interlocked counter for graceful drain

        // ── Metrics ───────────────────────────────────────────────────────
        private long _totalRequests;
        private long _errorRequests;
        private readonly ConcurrentDictionary<string, long>   _routeCounts       = new();
        private readonly ConcurrentDictionary<string, double> _routeLastLatencyMs = new();

        public int    Port      { get; }
        public bool   IsRunning { get; private set; }
        public event  Action<string>? OnLog;

        /// <summary>
        /// Fires after a successful POST /shutdown, just after the response is flushed.
        /// Hosts (WinForms GUI, Windows Service) subscribe to drive their own graceful exit.
        /// If no subscribers are attached, the server calls <see cref="Environment.Exit"/>
        /// so the remote shutdown still takes effect.
        /// </summary>
        public event  Action? OnShutdownRequested;

        /// <summary>
        /// Creates the HTTP server.
        /// When <paramref name="apiKey"/> is non-empty every request must supply it via
        /// "Authorization: Bearer &lt;key&gt;", "X-Api-Key: &lt;key&gt;", or "?apiKey=&lt;key&gt;".
        /// Unauthenticated requests receive HTTP 401.
        /// Pass null or empty to disable authentication (dev/local use only).
        /// <paramref name="enableShellRun"/> must be explicitly set to true to enable the
        /// POST /run shell-execution endpoint. It is disabled by default because it allows
        /// authenticated callers to execute arbitrary OS commands.
        /// </summary>
        /// <paramref name="bindAll"/> controls the listener prefix:
        /// false (default) → <c>http://localhost:{port}/</c> (loopback only, safer default).
        /// true → <c>http://+:{port}/</c> (all interfaces; set APEX_HTTP_BIND_ALL=true or HttpBindAll in appsettings.json).
        public HttpCommandServer(int port, CommandProcessor processor, SceneStore store,
                                 AiChatService? chatService = null,
                                 string? apiKey = null, bool enableShellRun = false,
                                 bool bindAll = false,
                                 string? testRunnerExePath = null,
                                 string? testRunnerConfigPath = null)
        {
            Port            = port;
            _processor      = processor;
            _dispatcher     = new CommandDispatcher(processor);
            _store          = store;
            _chatService    = chatService;
            _apiKey         = string.IsNullOrWhiteSpace(apiKey) ? null : apiKey.Trim();
            _enableShellRun = enableShellRun;
            _bindAll        = bindAll;
            _testRunnerExePath    = string.IsNullOrWhiteSpace(testRunnerExePath)    ? null : testRunnerExePath.Trim();
            _testRunnerConfigPath = string.IsNullOrWhiteSpace(testRunnerConfigPath) ? null : testRunnerConfigPath.Trim();
        }

        // ── Lifecycle ─────────────────────────────────────────────────────

        public void Start()
        {
            if (IsRunning) return;
            _listener.Prefixes.Clear();
            // Default to loopback-only; set bindAll=true (or APEX_HTTP_BIND_ALL=true) for network-wide binding.
            string prefix = _bindAll ? $"http://+:{Port}/" : $"http://localhost:{Port}/";
            _listener.Prefixes.Add(prefix);
            _listener.Start();
            IsRunning  = true;
            _cts       = new CancellationTokenSource();
            _listenTask = Task.Run(() => ListenLoop(_cts.Token));
            string bindDesc = _bindAll ? $"http://+:{Port}/ (all interfaces)" : $"http://localhost:{Port}/";
            OnLog?.Invoke($"HTTP server listening on {bindDesc}");
        }

        public void Stop(TimeSpan? drainTimeout = null)
        {
            if (!IsRunning) return;
            _cts?.Cancel();
            try { _listener.Stop(); } catch { /* already stopped */ }
            IsRunning = false;

            // Wait for in-flight request handlers to complete (default 5 s).
            var deadline = DateTime.UtcNow + (drainTimeout ?? TimeSpan.FromSeconds(5));
            while (Volatile.Read(ref _activeRequests) > 0 && DateTime.UtcNow < deadline)
                Thread.Sleep(50);

            int abandoned = Volatile.Read(ref _activeRequests);
            OnLog?.Invoke(abandoned > 0
                ? $"HTTP server stopped ({abandoned} request(s) abandoned after drain timeout)."
                : "HTTP server stopped.");
        }

        // ── Accept loop ───────────────────────────────────────────────────

        private async Task ListenLoop(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var ctx = await _listener.GetContextAsync().WaitAsync(ct);
                    Interlocked.Increment(ref _activeRequests);
                    _ = Task.Run(async () =>
                    {
                        try   { await HandleAsync(ctx); }
                        finally { Interlocked.Decrement(ref _activeRequests); }
                    }, ct);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex) when (!ct.IsCancellationRequested)
                {
                    OnLog?.Invoke($"HTTP listener error: {ex.Message}");
                }
            }
        }

        // ── Request handler ───────────────────────────────────────────────

        // ── Authentication ────────────────────────────────────────────────

        /// <summary>
        /// Returns true if the request carries the correct API key, or if auth is disabled.
        /// Checks (in order): Authorization: Bearer, X-Api-Key header, ?apiKey= query param.
        /// </summary>
        private bool IsAuthenticated(HttpListenerRequest req)
        {
            if (_apiKey == null) return true;   // auth disabled

            // Authorization: Bearer <key>
            string? authHeader = req.Headers["Authorization"];
            if (authHeader != null && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                return authHeader[7..].Trim() == _apiKey;

            // X-Api-Key: <key>
            string? apiKeyHeader = req.Headers["X-Api-Key"];
            if (apiKeyHeader != null)
                return apiKeyHeader.Trim() == _apiKey;

            // ?apiKey=<key>  (convenient for browser / curl testing)
            string? queryKey = req.QueryString["apiKey"];
            if (queryKey != null)
                return queryKey.Trim() == _apiKey;

            return false;
        }

        private static readonly byte[] UnauthorizedBody =
            System.Text.Encoding.UTF8.GetBytes(
                """{"success":false,"error":"Unauthorized. Supply the API key via 'Authorization: Bearer <key>', 'X-Api-Key: <key>' header, or '?apiKey=<key>' query parameter."}""");

        private async Task HandleAsync(HttpListenerContext ctx)
        {
            var req      = ctx.Request;
            var res      = ctx.Response;
            string method   = req.HttpMethod.ToUpperInvariant();
            string rawPath  = req.Url?.AbsolutePath.TrimEnd('/') ?? "/";
            string clientIp = req.RemoteEndPoint?.Address?.ToString() ?? "?";
            var    sw       = Stopwatch.StartNew();
            int    statusCode = 200;

            Interlocked.Increment(ref _totalRequests);

            try
            {

            // ── Unauthenticated health probe (safe to expose; no sensitive data) ─
            if (method == "GET" && rawPath.TrimEnd('/').Equals("/health", StringComparison.OrdinalIgnoreCase))
            {
                statusCode = await WriteResponse(res, HandleHealth(), "json");
                return;
            }

            // ── Auth gate ─────────────────────────────────────────────────
            if (!IsAuthenticated(req))
            {
                statusCode = 401;
                Interlocked.Increment(ref _errorRequests);
                res.StatusCode      = 401;
                res.ContentType     = "application/json; charset=utf-8";
                res.Headers["WWW-Authenticate"] = "Bearer realm=\"ApexComputerUse\"";
                res.ContentLength64 = UnauthorizedBody.Length;
                try   { await res.OutputStream.WriteAsync(UnauthorizedBody); }
                finally { res.Close(); }
                return;
            }
            string ext     = Path.GetExtension(rawPath).ToLowerInvariant();
            bool   hasExt  = ext is ".json" or ".html" or ".htm" or ".txt" or ".text" or ".pdf";
            // Strip format extension for routing; keep original for format detection
            string path    = hasExt ? rawPath[..^ext.Length].ToLowerInvariant()
                                    : rawPath.ToLowerInvariant();
            string format  = FormatAdapter.Negotiate(req, hasExt ? ext[1..] : null);

            ApexResult result;

            try
            {
                string body = req.HasEntityBody
                    ? await new StreamReader(req.InputStream, req.ContentEncoding).ReadToEndAsync()
                    : "";

                // Test page — served directly, bypasses format adapter
                if (method == "GET" && (path == "" || path == "/"))
                {
                    await ServeTestPage(res);
                    return;
                }

                // Scene editor page — served directly
                if (method == "GET" && path == "/editor")
                {
                    await ServeEditorPage(res);
                    return;
                }

                // AI Chat page — served directly with key embedded
                if (method == "GET" && path == "/chat")
                {
                    await ServeChatPage(res, _apiKey);
                    return;
                }

                // AI Chat SSE streaming send
                if (method == "POST" && path == "/chat/send")
                {
                    await HandleChatSendAsync(req, res, body);
                    return;
                }

                // Launch TestRunner as a detached child process
                if (method == "POST" && path == "/run-tests")
                {
                    var runResult = LaunchTestRunner(req);
                    statusCode = await WriteResponse(res, runResult, format);
                    if (statusCode >= 400) Interlocked.Increment(ref _errorRequests);
                    return;
                }

                // Graceful shutdown — host decides how to exit (WinForms: Application.Exit;
                // Service: Stop()). Falls back to Environment.Exit if nothing subscribed.
                if (method == "POST" && path == "/shutdown")
                {
                    var shutdownResult = new ApexResult
                    {
                        Success = true,
                        Action  = "shutdown",
                        Data    = new Dictionary<string, string> { ["message"] = "Exiting" }
                    };
                    statusCode = await WriteResponse(res, shutdownResult, format);
                    OnLog?.Invoke("Shutdown requested via POST /shutdown.");
                    _ = Task.Run(async () =>
                    {
                        // Let the response flush and give the listener a moment to drain.
                        await Task.Delay(250).ConfigureAwait(false);
                        if (OnShutdownRequested is { } handler)
                        {
                            try { handler.Invoke(); }
                            catch (Exception ex) { OnLog?.Invoke($"Shutdown handler threw: {ex.Message}"); }
                        }
                        else
                        {
                            // No host wired — exit the process directly so the remote caller
                            // still gets what they asked for.
                            Environment.Exit(0);
                        }
                    });
                    return;
                }

                // Scene REST routes — handled before the main switch
                var sceneResult = TryHandleSceneRoute(method, path, body, req);
                if (sceneResult != null)
                {
                    statusCode = await WriteResponse(res, sceneResult, format);
                    if (statusCode >= 400) Interlocked.Increment(ref _errorRequests);
                    return;
                }

                // /run is async — handled before the sync switch
                if (path == "/run")
                {
                    if (!_enableShellRun)
                    {
                        result = new ApexResult
                        {
                            Success = false,
                            Action  = "run",
                            Error   = "The /run endpoint is disabled. " +
                                      "Set enableShellRun=true when constructing HttpCommandServer to opt in."
                        };
                    }
                    else
                    {
                        string? cmd = method == "GET"
                            ? req.QueryString["cmd"]
                            : FromJson(body, "run").Value;
                        result = await HandleRunAsync(cmd);
                    }
                }
                else
                {
                    result = (method, path) switch
                    {
                        // ── Observability routes ───────────────────────────────────
                        ("GET", "/health")  => HandleHealth(),
                        ("GET", "/metrics") => HandleMetrics(),
                        // ── Diagnostic routes (auth-gated) ─────────────────────────
                        ("GET", "/ping")    => HandlePing(),
                        ("GET", "/sysinfo") => HandleSysinfo(),
                        ("GET", "/env")     => HandleEnv(),
                        ("GET", "/ls")      => HandleLs(req.QueryString["path"]),

                        // ── Existing routes — adapted to ApexResult ────────────────
                        ("GET", "/status")
                            => ApexResult.From("status",     _dispatcher.Dispatch(new CommandRequest { Command = "status" })),
                        ("GET", "/windows")
                            => ApexResult.From("windows",    _dispatcher.Dispatch(new CommandRequest { Command = "windows" })),
                        ("GET", "/help")
                            => ApexResult.From("help",       _dispatcher.Dispatch(new CommandRequest { Command = "help" })),
                        ("GET", "/elements")
                            => ApexResult.From("elements",   _dispatcher.Dispatch(new CommandRequest
                            {
                                Command        = "elements",
                                SearchType     = req.QueryString["type"],
                                AutomationId   = req.QueryString["id"],       // numeric — expands a subtree from a previously-mapped element
                                Depth          = int.TryParse(req.QueryString["depth"], out int _elemDepth) ? _elemDepth : null,
                                OnscreenOnly   = string.Equals(req.QueryString["onscreen"],       "true", StringComparison.OrdinalIgnoreCase),
                                // ── Browser-friendly tree shaping (opt-in; see README) ──
                                Match          = req.QueryString["match"],                        // text-search Name/AutomationId/Value
                                CollapseChains = string.Equals(req.QueryString["collapseChains"], "true", StringComparison.OrdinalIgnoreCase),
                                IncludePath    = string.Equals(req.QueryString["includePath"],    "true", StringComparison.OrdinalIgnoreCase),
                                Properties     = req.QueryString["properties"],                   // "extra" → value + helpText
                                ChangedSince   = req.QueryString["since"] ?? req.QueryString["changedSince"]
                            })),
                        ("GET", "/uimap")
                            => ApexResult.From("uimap",      _dispatcher.Dispatch(new CommandRequest { Command = "uimap" })),
                        ("GET", "/ai/status")
                            => ApexResult.From("ai/status",  _dispatcher.Dispatch(new CommandRequest { Command = "ai", Action = "status" })),
                        ("GET", "/chat/status")  => HandleChatStatus(),
                        ("POST", "/chat/reset")  => HandleChatReset(),
                        ("POST", "/find") or ("GET", "/find")
                            => ApexResult.From("find",    _dispatcher.Dispatch(
                                method == "POST" ? FromJson(body, "find") : FromQueryString(req, "find"))),
                        ("POST", "/execute") or ("GET", "/execute")
                            => ApexResult.From("execute", _dispatcher.Dispatch(
                                method == "POST" ? FromJson(body, "execute") : FromQueryString(req, "execute"))),
                        ("POST", "/exec") or ("GET", "/exec")
                            => ApexResult.From("execute", _dispatcher.Dispatch(
                                method == "POST" ? FromJson(body, "execute") : FromQueryString(req, "execute"))),
                        ("POST", "/ocr") or ("GET", "/ocr")
                            => ApexResult.From("ocr",     _dispatcher.Dispatch(
                                method == "POST" ? FromJson(body, "ocr") : FromQueryString(req, "ocr"))),
                        ("POST", "/capture") or ("GET", "/capture")
                            => ApexResult.From("capture", _dispatcher.Dispatch(
                                method == "POST" ? FromJson(body, "capture") : FromQueryString(req, "capture"))),
                        ("POST", "/draw") or ("GET", "/draw")
                            => ApexResult.From("draw",    _dispatcher.Dispatch(
                                method == "POST" ? FromJson(body, "draw")    : FromQueryString(req, "draw"))),
                        ("GET", "/draw/demo") => HandleDrawDemo(req),
                        ("POST", "/ai/init")
                            => ApexResult.From("ai/init",    _dispatcher.Dispatch(FromJson(body, "ai", "init"))),
                        ("POST", "/ai/describe")
                            => ApexResult.From("ai/describe",_dispatcher.Dispatch(FromJson(body, "ai", "describe"))),
                        ("POST", "/ai/file")
                            => ApexResult.From("ai/file",    _dispatcher.Dispatch(FromJson(body, "ai", "file"))),
                        ("POST", "/ai/ask")
                            => ApexResult.From("ai/ask",     _dispatcher.Dispatch(FromJson(body, "ai", "ask"))),
                        _ => new ApexResult
                        {
                            Success = false,
                            Action  = $"{method} {path}",
                            Error   = $"Unknown route. GET /help for endpoints."
                        }
                    };
                }
            }
            catch (Exception ex)
            {
                result = new ApexResult { Success = false, Action = $"{method} {path}", Error = ex.Message };
            }

            statusCode = await WriteResponse(res, result, format);
            if (statusCode >= 400) Interlocked.Increment(ref _errorRequests);

            } // end try
            finally
            {
                sw.Stop();
                string routeKey = $"{method} {rawPath}";
                _routeCounts.AddOrUpdate(routeKey, 1, (_, n) => n + 1);
                _routeLastLatencyMs[routeKey] = sw.Elapsed.TotalMilliseconds;
                OnLog?.Invoke($"HTTP {statusCode} {method} {rawPath} ({sw.ElapsedMilliseconds}ms) [{clientIp}]");
            }
        }

        /// <summary>Returns the HTTP status code that was written.</summary>
        private static async Task<int> WriteResponse(HttpListenerResponse res, ApexResult result, string format)
        {
            var (buf, contentType, statusCode) = FormatAdapter.Render(result, format);
            res.ContentType     = contentType;
            res.ContentLength64 = buf.Length;
            res.StatusCode      = statusCode;
            try   { await res.OutputStream.WriteAsync(buf); }
            finally { res.Close(); }
            return statusCode;
        }

        // ── Scene REST routes ─────────────────────────────────────────────

        /// <summary>
        /// Returns a result if the path matches a /scenes/* route, null otherwise.
        /// </summary>
        private ApexResult? TryHandleSceneRoute(string method, string path,
                                                 string body, HttpListenerRequest req)
        {
            // Segments: ["", "scenes", id?, "layers"?, lid?, "shapes"?, sid?]
            var seg = path.Split('/');
            if (seg.Length < 2 || seg[1] != "scenes") return null;

            try
            {
                // POST /scenes/[id]/shapes/[sid]/move
                if (seg.Length == 6 && seg[4] == "shapes" && seg[5] == "move" && method == "POST")
                {
                    using var d = JsonDocument.Parse(body.Length > 0 ? body : "{}");
                    string targetLayerId = d.RootElement.Str("target_layer_id") ?? "";
                    var ss = _store.MoveShapeToLayer(seg[2], seg[3], targetLayerId);
                    return Ok("scene/shapes/move", "shape", JsonSerializer.Serialize(ss));
                }

                switch (seg.Length)
                {
                    // GET /scenes   POST /scenes
                    case 2:
                        if (method == "GET")
                        {
                            var list = _store.ListScenes();
                            return Ok("scenes/list", "scenes", JsonSerializer.Serialize(list));
                        }
                        if (method == "POST")
                        {
                            using var d = JsonDocument.Parse(body.Length > 0 ? body : "{}");
                            var scene = _store.CreateScene(
                                d.RootElement.Str("name") ?? "Untitled",
                                d.RootElement.Int("width") ?? 800,
                                d.RootElement.Int("height") ?? 600,
                                d.RootElement.Str("background") ?? "white");
                            return Ok("scenes/create", "scene", JsonSerializer.Serialize(scene));
                        }
                        break;

                    // GET/PUT/DELETE /scenes/{id}   GET /scenes/{id}/render
                    case 3:
                    {
                        string id = seg[2];
                        if (method == "GET")
                        {
                            var scene = _store.GetScene(id)
                                        ?? throw new KeyNotFoundException($"Scene '{id}' not found.");
                            return Ok("scenes/get", "scene", JsonSerializer.Serialize(scene));
                        }
                        if (method == "PUT" || method == "PATCH")
                        {
                            using var d = JsonDocument.Parse(body.Length > 0 ? body : "{}");
                            var scene = _store.UpdateSceneMeta(id,
                                d.RootElement.Str("name"),
                                d.RootElement.Int("width"),
                                d.RootElement.Int("height"),
                                d.RootElement.Str("background"));
                            return Ok("scenes/update", "scene", JsonSerializer.Serialize(scene));
                        }
                        if (method == "DELETE")
                        {
                            _store.DeleteScene(id);
                            return Ok("scenes/delete", "deleted", id);
                        }
                        break;
                    }

                    case 4 when seg[3] == "render":
                    {
                        string base64 = _store.RenderScene(seg[2]);
                        return Ok("scenes/render", "result", base64);
                    }

                    // GET/POST /scenes/{id}/layers
                    case 4 when seg[3] == "layers":
                    {
                        string sceneId = seg[2];
                        if (method == "GET")
                        {
                            var scene = _store.GetScene(sceneId)
                                        ?? throw new KeyNotFoundException($"Scene '{sceneId}' not found.");
                            return Ok("scenes/layers/list", "layers", JsonSerializer.Serialize(scene.Layers));
                        }
                        if (method == "POST")
                        {
                            using var d = JsonDocument.Parse(body.Length > 0 ? body : "{}");
                            var layer = _store.AddLayer(sceneId, d.RootElement.Str("name") ?? "Layer");
                            return Ok("scenes/layers/add", "layer", JsonSerializer.Serialize(layer));
                        }
                        break;
                    }

                    // GET/PUT/DELETE /scenes/{id}/layers/{lid}
                    case 5 when seg[3] == "layers":
                    {
                        string sceneId  = seg[2];
                        string layerId  = seg[4];
                        if (method == "PUT" || method == "PATCH")
                        {
                            using var d = JsonDocument.Parse(body.Length > 0 ? body : "{}");
                            float? opacity = d.RootElement.TryGetProperty("opacity", out var op)
                                             && op.ValueKind == JsonValueKind.Number
                                             ? (float?)op.GetSingle() : null;
                            int?   zIndex  = d.RootElement.Int("z_index") ?? d.RootElement.Int("zIndex");
                            bool?  visible = d.RootElement.Bool("visible");
                            bool?  locked  = d.RootElement.Bool("locked");
                            var    layer   = _store.UpdateLayer(sceneId, layerId,
                                                d.RootElement.Str("name"), visible, locked, opacity, zIndex);
                            return Ok("scenes/layers/update", "layer", JsonSerializer.Serialize(layer));
                        }
                        if (method == "DELETE")
                        {
                            _store.DeleteLayer(sceneId, layerId);
                            return Ok("scenes/layers/delete", "deleted", layerId);
                        }
                        break;
                    }

                    // GET/POST /scenes/{id}/layers/{lid}/shapes
                    case 6 when seg[3] == "layers" && seg[5] == "shapes":
                    {
                        string sceneId = seg[2];
                        string layerId = seg[4];
                        if (method == "GET")
                        {
                            var scene = _store.GetScene(sceneId)
                                        ?? throw new KeyNotFoundException($"Scene '{sceneId}' not found.");
                            var layer = scene.Layers.FirstOrDefault(l => l.Id == layerId)
                                        ?? throw new KeyNotFoundException($"Layer '{layerId}' not found.");
                            return Ok("scenes/shapes/list", "shapes", JsonSerializer.Serialize(layer.Shapes));
                        }
                        if (method == "POST")
                        {
                            using var d = JsonDocument.Parse(body.Length > 0 ? body : "{}");
                            string shapeJson = d.RootElement.TryGetProperty("shape", out var sp)
                                               ? sp.GetRawText() : body;
                            string? name = d.RootElement.Str("name");
                            var shapeCmd = JsonSerializer.Deserialize<AIDrawingCommand.ShapeCommand>(
                                shapeJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                                ?? throw new InvalidOperationException("Invalid shape JSON.");
                            var ss = _store.AddShape(sceneId, layerId, shapeCmd, name);
                            return Ok("scenes/shapes/add", "shape", JsonSerializer.Serialize(ss));
                        }
                        break;
                    }

                    // GET/PUT/PATCH/DELETE /scenes/{id}/layers/{lid}/shapes/{sid}
                    case 7 when seg[3] == "layers" && seg[5] == "shapes":
                    {
                        string sceneId = seg[2];
                        string layerId = seg[4];
                        string shapeId = seg[6];
                        if (method == "PUT")
                        {
                            using var d = JsonDocument.Parse(body.Length > 0 ? body : "{}");
                            string shapeJson = d.RootElement.TryGetProperty("shape", out var sp)
                                               ? sp.GetRawText() : body;
                            string? name = d.RootElement.Str("name");
                            var shapeCmd = JsonSerializer.Deserialize<AIDrawingCommand.ShapeCommand>(
                                shapeJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                                ?? throw new InvalidOperationException("Invalid shape JSON.");
                            var ss = _store.UpdateShape(sceneId, layerId, shapeId, shapeCmd, name);
                            return Ok("scenes/shapes/update", "shape", JsonSerializer.Serialize(ss));
                        }
                        if (method == "PATCH")
                        {
                            using var d = JsonDocument.Parse(body.Length > 0 ? body : "{}");
                            var ss = _store.PatchShapeGeometry(sceneId, layerId, shapeId,
                                d.RootElement.Float("x"),  d.RootElement.Float("y"),
                                d.RootElement.Float("x2"), d.RootElement.Float("y2"),
                                d.RootElement.Float("w"),  d.RootElement.Float("h"),
                                d.RootElement.TryGetProperty("points", out var pts)
                                    ? pts.EnumerateArray().Select(p => p.GetSingle()).ToArray() : null,
                                r:          d.RootElement.Float("r"),
                                visible:    d.RootElement.Bool("visible"),
                                locked:     d.RootElement.Bool("locked"),
                                zIndex:     d.RootElement.Int("z_index") ?? d.RootElement.Int("zIndex"),
                                name:       d.RootElement.Str("name"),
                                rotation:   d.RootElement.Float("rotation"),
                                startAngle: d.RootElement.Float("start_angle"),
                                sweepAngle: d.RootElement.Float("sweep_angle"));
                            return Ok("scenes/shapes/patch", "shape", JsonSerializer.Serialize(ss));
                        }
                        if (method == "DELETE")
                        {
                            _store.DeleteShape(sceneId, layerId, shapeId);
                            return Ok("scenes/shapes/delete", "deleted", shapeId);
                        }
                        break;
                    }
                }
            }
            catch (KeyNotFoundException ex)
            {
                return new ApexResult { Success = false, Action = $"scenes {method} {path}", Error = ex.Message };
            }
            catch (Exception ex)
            {
                return new ApexResult { Success = false, Action = $"scenes {method} {path}", Error = ex.Message };
            }

            return null; // no match
        }

        private static ApexResult Ok(string action, string key, string value) =>
            new() { Success = true, Action = action,
                    Data = new Dictionary<string, string> { [key] = value } };

        // ── AI Chat ───────────────────────────────────────────────────────

        private ApexResult HandleChatStatus()
        {
            if (_chatService == null)
                return new ApexResult { Success = false, Action = "chat/status", Error = "Chat service not initialized." };
            return new ApexResult
            {
                Success = true,
                Action  = "chat/status",
                Data    = new Dictionary<string, string>
                {
                    ["provider"]      = _chatService.CurrentProvider ?? string.Empty,
                    ["model"]         = _chatService.CurrentModel ?? string.Empty,
                    ["sessionActive"] = _chatService.SessionActive.ToString()
                }
            };
        }

        private ApexResult HandleChatReset()
        {
            _chatService?.ResetSession();
            return new ApexResult { Success = true, Action = "chat/reset",
                Data = new Dictionary<string, string> { ["message"] = "Conversation reset." } };
        }

        private async Task HandleChatSendAsync(HttpListenerRequest req, HttpListenerResponse res, string body)
        {
            if (_chatService == null)
            {
                await WriteResponse(res, new ApexResult { Success = false, Action = "chat/send",
                    Error = "Chat service not initialized." }, "json");
                return;
            }

            string message = "";
            try
            {
                using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
                message = doc.RootElement.TryGetProperty("message", out var m) ? m.GetString() ?? "" : "";
            }
            catch { /* leave message empty */ }

            if (string.IsNullOrWhiteSpace(message))
            {
                await WriteResponse(res, new ApexResult { Success = false, Action = "chat/send",
                    Error = "\"message\" field is required." }, "json");
                return;
            }

            res.ContentType   = "text/event-stream; charset=utf-8";
            res.Headers["Cache-Control"]       = "no-cache";
            res.Headers["X-Accel-Buffering"]   = "no";
            res.Headers["Access-Control-Allow-Origin"] = "*";
            res.StatusCode    = 200;

            using var sw = new System.IO.StreamWriter(res.OutputStream,
                new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
                { AutoFlush = true };

            void WriteEvent(string data)
            {
                // JSON-encode the payload so newlines and special chars in tokens are safe.
                var json = JsonSerializer.Serialize(data);
                sw.Write($"data: {json}\n\n");
            }

            try
            {
                await _chatService.SendAsync(
                    message,
                    onToken:    token => WriteEvent(token),
                    onComplete: meta  => WriteEvent($"\u200b{meta}"),   // zero-width space prefix = metadata line
                    onError:    err   => WriteEvent($"\u26a0\ufe0f {err}"));
            }
            catch (Exception ex)
            {
                WriteEvent($"\u26a0\ufe0f {ex.Message}");
            }

            sw.Write("data: [DONE]\n\n");
            try { res.Close(); } catch { /* client may have disconnected */ }
        }

        private ApexResult LaunchTestRunner(HttpListenerRequest req)
        {
            if (_testRunnerExePath is null || !File.Exists(_testRunnerExePath))
            {
                return new ApexResult
                {
                    Success = false,
                    Action  = "run-tests",
                    Error   = "TestRunner is not configured. Set TestRunnerExePath in appsettings.json " +
                              "or the APEX_TEST_RUNNER_EXE_PATH environment variable."
                };
            }

            string? mode = req.QueryString["mode"]?.Trim().ToLowerInvariant();
            if (!string.IsNullOrEmpty(mode) && mode != "demo" && mode != "benchmark")
            {
                return new ApexResult
                {
                    Success = false,
                    Action  = "run-tests",
                    Error   = $"Invalid mode '{mode}'. Use 'demo' or 'benchmark'."
                };
            }

            var args = new List<string>();
            if (!string.IsNullOrWhiteSpace(_testRunnerConfigPath))
                args.Add($"\"{_testRunnerConfigPath}\"");
            if (!string.IsNullOrEmpty(mode))
                args.AddRange(new[] { "--mode", mode });

            var psi = new ProcessStartInfo
            {
                FileName         = _testRunnerExePath,
                Arguments        = string.Join(' ', args),
                UseShellExecute  = true,
                WorkingDirectory = Path.GetDirectoryName(_testRunnerExePath) ?? ""
            };

            try
            {
                var proc = Process.Start(psi);
                if (proc is null)
                {
                    return new ApexResult
                    {
                        Success = false,
                        Action  = "run-tests",
                        Error   = "Process.Start returned null."
                    };
                }

                OnLog?.Invoke($"TestRunner launched (PID {proc.Id}) mode={mode ?? "(default)"}");
                return new ApexResult
                {
                    Success = true,
                    Action  = "run-tests",
                    Data = new Dictionary<string, string>
                    {
                        ["pid"]  = proc.Id.ToString(),
                        ["exe"]  = _testRunnerExePath,
                        ["mode"] = mode ?? ""
                    }
                };
            }
            catch (Exception ex)
            {
                return new ApexResult
                {
                    Success = false,
                    Action  = "run-tests",
                    Error   = $"Failed to start TestRunner: {ex.Message}"
                };
            }
        }

        private static async Task ServeChatPage(HttpListenerResponse res, string? apiKey)
        {
            // JsonSerializer.Serialize escapes the string for safe embedding in a JS string literal.
            var escapedKey = JsonSerializer.Serialize(apiKey ?? "").Trim('"');
            var html = ChatPageHtml.Replace("__APEX_KEY__", escapedKey);
            var buf  = Encoding.UTF8.GetBytes(html);
            res.ContentType     = "text/html; charset=utf-8";
            res.ContentLength64 = buf.Length;
            res.StatusCode      = 200;
            try   { await res.OutputStream.WriteAsync(buf); }
            finally { res.Close(); }
        }

        private const string ChatPageHtml = """
            <!DOCTYPE html>
            <html lang="en">
            <head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width,initial-scale=1">
            <title>AI Chat — ApexComputerUse</title>
            <style>
              *, *::before, *::after { box-sizing: border-box; margin: 0; padding: 0; }
              :root { --bg:#1e1e1e; --surface:#252526; --border:#3e3e42; --accent:#0e639c;
                      --accent2:#1177bb; --text:#cccccc; --muted:#888; --user-bg:#0e639c;
                      --user-text:#fff; --asst-bg:#2d2d2d; --asst-border:#3e3e42; }
              body { font-family: Consolas, "Courier New", monospace; font-size: 14px;
                     background: var(--bg); color: var(--text);
                     height: 100vh; display: flex; flex-direction: column; overflow: hidden; }
              header { background: var(--surface); border-bottom: 1px solid var(--border);
                       padding: 8px 16px; display: flex; align-items: center; gap: 10px;
                       flex-shrink: 0; }
              header .title { font-weight: bold; font-size: 15px; color: #9cdcfe; }
              #badge { background: var(--accent); color: #fff; padding: 2px 8px;
                       border-radius: 4px; font-size: 12px; }
              #modelLabel { color: var(--muted); font-size: 12px; flex: 1; }
              #resetBtn { background: transparent; color: var(--muted); border: 1px solid var(--border);
                          padding: 3px 10px; border-radius: 4px; cursor: pointer; font-size: 12px; }
              #resetBtn:hover { color: var(--text); border-color: #888; }
              #messages { flex: 1; overflow-y: auto; padding: 16px;
                          display: flex; flex-direction: column; gap: 10px; }
              .msg-wrap { display: flex; flex-direction: column; }
              .msg-wrap.user  { align-items: flex-end; }
              .msg-wrap.asst  { align-items: flex-start; }
              .bubble { max-width: 78%; padding: 9px 13px; border-radius: 8px;
                        white-space: pre-wrap; word-break: break-word; line-height: 1.55; }
              .user .bubble  { background: var(--user-bg); color: var(--user-text); }
              .asst .bubble  { background: var(--asst-bg); border: 1px solid var(--asst-border); }
              .meta { font-size: 11px; color: var(--muted); margin-top: 3px; padding: 0 2px; }
              #inputArea { border-top: 1px solid var(--border); background: var(--surface);
                           padding: 10px 14px; display: flex; gap: 8px; flex-shrink: 0; }
              #inputBox { flex: 1; background: var(--bg); border: 1px solid var(--border);
                          color: var(--text); padding: 7px 10px; border-radius: 4px;
                          font-family: inherit; font-size: 14px; resize: none;
                          min-height: 38px; max-height: 120px; overflow-y: auto; }
              #inputBox:focus { outline: none; border-color: var(--accent); }
              #sendBtn { background: var(--accent); color: #fff; border: none;
                         padding: 0 18px; border-radius: 4px; cursor: pointer;
                         font-size: 14px; font-family: inherit; white-space: nowrap; }
              #sendBtn:hover { background: var(--accent2); }
              #sendBtn:disabled { opacity: 0.5; cursor: default; }
              #statusBar { font-size: 11px; color: var(--muted); padding: 2px 14px 6px; flex-shrink: 0; }
              .typing::after { content: "▋"; animation: blink .7s step-end infinite; }
              @keyframes blink { 50% { opacity: 0; } }
            </style>
            </head>
            <body>
            <header>
              <span class="title">AI Chat</span>
              <span id="badge">—</span>
              <span id="modelLabel">loading…</span>
              <button id="resetBtn" title="Clear conversation history on server">New chat</button>
            </header>
            <div id="messages"></div>
            <div id="statusBar"></div>
            <div id="inputArea">
              <textarea id="inputBox" rows="1" placeholder="Message… (Enter to send, Shift+Enter for newline)"></textarea>
              <button id="sendBtn">Send</button>
            </div>
            <script>
            'use strict';
            const API_KEY = '__APEX_KEY__';

            const $messages  = document.getElementById('messages');
            const $input     = document.getElementById('inputBox');
            const $send      = document.getElementById('sendBtn');
            const $status    = document.getElementById('statusBar');
            const $badge     = document.getElementById('badge');
            const $model     = document.getElementById('modelLabel');

            function headers() {
              const h = { 'Content-Type': 'application/json' };
              if (API_KEY) h['X-Api-Key'] = API_KEY;
              return h;
            }

            async function loadStatus() {
              try {
                const r = await fetch('/chat/status', { headers: headers() });
                const d = await r.json();
                if (d.success && d.data) {
                  $badge.textContent = d.data.provider || '—';
                  $model.textContent = d.data.model || '';
                }
              } catch { $model.textContent = 'could not connect'; }
            }

            function addMessage(role, text) {
              const wrap   = document.createElement('div');
              wrap.className = `msg-wrap ${role}`;
              const bubble = document.createElement('div');
              bubble.className = 'bubble';
              bubble.textContent = text;
              wrap.appendChild(bubble);
              $messages.appendChild(wrap);
              $messages.scrollTop = $messages.scrollHeight;
              return bubble;
            }

            function setStatus(t) { $status.textContent = t; }

            async function send() {
              const text = $input.value.trim();
              if (!text) return;
              $input.value = '';
              autoResize();
              $send.disabled = true;
              addMessage('user', text);

              const bubble = addMessage('asst', '');
              bubble.classList.add('typing');
              setStatus('Thinking…');

              let buffer = '';
              try {
                const res = await fetch('/chat/send', {
                  method: 'POST',
                  headers: headers(),
                  body: JSON.stringify({ message: text })
                });
                const reader  = res.body.getReader();
                const decoder = new TextDecoder();
                let leftover  = '';

                while (true) {
                  const { done, value } = await reader.read();
                  if (done) break;
                  leftover += decoder.decode(value, { stream: true });
                  const lines = leftover.split('\n');
                  leftover = lines.pop();
                  for (const line of lines) {
                    if (!line.startsWith('data: ')) continue;
                    const raw = line.slice(6);
                    if (raw === '[DONE]') { setStatus(''); break; }
                    try {
                      const tok = JSON.parse(raw);
                      // zero-width space prefix = metadata line (show in status, not bubble)
                      if (tok.startsWith('\u200b')) { setStatus(tok.slice(1)); continue; }
                      buffer += tok;
                      bubble.textContent = buffer;
                      $messages.scrollTop = $messages.scrollHeight;
                    } catch { /* partial chunk */ }
                  }
                }
              } catch (e) {
                setStatus('Error: ' + e.message);
              } finally {
                bubble.classList.remove('typing');
                $send.disabled = false;
                $input.focus();
              }
            }

            document.getElementById('resetBtn').onclick = async () => {
              await fetch('/chat/reset', { method: 'POST', headers: headers() });
              $messages.innerHTML = '';
              setStatus('Conversation cleared.');
              setTimeout(() => setStatus(''), 2000);
            };

            $send.onclick = send;
            $input.addEventListener('keydown', e => {
              if (e.key === 'Enter' && !e.shiftKey) { e.preventDefault(); send(); }
            });

            function autoResize() {
              $input.style.height = 'auto';
              $input.style.height = Math.min($input.scrollHeight, 120) + 'px';
            }
            $input.addEventListener('input', autoResize);

            loadStatus();
            $input.focus();
            </script>
            </body>
            </html>
            """;

        // ── Editor page ───────────────────────────────────────────────────

        private static async Task ServeEditorPage(HttpListenerResponse res)
        {
            const string html = """
                <!DOCTYPE html>
                <html lang="en">
                <head>
                <meta charset="utf-8">
                <title>Scene Editor — ApexComputerUse</title>
                <style>
                  *, *::before, *::after { box-sizing: border-box; margin: 0; padding: 0; }
                  body { font-family: monospace; font-size: 13px; background: #1e1e1e; color: #d4d4d4;
                         display: flex; flex-direction: column; height: 100vh; overflow: hidden; }
                  header { background: #252526; border-bottom: 1px solid #3c3c3c;
                           padding: .35em 1em; display: flex; align-items: center; gap: .75em; flex-shrink: 0; }
                  header a { color: #4ec9b0; text-decoration: none; font-size: .82em; }
                  header a:hover { text-decoration: underline; }
                  header span.spacer { flex: 1; }
                  header .brand { font-size: .82em; color: #9cdcfe; }

                  main { display: flex; flex: 1; overflow: hidden; min-height: 0; }

                  /* Scenes list */
                  #scenePanel { width: 200px; background: #252526; border-right: 1px solid #3c3c3c;
                                display: flex; flex-direction: column; flex-shrink: 0; }
                  .ph { padding: .35em .6em; font-size: .7em; color: #888; text-transform: uppercase;
                        letter-spacing: .06em; background: #2d2d30; display: flex; align-items: center; flex-shrink: 0; }
                  .ph span { flex: 1; }
                  .ph button { background: none; border: none; color: #888; cursor: pointer; padding: 0 .25em; font-size: 1.1em; line-height: 1; }
                  .ph button:hover { color: #d4d4d4; }
                  select.lst { flex: 1; background: #1e1e1e; color: #d4d4d4; border: none; outline: none;
                               font: inherit; font-size: .82em; overflow-y: auto; width: 100%; padding: .1em 0; }
                  select.lst option { padding: .15em .6em; cursor: pointer; }
                  select.lst option:checked { background: #094771; color: #fff; }
                  .btnrow { display: flex; gap: .3em; padding: .35em .5em; background: #2d2d30; flex-shrink: 0; }
                  .btnrow input { flex: 1; background: #3c3c3c; color: #d4d4d4; border: 1px solid #555;
                                  border-radius: 2px; padding: .15em .35em; font: inherit; font-size: .76em; }
                  button.sm { background: #2d2d30; color: #bbb; border: 1px solid #444;
                              border-radius: 3px; padding: .2em .5em; cursor: pointer;
                              font: inherit; font-size: .76em; white-space: nowrap; }
                  button.sm:hover { background: #3e3e42; color: #d4d4d4; }
                  button.sm.on { background: #094771; border-color: #1177bb; color: #fff; }

                  /* Editor area */
                  #editorArea { flex: 1; display: flex; flex-direction: column; overflow: hidden; }
                  #toolbar { background: #2d2d30; border-bottom: 1px solid #3c3c3c;
                             padding: .3em .6em; display: flex; gap: .3em; flex-shrink: 0; align-items: center; }
                  #toolbar span.sep { width: 1px; background: #555; height: 1.4em; margin: 0 .2em; }
                  #canvasWrap { flex: 1; overflow: hidden; position: relative; background: #111; }
                  canvas#cv { display: block; position: absolute; top: 0; left: 0; cursor: default; }

                  /* Layers + Props */
                  #rightPanel { width: 220px; background: #252526; border-left: 1px solid #3c3c3c;
                                display: flex; flex-direction: column; overflow: hidden; flex-shrink: 0; }
                  #layerPanel { flex: 0 0 180px; border-bottom: 1px solid #3c3c3c; display: flex; flex-direction: column; }
                  #layerList { flex: 1; overflow-y: auto; }
                  .layerItem { display: flex; align-items: center; gap: .3em; padding: .25em .5em;
                               cursor: pointer; font-size: .8em; border-bottom: 1px solid #2d2d30; }
                  .layerItem:hover { background: #2a2d2e; }
                  .layerItem.sel { background: #094771; }
                  .layerItem .eye { cursor: pointer; opacity: .5; user-select: none; }
                  .layerItem .eye.vis { opacity: 1; }
                  .layerItem .lname { flex: 1; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
                  #propsPanel { flex: 1; overflow-y: auto; padding: .5em; }
                  #propsPanel h4 { font-size: .7em; color: #888; text-transform: uppercase;
                                   letter-spacing: .06em; margin-bottom: .4em; }
                  .prop { display: flex; flex-direction: column; margin-bottom: .4em; }
                  .prop label { font-size: .72em; color: #888; margin-bottom: .15em; }
                  .prop input { background: #3c3c3c; color: #d4d4d4; border: 1px solid #555;
                                border-radius: 2px; padding: .18em .35em; font: inherit; font-size: .8em; }
                  .prop input:focus { outline: 1px solid #0e639c; }

                  #statusBar { background: #2d2d30; border-top: 1px solid #3c3c3c;
                               padding: .2em .8em; font-size: .72em; color: #888; flex-shrink: 0;
                               display: flex; gap: 1em; }

                  #noScene { display: flex; flex: 1; align-items: center; justify-content: center;
                             color: #555; font-size: 1.1em; }
                </style>
                </head>
                <body>
                <header>
                  <a href="/">&larr; Console</a>
                  <span class="spacer"></span>
                  <span id="hdrScene" style="font-size:.82em;color:#9cdcfe"></span>
                  <span class="brand">Scene Editor</span>
                </header>

                <main>
                  <!-- Scenes sidebar -->
                  <div id="scenePanel">
                    <div class="ph"><span>Scenes</span><button onclick="loadScenes()" title="Refresh">↺</button></div>
                    <select class="lst" id="sceneList" size="10" onchange="onSceneSelect()"></select>
                    <div class="btnrow">
                      <input id="newSceneName" placeholder="scene name…">
                      <button class="sm" onclick="createScene()">+</button>
                    </div>
                    <div class="btnrow">
                      <button class="sm" onclick="deleteScene()" style="color:#e05252">Delete</button>
                      <button class="sm" onclick="renderScene()">Render</button>
                    </div>
                  </div>

                  <!-- Canvas + toolbar + right panel -->
                  <div id="editorArea">
                    <div id="toolbar">
                      <button class="sm on" id="toolArrow" onclick="setTool('arrow')" title="Select / Move (V)">&#9654;</button>
                      <button class="sm" id="toolRect"    onclick="setTool('rect')"    title="Rectangle (R)">&#9645;</button>
                      <button class="sm" id="toolEllipse" onclick="setTool('ellipse')" title="Ellipse (E)">&#9711;</button>
                      <button class="sm" id="toolCircle"  onclick="setTool('circle')"  title="Circle (C)">&#9675;</button>
                      <button class="sm" id="toolLine"     onclick="setTool('line')"     title="Line (L)">&#9135;</button>
                      <button class="sm" id="toolText"     onclick="setTool('text')"     title="Text (T)">T</button>
                      <button class="sm" id="toolTriangle" onclick="setTool('triangle')" title="Triangle (G)">&#9651;</button>
                      <button class="sm" id="toolArc"      onclick="setTool('arc')"      title="Arc (A)">&#8978;</button>
                      <span class="sep"></span>
                      <button class="sm" onclick="deleteSelected()" title="Delete selected (Del)">&#128465;</button>
                      <span class="sep"></span>
                      <label style="font-size:.76em;color:#888">Fill</label>
                      <input id="fillColor" type="color" value="#4a90d9" style="width:28px;height:22px;cursor:pointer;background:none;border:1px solid #555;border-radius:2px;padding:1px;">
                      <label style="font-size:.76em;color:#888">Stroke</label>
                      <input id="strokeColor" type="color" value="#ffffff" style="width:28px;height:22px;cursor:pointer;background:none;border:1px solid #555;border-radius:2px;padding:1px;">
                    </div>
                    <div style="display:flex;flex:1;overflow:hidden;min-height:0">
                      <div id="canvasWrap">
                        <div id="noScene">Select or create a scene to begin</div>
                        <canvas id="cv" style="display:none"></canvas>
                      </div>
                      <!-- Right panel: layers + props -->
                      <div id="rightPanel">
                        <div id="layerPanel">
                          <div class="ph"><span>Layers</span>
                            <button onclick="addLayer()" title="Add layer">+</button>
                            <button onclick="deleteLayer()" title="Delete layer">&#10005;</button>
                          </div>
                          <div id="layerList"></div>
                        </div>
                        <div id="propsPanel">
                          <div id="propsContent"><p style="font-size:.78em;color:#555">Select a shape</p></div>
                        </div>
                      </div>
                    </div>
                    <div id="statusBar">
                      <span id="sCursor">x: — y: —</span>
                      <span id="sSelected">nothing selected</span>
                      <span id="sScene"></span>
                    </div>
                  </div>
                </main>

                <script>
                'use strict';

                // ── State ────────────────────────────────────────────────────
                let scenes      = [];
                let curScene    = null;   // full scene object
                let curLayerId  = null;
                let curShapeId  = null;
                let tool        = 'arrow';
                let viewScale   = 1;
                let viewOX      = 0;
                let viewOY      = 0;

                // Drag state
                let dragMode    = null;   // null | 'move' | 'pan' | 'place'
                let dragStart   = null;   // {x,y} in scene coords
                let dragShape   = null;   // snapshot of shape at drag start
                let newShape    = null;   // shape being placed

                const cv        = document.getElementById('cv');
                const ctx       = cv.getContext('2d');

                // ── Boot ─────────────────────────────────────────────────────
                (async () => { await loadScenes(); })();

                // ── Scene management ─────────────────────────────────────────
                async function loadScenes() {
                  const r = await api('GET', '/scenes');
                  scenes  = r.scenes ? JSON.parse(r.scenes) : [];
                  const sel = document.getElementById('sceneList');
                  sel.innerHTML = '';
                  scenes.forEach(s => {
                    const o = document.createElement('option');
                    o.value = s.id; o.textContent = s.name + ' (' + s.width + 'x' + s.height + ')';
                    sel.appendChild(o);
                  });
                  if (curScene) {
                    sel.value = curScene.id;
                    await refreshScene();
                  }
                }

                async function onSceneSelect() {
                  const id = document.getElementById('sceneList').value;
                  if (!id) return;
                  const r = await api('GET', '/scenes/' + id);
                  setScene(JSON.parse(r.scene));
                }

                async function createScene() {
                  const name = document.getElementById('newSceneName').value.trim() || 'Untitled';
                  const r    = await api('POST', '/scenes', { name });
                  scenes.push(JSON.parse(r.scene));
                  await loadScenes();
                  document.getElementById('sceneList').value = JSON.parse(r.scene).id;
                  await onSceneSelect();
                }

                async function deleteScene() {
                  if (!curScene) return;
                  if (!confirm('Delete scene "' + curScene.name + '"?')) return;
                  await api('DELETE', '/scenes/' + curScene.id);
                  curScene = null; curLayerId = null; curShapeId = null;
                  setCanvasVisible(false);
                  await loadScenes();
                }

                async function renderScene() {
                  if (!curScene) return;
                  const r   = await api('GET', '/scenes/' + curScene.id + '/render');
                  const img = document.createElement('img');
                  img.src   = 'data:image/png;base64,' + r.result;
                  img.style.cssText = 'max-width:100%;border:1px solid #555;margin:.5em 0';
                  const wrap = document.getElementById('canvasWrap');
                  // Remove previous render previews
                  wrap.querySelectorAll('img.render-preview').forEach(e => e.remove());
                  img.className = 'render-preview';
                  wrap.appendChild(img);
                }

                function setScene(scene) {
                  curScene = scene;
                  document.getElementById('hdrScene').textContent = scene.name;
                  document.getElementById('sScene').textContent   = scene.width + ' × ' + scene.height;
                  cv.width  = scene.width;
                  cv.height = scene.height;
                  viewOX = 0; viewOY = 0; viewScale = 1;
                  setCanvasVisible(true);
                  if (!curLayerId && scene.layers && scene.layers.length > 0)
                    curLayerId = scene.layers[0].id;
                  renderLayers();
                  draw();
                }

                async function refreshScene() {
                  if (!curScene) return;
                  const r = await api('GET', '/scenes/' + curScene.id);
                  setScene(JSON.parse(r.scene));
                }

                function setCanvasVisible(v) {
                  document.getElementById('noScene').style.display  = v ? 'none'  : 'flex';
                  cv.style.display = v ? 'block' : 'none';
                }

                // ── Layer management ──────────────────────────────────────────
                function renderLayers() {
                  const el  = document.getElementById('layerList');
                  el.innerHTML = '';
                  if (!curScene || !curScene.layers) return;
                  const sorted = [...curScene.layers].sort((a,b) => b.zIndex - a.zIndex);
                  sorted.forEach(l => {
                    const div = document.createElement('div');
                    div.className = 'layerItem' + (l.id === curLayerId ? ' sel' : '');
                    div.dataset.id = l.id;
                    div.innerHTML =
                      '<span class="eye ' + (l.visible ? 'vis' : '') + '" onclick="toggleLayerVis(\'' + l.id + '\',event)" title="Toggle visibility">&#128065;</span>' +
                      '<span class="lname" title="' + esc(l.name) + '">' + esc(l.name) + '</span>';
                    div.addEventListener('click', () => { curLayerId = l.id; renderLayers(); showShapeProps(); });
                    el.appendChild(div);
                  });
                }

                async function addLayer() {
                  if (!curScene) return;
                  const name = prompt('Layer name:', 'Layer ' + (curScene.layers.length + 1));
                  if (!name) return;
                  const r = await api('POST', '/scenes/' + curScene.id + '/layers', { name });
                  curScene.layers.push(JSON.parse(r.layer));
                  curLayerId = JSON.parse(r.layer).id;
                  renderLayers(); draw();
                }

                async function deleteLayer() {
                  if (!curScene || !curLayerId) return;
                  await api('DELETE', '/scenes/' + curScene.id + '/layers/' + curLayerId);
                  curScene.layers = curScene.layers.filter(l => l.id !== curLayerId);
                  curLayerId = curScene.layers.length > 0 ? curScene.layers[0].id : null;
                  curShapeId = null; renderLayers(); draw(); showShapeProps();
                }

                async function toggleLayerVis(layerId, ev) {
                  ev.stopPropagation();
                  if (!curScene) return;
                  const layer = curScene.layers.find(l => l.id === layerId);
                  if (!layer) return;
                  layer.visible = !layer.visible;
                  await api('PATCH', '/scenes/' + curScene.id + '/layers/' + layerId, { visible: layer.visible });
                  renderLayers(); draw();
                }

                // ── Shape helpers ─────────────────────────────────────────────
                function activeLayer() {
                  if (!curScene || !curLayerId) return null;
                  return curScene.layers.find(l => l.id === curLayerId) || null;
                }

                function findShape(shapeId) {
                  if (!curScene) return null;
                  for (const l of curScene.layers)
                    for (const s of l.shapes)
                      if (s.id === shapeId) return { shape: s, layer: l };
                  return null;
                }

                function curShape() {
                  const r = curShapeId ? findShape(curShapeId) : null;
                  return r ? r.shape : null;
                }

                // ── Canvas drawing ────────────────────────────────────────────
                function draw() {
                  if (!curScene) return;
                  ctx.clearRect(0, 0, cv.width, cv.height);

                  // Background
                  ctx.fillStyle = curScene.background || '#ffffff';
                  ctx.fillRect(0, 0, cv.width, cv.height);

                  // All layers sorted by zIndex
                  const layers = [...(curScene.layers || [])].sort((a,b) => a.zIndex - b.zIndex);
                  for (const l of layers) {
                    if (!l.visible) continue;
                    ctx.save();
                    ctx.globalAlpha = Math.max(0, Math.min(1, l.opacity ?? 1));
                    const shapes = [...(l.shapes || [])].sort((a,b) => a.zIndex - b.zIndex);
                    for (const ss of shapes) {
                      if (!ss.visible) continue;
                      drawShape(ctx, ss.shape);
                    }
                    ctx.restore();
                  }

                  // Selection box
                  const sel = curShape();
                  if (sel) drawSelection(ctx, sel);

                  // Update canvas transform to fit in wrapper
                  positionCanvas();
                }

                function positionCanvas() {
                  const wrap = document.getElementById('canvasWrap');
                  const scale = Math.min(
                    (wrap.clientWidth  - 20) / cv.width,
                    (wrap.clientHeight - 20) / cv.height,
                    1);
                  cv.style.transform       = `scale(${scale})`;
                  cv.style.transformOrigin = 'top left';
                  cv.style.left = Math.max(0, (wrap.clientWidth  - cv.width  * scale) / 2) + 'px';
                  cv.style.top  = Math.max(0, (wrap.clientHeight - cv.height * scale) / 2) + 'px';
                }

                function drawShape(ctx, s) {
                  if (!s || !s.type) return;
                  ctx.save();
                  ctx.globalAlpha *= (s.opacity ?? 1);
                  const color = s.color || '#ffffff';
                  ctx.strokeStyle = color;
                  ctx.fillStyle   = color;
                  ctx.lineWidth   = s.stroke_width ?? 2;
                  if (s.dashed) ctx.setLineDash([6, 3]);

                  // Apply center-origin rotation when set
                  if (s.rotation) {
                    const bb = shapeBBox(s);
                    if (bb) {
                      const cx = bb.x + bb.w/2, cy = bb.y + bb.h/2;
                      ctx.translate(cx, cy);
                      ctx.rotate(s.rotation * Math.PI / 180);
                      ctx.translate(-cx, -cy);
                    }
                  }

                  switch (s.type) {
                    case 'rect': {
                      const x = s.x ?? 0, y = s.y ?? 0, w = s.w ?? 100, h = s.h ?? 60;
                      if (s.corner_radius && s.corner_radius > 0) {
                        const r = s.corner_radius;
                        ctx.beginPath();
                        ctx.moveTo(x+r, y);
                        ctx.lineTo(x+w-r, y); ctx.quadraticCurveTo(x+w, y, x+w, y+r);
                        ctx.lineTo(x+w, y+h-r); ctx.quadraticCurveTo(x+w, y+h, x+w-r, y+h);
                        ctx.lineTo(x+r, y+h); ctx.quadraticCurveTo(x, y+h, x, y+h-r);
                        ctx.lineTo(x, y+r); ctx.quadraticCurveTo(x, y, x+r, y);
                        ctx.closePath();
                      } else {
                        ctx.beginPath(); ctx.rect(x, y, w, h);
                      }
                      if (s.fill) ctx.fill(); else ctx.stroke();
                      break;
                    }
                    case 'ellipse': {
                      const x = s.x ?? 0, y = s.y ?? 0, w = s.w ?? 100, h = s.h ?? 60;
                      ctx.beginPath();
                      ctx.ellipse(x + w/2, y + h/2, w/2, h/2, 0, 0, Math.PI*2);
                      if (s.fill) ctx.fill(); else ctx.stroke();
                      break;
                    }
                    case 'circle': {
                      ctx.beginPath();
                      ctx.arc(s.x ?? 0, s.y ?? 0, s.r ?? 40, 0, Math.PI*2);
                      if (s.fill) ctx.fill(); else ctx.stroke();
                      break;
                    }
                    case 'line':
                    case 'arrow': {
                      const x1 = s.x ?? 0, y1 = s.y ?? 0, x2 = s.x2 ?? 100, y2 = s.y2 ?? 0;
                      ctx.beginPath(); ctx.moveTo(x1, y1); ctx.lineTo(x2, y2); ctx.stroke();
                      if (s.type === 'arrow') {
                        const angle = Math.atan2(y2-y1, x2-x1);
                        const len   = 12;
                        ctx.beginPath();
                        ctx.moveTo(x2, y2);
                        ctx.lineTo(x2 - len*Math.cos(angle - Math.PI/6), y2 - len*Math.sin(angle - Math.PI/6));
                        ctx.moveTo(x2, y2);
                        ctx.lineTo(x2 - len*Math.cos(angle + Math.PI/6), y2 - len*Math.sin(angle + Math.PI/6));
                        ctx.stroke();
                      }
                      break;
                    }
                    case 'polygon': {
                      const pts = s.points || [];
                      if (pts.length >= 4) {
                        ctx.beginPath(); ctx.moveTo(pts[0], pts[1]);
                        for (let i = 2; i < pts.length - 1; i += 2)
                          ctx.lineTo(pts[i], pts[i+1]);
                        ctx.closePath();
                        if (s.fill) ctx.fill(); else ctx.stroke();
                      }
                      break;
                    }
                    case 'text': {
                      const size = s.font_size ?? 16;
                      ctx.font = (s.font_bold ? 'bold ' : '') + size + 'px monospace';
                      ctx.textAlign    = s.align ?? 'left';
                      ctx.textBaseline = 'top';
                      if (s.background) {
                        const m = ctx.measureText(s.text || '');
                        ctx.fillStyle = s.background;
                        ctx.fillRect(s.x ?? 0, s.y ?? 0, m.width + 4, size + 4);
                        ctx.fillStyle = color;
                      }
                      ctx.fillText(s.text || '', s.x ?? 0, s.y ?? 0);
                      break;
                    }
                    case 'triangle': {
                      const x = s.x??0, y = s.y??0, w = s.w??80, h = s.h??60;
                      ctx.beginPath();
                      ctx.moveTo(x + w/2, y);
                      ctx.lineTo(x, y + h);
                      ctx.lineTo(x + w, y + h);
                      ctx.closePath();
                      if (s.fill) ctx.fill(); else ctx.stroke();
                      break;
                    }
                    case 'arc': {
                      const x = s.x??0, y = s.y??0, w = s.w??80, h = s.h??80;
                      const startRad = (s.start_angle??0) * Math.PI / 180;
                      const sweepRad = (s.sweep_angle??90) * Math.PI / 180;
                      ctx.beginPath();
                      ctx.ellipse(x + w/2, y + h/2, w/2, h/2, 0, startRad, startRad + sweepRad);
                      ctx.stroke();
                      break;
                    }
                  }
                  ctx.restore();
                }

                function drawSelection(ctx, s) {
                  const bb = shapeBBox(s);
                  if (!bb) return;
                  ctx.save();
                  ctx.strokeStyle = '#0e9';
                  ctx.lineWidth   = 1;
                  ctx.setLineDash([4, 3]);
                  ctx.strokeRect(bb.x - 4, bb.y - 4, bb.w + 8, bb.h + 8);
                  ctx.setLineDash([]);
                  // handles
                  const hx = bb.x - 4, hy = bb.y - 4, hw = bb.w + 8, hh = bb.h + 8;
                  const pts = [ [hx,hy],[hx+hw/2,hy],[hx+hw,hy],
                                 [hx,hy+hh/2],[hx+hw,hy+hh/2],
                                 [hx,hy+hh],[hx+hw/2,hy+hh],[hx+hw,hy+hh] ];
                  ctx.fillStyle = '#0e9';
                  for (const [px,py] of pts) { ctx.fillRect(px-4, py-4, 8, 8); }
                  ctx.restore();
                }

                function shapeBBox(s) {
                  if (!s) return null;
                  switch (s.type) {
                    case 'rect':     return { x: s.x??0, y: s.y??0, w: s.w??100, h: s.h??60 };
                    case 'ellipse':  return { x: s.x??0, y: s.y??0, w: s.w??100, h: s.h??60 };
                    case 'triangle': return { x: s.x??0, y: s.y??0, w: s.w??80,  h: s.h??60 };
                    case 'arc':      return { x: s.x??0, y: s.y??0, w: s.w??80,  h: s.h??80 };
                    case 'circle':  return { x: (s.x??0)-(s.r??40), y: (s.y??0)-(s.r??40), w: (s.r??40)*2, h: (s.r??40)*2 };
                    case 'line': case 'arrow': {
                      const x1=s.x??0,y1=s.y??0,x2=s.x2??100,y2=s.y2??0;
                      return { x:Math.min(x1,x2), y:Math.min(y1,y2), w:Math.abs(x2-x1)||1, h:Math.abs(y2-y1)||1 };
                    }
                    case 'text':   return { x: s.x??0, y: s.y??0, w: (s.text||'').length*(s.font_size??16)*0.6, h: (s.font_size??16)+4 };
                    case 'polygon': {
                      const pts = s.points || [];
                      if (pts.length < 2) return null;
                      const xs = pts.filter((_,i)=>i%2===0), ys = pts.filter((_,i)=>i%2===1);
                      const x=Math.min(...xs),y=Math.min(...ys);
                      return { x, y, w: Math.max(...xs)-x||1, h: Math.max(...ys)-y||1 };
                    }
                  }
                  return null;
                }

                // ── Mouse events ──────────────────────────────────────────────
                cv.addEventListener('mousedown', onMouseDown);
                cv.addEventListener('mousemove', onMouseMove);
                cv.addEventListener('mouseup',   onMouseUp);
                window.addEventListener('resize', () => { if (curScene) positionCanvas(); });

                function canvasCoords(ev) {
                  const r = cv.getBoundingClientRect();
                  const scaleX = cv.width  / r.width;
                  const scaleY = cv.height / r.height;
                  return { x: (ev.clientX - r.left) * scaleX, y: (ev.clientY - r.top) * scaleY };
                }

                function hitTest(x, y) {
                  if (!curScene) return null;
                  const layers = [...curScene.layers].sort((a,b) => b.zIndex - a.zIndex);
                  for (const l of layers) {
                    if (!l.visible) continue;
                    const shapes = [...l.shapes].sort((a,b) => b.zIndex - a.zIndex);
                    for (const ss of shapes) {
                      if (!ss.visible) continue;
                      const bb = shapeBBox(ss.shape);
                      if (bb && x >= bb.x-6 && x <= bb.x+bb.w+6 && y >= bb.y-6 && y <= bb.y+bb.h+6)
                        return { ss, layer: l };
                    }
                  }
                  return null;
                }

                function onMouseDown(ev) {
                  const {x, y} = canvasCoords(ev);
                  if (tool === 'arrow') {
                    const hit = hitTest(x, y);
                    if (hit) {
                      curShapeId = hit.ss.id;
                      curLayerId = hit.layer.id;
                      dragMode   = 'move';
                      dragStart  = {x, y};
                      dragShape  = JSON.parse(JSON.stringify(hit.ss.shape));
                      draw();
                    } else {
                      curShapeId = null; dragMode = null; draw(); showShapeProps();
                    }
                    return;
                  }
                  // Placing a new shape
                  const layer = activeLayer();
                  if (!layer) { alert('Select a layer first'); return; }
                  dragMode = 'place';
                  dragStart = {x, y};
                  newShape = makeNewShape(tool, x, y);
                  layer.shapes.push({ id: '_new', name: tool, visible: true, locked: false, zIndex: layer.shapes.length, shape: newShape });
                  draw();
                }

                function onMouseMove(ev) {
                  const {x, y} = canvasCoords(ev);
                  document.getElementById('sCursor').textContent = 'x: ' + Math.round(x) + '  y: ' + Math.round(y);

                  if (dragMode === 'move' && dragShape) {
                    const dx = x - dragStart.x, dy = y - dragStart.y;
                    const ss = findShape(curShapeId);
                    if (!ss) return;
                    const s = ss.shape;
                    s.x  = (dragShape.x  ?? 0) + dx;
                    s.y  = (dragShape.y  ?? 0) + dy;
                    if (dragShape.x2 !== undefined) s.x2 = dragShape.x2 + dx;
                    if (dragShape.y2 !== undefined) s.y2 = dragShape.y2 + dy;
                    if (dragShape.points) {
                      const pts = [...dragShape.points];
                      for (let i = 0; i < pts.length; i += 2) { pts[i] += dx; pts[i+1] += dy; }
                      s.points = pts;
                    }
                    draw();
                  } else if (dragMode === 'place' && newShape) {
                    updateShapeSize(newShape, dragStart, x, y);
                    draw();
                  }
                }

                async function onMouseUp(ev) {
                  const {x, y} = canvasCoords(ev);

                  if (dragMode === 'move' && curShapeId) {
                    const ss = findShape(curShapeId);
                    if (ss) {
                      const s = ss.shape;
                      const layerId = ss.layer.id;
                      const patch = { x: s.x, y: s.y };
                      if (s.x2 !== undefined) patch.x2 = s.x2;
                      if (s.y2 !== undefined) patch.y2 = s.y2;
                      if (s.points) patch.points = s.points;
                      await api('PATCH', '/scenes/' + curScene.id + '/layers/' + layerId + '/shapes/' + curShapeId, patch);
                    }
                    dragMode = null; dragShape = null; showShapeProps();
                  } else if (dragMode === 'place' && newShape) {
                    const layer = activeLayer();
                    if (!layer) return;
                    // Remove placeholder
                    layer.shapes = layer.shapes.filter(s => s.id !== '_new');
                    // Commit to server
                    const fillC   = document.getElementById('fillColor').value;
                    const strokeC = document.getElementById('strokeColor').value;
                    newShape.color = newShape.type === 'text' ? strokeC : fillC;
                    newShape.fill  = !['line','arrow','text','arc'].includes(newShape.type);
                    updateShapeSize(newShape, dragStart, x, y);
                    const r  = await api('POST', '/scenes/' + curScene.id + '/layers/' + curLayerId + '/shapes',
                                         { shape: newShape, name: tool });
                    const ss = JSON.parse(r.shape);
                    layer.shapes.push(ss);
                    curShapeId = ss.id;
                    dragMode = null; newShape = null;
                    setTool('arrow');
                    draw(); showShapeProps();
                  }
                }

                function makeNewShape(t, x, y) {
                  switch (t) {
                    case 'rect':     return { type:'rect',     x, y, w:80, h:50 };
                    case 'ellipse':  return { type:'ellipse',  x, y, w:80, h:50 };
                    case 'circle':   return { type:'circle',   x, y, r:40 };
                    case 'line':     return { type:'line',     x, y, x2:x+80, y2:y };
                    case 'text':     return { type:'text',     x, y, text:'Text', font_size:16 };
                    case 'triangle': return { type:'triangle', x, y, w:80, h:60 };
                    case 'arc':      return { type:'arc',      x, y, w:80, h:80, start_angle:0, sweep_angle:90 };
                    default:         return { type:'rect',     x, y, w:80, h:50 };
                  }
                }

                function updateShapeSize(s, start, ex, ey) {
                  const dx = ex - start.x, dy = ey - start.y;
                  switch (s.type) {
                    case 'rect': case 'ellipse': case 'triangle': case 'arc':
                      s.w = Math.max(4, Math.abs(dx)); s.h = Math.max(4, Math.abs(dy));
                      if (dx < 0) s.x = start.x + dx;
                      if (dy < 0) s.y = start.y + dy;
                      break;
                    case 'circle': s.r = Math.max(4, Math.sqrt(dx*dx + dy*dy)); break;
                    case 'line': case 'arrow': s.x2 = ex; s.y2 = ey; break;
                  }
                }

                // ── Tool buttons ──────────────────────────────────────────────
                function setTool(t) {
                  tool = t;
                  document.querySelectorAll('#toolbar button.sm').forEach(b => b.classList.remove('on'));
                  const btn = document.getElementById('tool' + t.charAt(0).toUpperCase() + t.slice(1));
                  if (btn) btn.classList.add('on');
                }

                // ── Properties panel ──────────────────────────────────────────
                function showShapeProps() {
                  const el  = document.getElementById('propsContent');
                  const sel = curShape();
                  if (!sel) { el.innerHTML = '<p style="font-size:.78em;color:#555">Select a shape</p>'; return; }
                  document.getElementById('sSelected').textContent = 'selected: ' + (sel.type || '?');
                  el.innerHTML = `
                    <h4>${sel.type || 'shape'}</h4>
                    <div class="prop"><label>x</label><input id="px" type="number" value="${sel.x??''}" onchange="patchProp('x',+this.value)"></div>
                    <div class="prop"><label>y</label><input id="py" type="number" value="${sel.y??''}" onchange="patchProp('y',+this.value)"></div>
                    ${sel.w!==undefined ? '<div class="prop"><label>w</label><input type="number" value="'+sel.w+'" onchange="patchProp(\'w\',+this.value)"></div>' : ''}
                    ${sel.h!==undefined ? '<div class="prop"><label>h</label><input type="number" value="'+sel.h+'" onchange="patchProp(\'h\',+this.value)"></div>' : ''}
                    ${sel.r!==undefined ? '<div class="prop"><label>r</label><input type="number" value="'+sel.r+'" onchange="patchProp(\'r\',+this.value)"></div>' : ''}
                    ${sel.x2!==undefined ? '<div class="prop"><label>x2</label><input type="number" value="'+sel.x2+'" onchange="patchProp(\'x2\',+this.value)"></div>' : ''}
                    ${sel.y2!==undefined ? '<div class="prop"><label>y2</label><input type="number" value="'+sel.y2+'" onchange="patchProp(\'y2\',+this.value)"></div>' : ''}
                    ${sel.type==='arc' ? '<div class="prop"><label>start°</label><input type="number" value="'+(sel.start_angle??0)+'" onchange="patchProp(\'start_angle\',+this.value)"></div>' : ''}
                    ${sel.type==='arc' ? '<div class="prop"><label>sweep°</label><input type="number" value="'+(sel.sweep_angle??90)+'" onchange="patchProp(\'sweep_angle\',+this.value)"></div>' : ''}
                    <div class="prop"><label>rotate°</label><input type="number" value="${sel.rotation??0}" onchange="patchProp('rotation',+this.value)"></div>`;
                }

                async function patchProp(field, val) {
                  const ss = curShapeId ? findShape(curShapeId) : null;
                  if (!ss) return;
                  ss.shape[field] = val;
                  draw();
                  await api('PATCH', '/scenes/' + curScene.id + '/layers/' + ss.layer.id + '/shapes/' + curShapeId, { [field]: val });
                }

                // ── Keyboard ──────────────────────────────────────────────────
                document.addEventListener('keydown', ev => {
                  if (ev.target.tagName === 'INPUT') return;
                  if (ev.key === 'Delete' || ev.key === 'Backspace') { ev.preventDefault(); deleteSelected(); }
                  if (ev.key === 'v' || ev.key === 'V') setTool('arrow');
                  if (ev.key === 'r' || ev.key === 'R') setTool('rect');
                  if (ev.key === 'e' || ev.key === 'E') setTool('ellipse');
                  if (ev.key === 'c' || ev.key === 'C') setTool('circle');
                  if (ev.key === 'l' || ev.key === 'L') setTool('line');
                  if (ev.key === 't' || ev.key === 'T') setTool('text');
                  if (ev.key === 'g' || ev.key === 'G') setTool('triangle');
                  if (ev.key === 'a' || ev.key === 'A') setTool('arc');
                  if (ev.key === 'Escape') { curShapeId = null; draw(); showShapeProps(); }
                });

                async function deleteSelected() {
                  if (!curScene || !curShapeId) return;
                  const ss = findShape(curShapeId);
                  if (!ss) return;
                  ss.layer.shapes = ss.layer.shapes.filter(s => s.id !== curShapeId);
                  await api('DELETE', '/scenes/' + curScene.id + '/layers/' + ss.layer.id + '/shapes/' + curShapeId);
                  curShapeId = null; draw(); showShapeProps();
                }

                // ── API helper ────────────────────────────────────────────────
                async function api(method, path, body) {
                  const opts = { method };
                  if (body !== undefined) {
                    opts.headers = { 'Content-Type': 'application/json' };
                    opts.body    = JSON.stringify(body);
                  }
                  const res = await fetch(path, opts);
                  const r   = await res.json();
                  if (!r.success) console.error(r.error, path);
                  return r.data || {};
                }

                function esc(s) {
                  return String(s).replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/"/g,'&quot;');
                }
                </script>
                </body>
                </html>
                """;

            byte[] buf = Encoding.UTF8.GetBytes(html);
            res.ContentType     = "text/html; charset=utf-8";
            res.ContentLength64 = buf.Length;
            res.StatusCode      = 200;
            try   { await res.OutputStream.WriteAsync(buf); }
            finally { res.Close(); }
        }

        // ── New route handlers ────────────────────────────────────────────

        private ApexResult HandleDrawDemo(HttpListenerRequest req)
        {
            bool overlay = string.Equals(req.QueryString["overlay"], "true",
                               StringComparison.OrdinalIgnoreCase);
            int overlayMs = int.TryParse(req.QueryString["ms"], out int ms) ? ms : 6000;

            var scene = AIDrawingCommand.BuildSpaceScene();
            scene.Overlay   = overlay;
            scene.OverlayMs = overlayMs;

            string base64 = AIDrawingCommand.Render(scene);

            if (overlay)
                Application.OpenForms[0]?.BeginInvoke(() => AIDrawingCommand.ShowOverlay(scene));

            string msg = $"Space scene rendered ({scene.Shapes.Count} shapes)." +
                         (overlay ? $" Overlay showing for {overlayMs / 1000.0:0.#}s (Esc to dismiss)." : "");
            return new ApexResult
            {
                Success = true,
                Action  = "draw/demo",
                Data    = new Dictionary<string, string> { ["result"] = base64, ["message"] = msg }
            };
        }

        // ── /health ───────────────────────────────────────────────────────

        private ApexResult HandleHealth()
        {
            var up = DateTime.UtcNow - _startTime;
            return new ApexResult
            {
                Success = true,
                Action  = "health",
                Data    = new Dictionary<string, string>
                {
                    ["status"]           = "ok",
                    ["uptime"]           = $"{(int)up.TotalHours:D2}:{up.Minutes:D2}:{up.Seconds:D2}",
                    ["model_loaded"]     = _processor.IsModelLoaded.ToString(),
                    ["model_processing"] = _processor.IsProcessing.ToString(),
                    ["active_requests"]  = Volatile.Read(ref _activeRequests).ToString(),
                    ["total_requests"]   = Volatile.Read(ref _totalRequests).ToString(),
                    ["error_requests"]   = Volatile.Read(ref _errorRequests).ToString(),
                }
            };
        }

        // ── /metrics ──────────────────────────────────────────────────────

        private ApexResult HandleMetrics()
        {
            var routes = _routeCounts.Keys.OrderByDescending(k => _routeCounts[k])
                .ToDictionary(
                    k => k,
                    k => new
                    {
                        count      = _routeCounts[k],
                        last_ms    = _routeLastLatencyMs.TryGetValue(k, out double ms) ? Math.Round(ms, 1) : 0.0
                    });

            return new ApexResult
            {
                Success = true,
                Action  = "metrics",
                Data    = new Dictionary<string, string>
                {
                    ["total_requests"]   = Volatile.Read(ref _totalRequests).ToString(),
                    ["error_requests"]   = Volatile.Read(ref _errorRequests).ToString(),
                    ["active_requests"]  = Volatile.Read(ref _activeRequests).ToString(),
                    ["routes"]           = JsonSerializer.Serialize(routes)
                }
            };
        }

        private static ApexResult HandlePing() => new()
        {
            Success = true,
            Action  = "ping",
            Data    = new Dictionary<string, string>
            {
                ["status"]    = "ok",
                ["timestamp"] = DateTimeOffset.UtcNow.ToString("O")
            }
        };

        private static ApexResult HandleSysinfo() => new()
        {
            Success = true,
            Action  = "sysinfo",
            Data    = new Dictionary<string, string>
            {
                ["os"]        = Environment.OSVersion.ToString(),
                ["machine"]   = Environment.MachineName,
                ["user"]      = Environment.UserName,
                ["domain"]    = Environment.UserDomainName,
                ["cpu_count"] = Environment.ProcessorCount.ToString(),
                ["clr"]       = Environment.Version.ToString(),
                ["is64bit"]   = Environment.Is64BitOperatingSystem.ToString(),
                ["cwd"]       = Environment.CurrentDirectory
            }
        };

        private static ApexResult HandleEnv()
        {
            var data = new Dictionary<string, string>();
            foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
                data[entry.Key?.ToString() ?? ""] = entry.Value?.ToString() ?? "";
            return new ApexResult { Success = true, Action = "env", Data = data };
        }

        private static ApexResult HandleLs(string? requestedPath)
        {
            string dir;
            try
            {
                dir = string.IsNullOrWhiteSpace(requestedPath)
                    ? Environment.CurrentDirectory
                    : Path.GetFullPath(requestedPath);   // canonicalize to prevent traversal
            }
            catch
            {
                return new ApexResult { Success = false, Action = "ls", Error = "Invalid path." };
            }

            if (!Directory.Exists(dir))
                return new ApexResult { Success = false, Action = "ls",
                    Error = $"Directory not found: {dir}" };

            var entries = new List<string>();
            foreach (string d in Directory.EnumerateDirectories(dir))
                entries.Add(Path.GetFileName(d) + "/");
            foreach (string f in Directory.EnumerateFiles(dir))
                entries.Add(Path.GetFileName(f));

            return new ApexResult
            {
                Success = true,
                Action  = "ls",
                Data    = new Dictionary<string, string>
                {
                    ["path"]    = dir,
                    ["entries"] = string.Join("\n", entries)
                }
            };
        }

        private static async Task<ApexResult> HandleRunAsync(string? cmd)
        {
            if (string.IsNullOrWhiteSpace(cmd))
                return new ApexResult { Success = false, Action = "run",
                    Error = "cmd parameter is required" };

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var psi = new ProcessStartInfo
            {
                FileName               = "cmd.exe",
                Arguments              = $"/c {cmd}",
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute        = false,
                CreateNoWindow         = true
            };

            using var proc = new Process { StartInfo = psi };
            proc.Start();

            string stdout = await proc.StandardOutput.ReadToEndAsync();
            string stderr = await proc.StandardError.ReadToEndAsync();
            try   { await proc.WaitForExitAsync(cts.Token); }
            catch (OperationCanceledException)
            {
                proc.Kill();
                return new ApexResult { Success = false, Action = "run",
                    Error = "Process timed out after 30 seconds" };
            }

            int exit = proc.ExitCode;
            return new ApexResult
            {
                Success = exit == 0,
                Action  = "run",
                Data    = new Dictionary<string, string>
                {
                    ["cmd"]       = cmd!,
                    ["stdout"]    = stdout,
                    ["stderr"]    = stderr,
                    ["exit_code"] = exit.ToString()
                },
                Error = exit == 0 ? null : $"Process exited with code {exit}"
            };
        }

        // ── Test page ─────────────────────────────────────────────────────

        private static async Task ServeTestPage(HttpListenerResponse res)
        {
            const string html = """
                <!DOCTYPE html>
                <html lang="en">
                <head>
                <meta charset="utf-8">
                <title>ApexComputerUse</title>
                <style>
                  *, *::before, *::after { box-sizing: border-box; margin: 0; padding: 0; }
                  body { font-family: monospace; font-size: 13px; background: #1e1e1e; color: #d4d4d4;
                         display: flex; flex-direction: column; height: 100vh; overflow: hidden; }

                  /* ── Header ── */
                  header { background: #252526; border-bottom: 1px solid #3c3c3c;
                           padding: .35em 1em; display: flex; align-items: center; gap: .75em;
                           flex-shrink: 0; }
                  #dot { width: 8px; height: 8px; border-radius: 50%; background: #555; flex-shrink: 0; }
                  #dot.ok  { background: #4ec94e; }
                  #dot.err { background: #e05252; }
                  #statusTxt { font-size: .78em; color: #888; }
                  header span.spacer { flex: 1; }
                  header span.brand  { font-size: .82em; color: #9cdcfe; }

                  /* ── Main 2-col layout ── */
                  main { display: grid; grid-template-columns: 270px 1fr; flex: 1; overflow: hidden; min-height: 0; }

                  /* ── Left sidebar ── */
                  aside { background: #252526; border-right: 1px solid #3c3c3c;
                          display: flex; flex-direction: column; overflow: hidden; min-height: 0; }

                  .panel { display: flex; flex-direction: column; overflow: hidden; border-bottom: 1px solid #3c3c3c; }
                  .panel.wins  { flex: 0 0 200px; }
                  .panel.elems { flex: 1; min-height: 0; }

                  .phead { padding: .35em .6em; font-size: .7em; color: #888; text-transform: uppercase;
                           letter-spacing: .06em; background: #2d2d30; display: flex; align-items: center; flex-shrink: 0; }
                  .phead span { flex: 1; }
                  .phead button { background: none; border: none; color: #888; cursor: pointer;
                                  padding: 0 .2em; font-size: 1.1em; line-height: 1; }
                  .phead button:hover { color: #d4d4d4; }

                  select.lst { flex: 1; background: #1e1e1e; color: #d4d4d4; border: none; outline: none;
                               font: inherit; font-size: .82em; overflow-y: auto; min-height: 0;
                               width: 100%; padding: .1em 0; }
                  select.lst option { padding: .15em .6em; cursor: pointer; }
                  select.lst option:hover { background: #2a2d2e; }
                  select.lst option:checked { background: #094771; color: #fff; }

                  .frow { display: flex; gap: .3em; padding: .3em .5em; background: #252526;
                          border-bottom: 1px solid #3c3c3c; flex-shrink: 0; align-items: center; }
                  .frow input[type=checkbox] { accent-color: #0e639c; }
                  .frow label { font-size: .75em; color: #9cdcfe; display: flex; align-items: center; gap: .2em; white-space: nowrap; }
                  .frow select { flex: 1; background: #3c3c3c; color: #d4d4d4; border: 1px solid #555;
                                 border-radius: 2px; padding: .15em .3em; font: inherit; font-size: .76em; }

                  /* ── Right panel ── */
                  section { display: flex; flex-direction: column; overflow: hidden; min-height: 0; }

                  #selInfo { padding: .3em .8em; font-size: .75em; color: #9cdcfe; background: #252526;
                             border-bottom: 1px solid #3c3c3c; white-space: nowrap; overflow: hidden;
                             text-overflow: ellipsis; flex-shrink: 0; min-height: 1.8em; }

                  /* ── Command builder ── */
                  .cmdbuild { padding: .7em .9em; border-bottom: 1px solid #3c3c3c; flex-shrink: 0; }
                  .cmdbuild h3 { font-size: .68em; color: #666; text-transform: uppercase;
                                 letter-spacing: .06em; margin-bottom: .55em; }

                  .groups { display: flex; flex-wrap: wrap; gap: .35em .6em; margin-bottom: .6em; }
                  .grp { display: flex; align-items: baseline; gap: .2em; flex-wrap: wrap; }
                  .glabel { font-size: .68em; color: #666; padding-right: .1em; white-space: nowrap; }

                  button.a { background: #2d2d30; color: #bbb; border: 1px solid #444;
                             border-radius: 3px; padding: .2em .5em; cursor: pointer;
                             font: inherit; font-size: .76em; white-space: nowrap; }
                  button.a:hover   { background: #3e3e42; border-color: #777; color: #d4d4d4; }
                  button.a.on      { background: #094771; border-color: #1177bb; color: #fff; }
                  button.a.cap     { background: #2d3b2d; border-color: #3a5c3a; color: #8ac88a; }
                  button.a.cap:hover { background: #3a4e3a; }
                  button.a.ai      { background: #2d2840; border-color: #5a4a8a; color: #b08ae0; }
                  button.a.ai:hover  { background: #3a3460; }

                  /* ── Format bar ── */
                  .fmt-bar { display: flex; gap: .5em; align-items: center; }
                  .fmt-bar label { font-size: .75em; color: #888; }
                  .fmt-bar select { background: #3c3c3c; color: #d4d4d4; border: 1px solid #555;
                                    border-radius: 2px; padding: .15em .4em; font: inherit; font-size: .76em; }
                  .fmt-bar a { font-size: .72em; color: #4ec9b0; text-decoration: none; padding: .1em .4em;
                               border: 1px solid #3c3c3c; border-radius: 2px; }
                  .fmt-bar a:hover { border-color: #4ec9b0; }

                  .vrow { display: flex; gap: .5em; align-items: flex-start; }
                  .vrow label { font-size: .76em; color: #888; white-space: nowrap; padding-top: .35em; }
                  .vrow textarea { flex: 1; background: #1e1e1e; color: #d4d4d4; border: 1px solid #3c3c3c;
                                   border-radius: 3px; padding: .28em .5em; font: inherit; font-size: .85em;
                                   resize: vertical; min-height: 2.2em; max-height: 14em;
                                   overflow-y: auto; white-space: pre; font-family: 'Consolas', monospace; }
                  .vrow textarea:focus { outline: 1px solid #0e639c; }
                  button#go { background: #0e639c; color: #fff; border: 1px solid #1177bb;
                              border-radius: 3px; padding: .28em .85em; cursor: pointer;
                              font: inherit; font-size: .85em; white-space: nowrap; }
                  button#go:hover { background: #1177bb; }

                  /* ── Output ── */
                  #out { flex: 1; overflow-y: auto; padding: .6em .9em; min-height: 0; }

                  .msg { border-left: 3px solid #555; background: #252526; border-radius: 2px;
                         padding: .45em .65em; margin-bottom: .45em; }
                  .msg.ok  { border-color: #4ec94e; }
                  .msg.err { border-color: #e05252; }
                  .msg .ts { font-size: .7em; color: #666; margin-bottom: .2em; }
                  .msg pre { white-space: pre-wrap; word-break: break-all; font: inherit; font-size: .82em; }
                  .msg img { max-width: 100%; margin-top: .4em; border: 1px solid #3c3c3c;
                             border-radius: 2px; display: block; cursor: zoom-in; }
                  .msg img.zoomed { max-width: none; width: 100%; cursor: zoom-out; }
                </style>
                </head>
                <body>

                <header>
                  <span id="dot"></span>
                  <span id="statusTxt">connecting…</span>
                  <span class="spacer"></span>
                  <label style="font-size:.75em;color:#9cdcfe;">API Key
                    <input id="apiKeyBox" type="password" placeholder="paste key…" autocomplete="off"
                           style="background:#3c3c3c;color:#d4d4d4;border:1px solid #555;border-radius:2px;
                                  padding:.15em .3em;font:inherit;font-size:.76em;width:140px;margin-left:.3em;">
                    <button type="button" id="apiKeyShow" title="Show / hide"
                            style="background:none;border:none;color:#888;cursor:pointer;font-size:1em;padding:0 .2em;">👁</button>
                  </label>
                  <span class="fmt-bar">
                    <label>Format</label>
                    <select id="fmtSel" onchange="onFmtChange()">
                      <option value="json" selected>JSON</option>
                      <option value="html">HTML</option>
                      <option value="text">Text</option>
                      <option value="pdf">PDF</option>
                    </select>
                    <a id="lnkHelp"    href="/help.json"    target="_blank">help</a>
                    <a id="lnkStatus"  href="/status.json"  target="_blank">status</a>
                    <a id="lnkWindows" href="/windows.json" target="_blank">windows</a>
                    <a href="/editor" target="_blank">scene editor</a>
                  </span>
                  <select id="runModeSel" title="TestRunner mode"
                          style="background:#3c3c3c;color:#d4d4d4;border:1px solid #555;border-radius:2px;
                                 padding:.15em .3em;font:inherit;font-size:.76em;">
                    <option value="demo" selected>demo</option>
                    <option value="benchmark">benchmark</option>
                  </select>
                  <button type="button" id="runTestsBtn"
                          style="background:#0e639c;color:#fff;border:none;border-radius:2px;
                                 padding:.2em .7em;font:inherit;font-size:.76em;cursor:pointer;">Run Tests</button>
                  <span class="brand">ApexComputerUse</span>
                </header>

                <main>
                  <!-- ── Left sidebar ── -->
                  <aside>
                    <div class="panel wins">
                      <div class="phead">
                        <span>Windows</span>
                        <button onclick="loadWindows()" title="Refresh">↺</button>
                      </div>
                      <select class="lst" id="winList" size="8" onchange="onWinSelect()"></select>
                    </div>

                    <div class="panel elems">
                      <div class="phead">
                        <span>Elements</span>
                        <button onclick="loadElements()" title="Refresh">↺</button>
                      </div>
                      <div class="frow">
                        <label>
                          <input type="checkbox" id="onscreen" checked onchange="loadElements()">
                          Onscreen
                        </label>
                        <select id="typeFilter" onchange="loadElements()">
                          <option value="">All types</option>
                          <option>Button</option><option>Edit</option><option>Text</option>
                          <option>ComboBox</option><option>List</option><option>ListItem</option>
                          <option>CheckBox</option><option>RadioButton</option>
                          <option>MenuItem</option><option>Menu</option>
                          <option>TabItem</option><option>Tab</option>
                          <option>TreeItem</option><option>Tree</option>
                          <option>DataItem</option><option>DataGrid</option>
                          <option>Slider</option><option>ProgressBar</option>
                          <option>Hyperlink</option><option>Image</option>
                          <option>Window</option><option>Pane</option>
                          <option>Group</option><option>Document</option>
                        </select>
                      </div>
                      <select class="lst" id="elemList" size="20" onchange="onElemSelect()"></select>
                    </div>
                  </aside>

                  <!-- ── Right panel ── -->
                  <section>
                    <div id="selInfo">No element selected — pick a window to start</div>

                    <div class="cmdbuild">
                      <h3>Command Builder</h3>
                      <div class="groups">

                        <div class="grp">
                          <span class="glabel">Click</span>
                          <button class="a" onclick="act('click')">click</button>
                          <button class="a" onclick="act('right-click')">right</button>
                          <button class="a" onclick="act('double-click')">double</button>
                          <button class="a" onclick="act('middle-click')">middle</button>
                          <button class="a" onclick="act('hover')">hover</button>
                          <button class="a" onclick="act('highlight')">highlight</button>
                          <button class="a" onclick="act('invoke')">invoke</button>
                          <button class="a" onclick="act('click-at')">click-at</button>
                          <button class="a" onclick="act('drag')">drag</button>
                        </div>

                        <div class="grp">
                          <span class="glabel">Text</span>
                          <button class="a" onclick="act('type')">type</button>
                          <button class="a" onclick="act('gettext')">gettext</button>
                          <button class="a" onclick="act('getvalue')">getvalue</button>
                          <button class="a" onclick="act('setvalue')">setvalue</button>
                          <button class="a" onclick="act('appendvalue')">append</button>
                          <button class="a" onclick="act('insert')">insert</button>
                          <button class="a" onclick="act('clearvalue')">clearval</button>
                          <button class="a" onclick="act('clear')">clear</button>
                          <button class="a" onclick="act('getselectedtext')">getsel</button>
                        </div>

                        <div class="grp">
                          <span class="glabel">Keys</span>
                          <button class="a" onclick="act('keys')">keys</button>
                          <button class="a" onclick="act('selectall')">sel-all</button>
                          <button class="a" onclick="act('copy')">copy</button>
                          <button class="a" onclick="act('cut')">cut</button>
                          <button class="a" onclick="act('paste')">paste</button>
                          <button class="a" onclick="act('undo')">undo</button>
                        </div>

                        <div class="grp">
                          <span class="glabel">State</span>
                          <button class="a" onclick="act('describe')">describe</button>
                          <button class="a" onclick="act('patterns')">patterns</button>
                          <button class="a" onclick="act('bounds')">bounds</button>
                          <button class="a" onclick="act('isenabled')">enabled?</button>
                          <button class="a" onclick="act('isvisible')">visible?</button>
                          <button class="a" onclick="act('focus')">focus</button>
                        </div>

                        <div class="grp">
                          <span class="glabel">Scroll</span>
                          <button class="a" onclick="act('scroll-up')">↑</button>
                          <button class="a" onclick="act('scroll-down')">↓</button>
                          <button class="a" onclick="act('scroll-left')">←</button>
                          <button class="a" onclick="act('scroll-right')">→</button>
                          <button class="a" onclick="act('scrollinto')">into view</button>
                          <button class="a" onclick="act('scrollpercent')">percent</button>
                          <button class="a" onclick="act('getscrollinfo')">info</button>
                        </div>

                        <div class="grp">
                          <span class="glabel">Toggle</span>
                          <button class="a" onclick="act('toggle')">toggle</button>
                          <button class="a" onclick="act('toggle-on')">on</button>
                          <button class="a" onclick="act('toggle-off')">off</button>
                          <button class="a" onclick="act('gettoggle')">state</button>
                          <button class="a" onclick="act('expand')">expand</button>
                          <button class="a" onclick="act('collapse')">collapse</button>
                          <button class="a" onclick="act('expandstate')">expstate</button>
                        </div>

                        <div class="grp">
                          <span class="glabel">Select</span>
                          <button class="a" onclick="act('select')">select</button>
                          <button class="a" onclick="act('select-index')">by-idx</button>
                          <button class="a" onclick="act('getitems')">getitems</button>
                          <button class="a" onclick="act('getselecteditem')">selected</button>
                          <button class="a" onclick="act('getselection')">getsel</button>
                          <button class="a" onclick="act('select-item')">sel-item</button>
                          <button class="a" onclick="act('addselect')">addsel</button>
                          <button class="a" onclick="act('removeselect')">rmsel</button>
                          <button class="a" onclick="act('isselected')">isselected</button>
                        </div>

                        <div class="grp">
                          <span class="glabel">Window</span>
                          <button class="a" onclick="act('minimize')">minimize</button>
                          <button class="a" onclick="act('maximize')">maximize</button>
                          <button class="a" onclick="act('restore')">restore</button>
                          <button class="a" onclick="act('windowstate')">state</button>
                        </div>

                        <div class="grp">
                          <span class="glabel">Range</span>
                          <button class="a" onclick="act('setrange')">set</button>
                          <button class="a" onclick="act('getrange')">get</button>
                          <button class="a" onclick="act('rangeinfo')">info</button>
                        </div>

                        <div class="grp">
                          <span class="glabel">Grid</span>
                          <button class="a" onclick="act('griditem')">item</button>
                          <button class="a" onclick="act('gridinfo')">info</button>
                          <button class="a" onclick="act('griditeminfo')">iteminfo</button>
                        </div>

                        <div class="grp">
                          <span class="glabel">Transform</span>
                          <button class="a" onclick="act('move')">move</button>
                          <button class="a" onclick="act('resize')">resize</button>
                        </div>

                        <div class="grp">
                          <span class="glabel">Wait</span>
                          <button class="a" onclick="act('wait')">wait</button>
                        </div>

                        <div class="grp">
                          <span class="glabel">Capture</span>
                          <button class="a cap" onclick="doCapture('element')">element</button>
                          <button class="a cap" onclick="doCapture('window')">window</button>
                          <button class="a cap" onclick="doCapture('screen')">screen</button>
                          <button class="a cap" onclick="doOcr()">OCR</button>
                          <button class="a cap" onclick="doUiMap()">UI map</button>
                          <button class="a cap" onclick="doDraw()" title="POST /draw — Value field must be a JSON DrawRequest (shapes array). Canvas: blank|white|black|screen|window|element">draw</button>
                          <button class="a cap" onclick="doDrawDemo()" title="GET /draw/demo — renders the built-in space scene. Add ?overlay=true to also show on screen.">demo</button>
                        </div>

                        <div class="grp">
                          <span class="glabel">AI Vision</span>
                          <button class="a ai" onclick="doAiStatus()" title="Check if vision model is loaded">status</button>
                          <button class="a ai" onclick="doAiDescribe()" title="Capture element and describe it with the vision model (Value = optional prompt)">describe</button>
                          <button class="a ai" onclick="doAiAsk()" title="Ask the vision model a question about the current element (Value = prompt)">ask</button>
                          <button class="a ai" onclick="doAiFile()" title="Send an image or audio file to the vision model (Value = file path)">file</button>
                        </div>

                      </div>

                      <div class="vrow">
                        <label>Value</label>
                        <textarea id="val" rows="1" placeholder="(optional — text, x,y, JSON, …)  Ctrl+Enter to execute"
                                  onkeydown="if(event.key==='Enter'&&(event.ctrlKey||event.metaKey)){event.preventDefault();doExec();}"
                                  oninput="this.rows=Math.min(12,Math.max(1,this.value.split('\n').length))"></textarea>
                        <button id="go" onclick="doExec()">▶ Execute</button>
                      </div>
                    </div>

                    <div id="out"></div>
                  </section>
                </main>

                <script>
                'use strict';
                let curAction = '';
                let curWin    = '';
                let curElemId = null;

                // ── Boot ────────────────────────────────────────────────────
                (async () => {
                  try {
                    const r = await call('GET', '/ping');
                    setDot(true);
                    document.getElementById('statusTxt').textContent =
                      'ok \u2014 ' + (r.data && r.data.timestamp ? r.data.timestamp : '');
                  } catch (e) {
                    setDot(false);
                    document.getElementById('statusTxt').textContent = 'server unreachable';
                  }
                  await loadWindows();
                })();

                function setDot(ok) {
                  document.getElementById('dot').className = ok ? 'ok' : 'err';
                }

                // ── Windows ─────────────────────────────────────────────────
                async function loadWindows() {
                  try {
                    const r    = await call('GET', '/windows');
                    const raw  = r.data && r.data.result ? r.data.result : '[]';
                    let wins   = [];
                    try { wins = JSON.parse(raw); } catch (_) {}
                    const sel  = document.getElementById('winList');
                    sel.innerHTML = '';
                    wins.forEach(w => {
                      const o = document.createElement('option');
                      o.value          = String(w.id || '');
                      o.textContent    = w.title || String(w);
                      o.dataset.title  = w.title || String(w);
                      o.dataset.id     = String(w.id || '');
                      sel.appendChild(o);
                    });
                    appendLog('windows', true, wins.length + ' window(s) loaded');
                  } catch (e) {
                    appendLog('windows', false, String(e));
                  }
                }

                async function onWinSelect() {
                  const sel = document.getElementById('winList');
                  const o   = sel.options[sel.selectedIndex];
                  if (!o) return;
                  curWin    = o.dataset.title;
                  curElemId = null;
                  updateInfo(null);
                  try {
                    await call('POST', '/find', { window: curWin });
                    await loadElements();
                  } catch (e) {
                    appendLog('find', false, String(e));
                  }
                }

                // ── Elements ────────────────────────────────────────────────
                async function loadElements() {
                  if (!curWin) return;
                  const onscreen = document.getElementById('onscreen').checked;
                  const type     = document.getElementById('typeFilter').value;
                  let url = '/elements?onscreen=' + (onscreen ? 'true' : 'false');
                  if (type) url += '&type=' + encodeURIComponent(type);
                  try {
                    const r   = await call('GET', url);
                    const raw = r.data && r.data.result ? r.data.result : 'null';
                    let root  = null;
                    try { root = JSON.parse(raw); } catch (_) {}
                    const sel = document.getElementById('elemList');
                    sel.innerHTML = '';
                    if (root) flattenInto(root, 0, sel);
                    appendLog('elements', true, sel.options.length + ' element(s) loaded');
                  } catch (e) {
                    appendLog('elements', false, String(e));
                  }
                }

                function flattenInto(node, depth, sel) {
                  if (!node) return;
                  const o     = document.createElement('option');
                  const pad   = '\u00a0\u00a0'.repeat(depth);
                  const parts = [node.controlType];
                  if (node.name)         parts.push('"' + node.name + '"');
                  if (node.automationId) parts.push('#' + node.automationId);
                  o.textContent   = pad + parts.join(' ');
                  o.value         = String(node.id || '');
                  o.dataset.id    = String(node.id || '');
                  o.dataset.name  = node.name || '';
                  o.dataset.type  = node.controlType || '';
                  o.dataset.aid   = node.automationId || '';
                  sel.appendChild(o);
                  if (node.children) node.children.forEach(c => flattenInto(c, depth + 1, sel));
                }

                async function onElemSelect() {
                  const sel = document.getElementById('elemList');
                  const o   = sel.options[sel.selectedIndex];
                  if (!o) return;
                  curElemId = o.dataset.id;
                  updateInfo(o);
                  try {
                    const body = { window: curWin };
                    if (curElemId) body.id = curElemId;
                    else if (o.dataset.name) body.name = o.dataset.name;
                    await call('POST', '/find', body);
                  } catch (e) {
                    appendLog('find', false, String(e));
                  }
                }

                function updateInfo(o) {
                  const el = document.getElementById('selInfo');
                  if (!o) {
                    el.textContent = curWin ? 'Window: ' + curWin : 'No element selected';
                    return;
                  }
                  const parts = [curWin, '[' + o.dataset.type + ']'];
                  if (o.dataset.name) parts.push(o.dataset.name);
                  if (o.dataset.aid)  parts.push('#' + o.dataset.aid);
                  if (curElemId)      parts.push('id:' + curElemId);
                  el.textContent = parts.join('  \u203a  ');
                }

                // ── Command builder ─────────────────────────────────────────
                const VALUE_HINTS = {
                  'type':          'text to type',
                  'keys':          '{CTRL}c or Ctrl+A',
                  'setvalue':      'new value',
                  'appendvalue':   'text to append',
                  'insert':        'text at caret',
                  'select':        'item text',
                  'select-index':  'zero-based index',
                  'click-at':      'x,y offset from element',
                  'drag':          'x,y screen coords',
                  'move':          'x,y',
                  'resize':        'w,h',
                  'scrollpercent': 'h,v (0-100)',
                  'setrange':      'number',
                  'griditem':      'row,col',
                  'wait':          'automationId to wait for',
                  'ai/describe':   'optional prompt (e.g. "list all buttons")',
                  'ai/ask':        'question for the vision model (required)',
                  'ai/file':       'file path (e.g. C:\\path\\to\\image.png)',
                };

                function act(name) {
                  curAction = name;
                  document.querySelectorAll('button.a').forEach(b => b.classList.remove('on'));
                  if (event && event.currentTarget) event.currentTarget.classList.add('on');
                  document.getElementById('go').textContent = '\u25b6 ' + name;
                  const vi = document.getElementById('val');
                  vi.placeholder = VALUE_HINTS[name] || '(optional)';
                }

                async function doExec() {
                  if (!curAction) { appendLog('execute', false, 'No action selected'); return; }
                  const val  = document.getElementById('val').value;
                  const body = { action: curAction };
                  if (val) body.value = val;
                  try {
                    const r = await call('POST', '/execute', body);
                    log(r);
                  } catch (e) {
                    appendLog('execute', false, String(e));
                  }
                }

                // ── Capture & OCR ───────────────────────────────────────────
                async function doCapture(target) {
                  try {
                    const body = target === 'element' ? {} : { action: target };
                    const r    = await call('POST', '/capture', body);
                    logCapture(r, target);
                  } catch (e) {
                    appendLog('capture', false, String(e));
                  }
                }

                async function doOcr() {
                  const val  = document.getElementById('val').value;
                  const body = val ? { value: val } : {};
                  try {
                    const r = await call('POST', '/ocr', body);
                    log(r);
                  } catch (e) {
                    appendLog('ocr', false, String(e));
                  }
                }

                // ── UI Map ──────────────────────────────────────────────────
                async function doUiMap() {
                  appendLog('uimap', true, 'Rendering UI map\u2026');
                  try {
                    const r = await call('GET', '/uimap');
                    logCapture(r, 'uimap');
                  } catch (e) {
                    appendLog('uimap', false, String(e));
                  }
                }

                // ── Draw demo ────────────────────────────────────────────────
                async function doDrawDemo() {
                  appendLog('draw/demo', true, 'Rendering space scene\u2026');
                  try {
                    const overlay = document.getElementById('val').value.trim() === 'overlay';
                    const url     = overlay ? '/draw/demo?overlay=true' : '/draw/demo';
                    const r       = await call('GET', url);
                    logCapture(r, 'draw/demo');
                  } catch (e) {
                    appendLog('draw/demo', false, String(e));
                  }
                }

                // ── Draw ────────────────────────────────────────────────────
                async function doDraw() {
                  const val = document.getElementById('val').value.trim();
                  if (!val) {
                    appendLog('draw', false, 'Enter a JSON DrawRequest in the Value field. Example: {"canvas":"blank","width":400,"height":300,"shapes":[{"type":"circle","x":200,"y":150,"r":80,"color":"royalblue","fill":true},{"type":"text","x":200,"y":140,"text":"Hello!","color":"white","font_size":20,"font_bold":true,"align":"center"}]}');
                    return;
                  }
                  appendLog('draw', true, 'Rendering\u2026');
                  try {
                    // Allow the value to be either a raw JSON object or wrapped in {"value":...}
                    let body;
                    try { const parsed = JSON.parse(val); body = parsed.shapes ? { value: val } : { value: val }; }
                    catch { body = { value: val }; }
                    const r = await call('POST', '/draw', body);
                    logCapture(r, 'draw');
                  } catch (e) {
                    appendLog('draw', false, String(e));
                  }
                }

                // ── AI Vision ───────────────────────────────────────────────
                async function doAiStatus() {
                  try {
                    const r = await call('GET', '/ai/status');
                    log(r);
                  } catch (e) {
                    appendLog('ai/status', false, String(e));
                  }
                }

                async function doAiDescribe() {
                  const prompt = document.getElementById('val').value.trim();
                  const body   = prompt ? { prompt } : {};
                  appendLog('ai/describe', true, 'Running vision model\u2026 (this may take a few seconds)');
                  try {
                    const r = await call('POST', '/ai/describe', body);
                    log(r);
                  } catch (e) {
                    appendLog('ai/describe', false, String(e));
                  }
                }

                async function doAiAsk() {
                  const prompt = document.getElementById('val').value.trim();
                  if (!prompt) { appendLog('ai/ask', false, 'Enter a question in the Value field first'); return; }
                  appendLog('ai/ask', true, 'Running vision model\u2026 (this may take a few seconds)');
                  try {
                    const r = await call('POST', '/ai/ask', { prompt });
                    log(r);
                  } catch (e) {
                    appendLog('ai/ask', false, String(e));
                  }
                }

                async function doAiFile() {
                  const val = document.getElementById('val').value.trim();
                  if (!val) { appendLog('ai/file', false, 'Enter a file path in the Value field first'); return; }
                  appendLog('ai/file', true, 'Sending file to vision model\u2026');
                  try {
                    const r = await call('POST', '/ai/file', { value: val });
                    log(r);
                  } catch (e) {
                    appendLog('ai/file', false, String(e));
                  }
                }

                function logCapture(r, label) {
                  const b64 = r.data && r.data.result;
                  if (b64 && b64.length > 100) {
                    const div = msgDiv(true);
                    const ts  = document.createElement('div');
                    ts.className = 'ts';
                    ts.textContent = now() + ' capture:' + label;
                    const img = document.createElement('img');
                    img.src   = 'data:image/png;base64,' + b64;
                    img.title = 'Click to zoom';
                    img.onclick = () => img.classList.toggle('zoomed');
                    div.appendChild(ts);
                    div.appendChild(img);
                    prepend(div);
                  } else {
                    log(r);
                  }
                }

                // ── Logging ─────────────────────────────────────────────────
                function log(r) {
                  const ok  = r && r.success;
                  const div = msgDiv(ok);
                  const ts  = document.createElement('div');
                  ts.className = 'ts';
                  ts.textContent = now() + ' ' + (r && r.action ? r.action : '');
                  div.appendChild(ts);
                  if (r._isPdf && r.data?.result) {
                    // PDF — show open link (blob URL)
                    const a = document.createElement('a');
                    a.href = r.data.result;
                    a.target = '_blank';
                    a.textContent = '\u{1F4C4} Open PDF';
                    a.style.cssText = 'color:#4ec94e;font-size:.85em;display:block;margin-top:.3em';
                    div.appendChild(a);
                  } else {
                    const pre = document.createElement('pre');
                    pre.textContent = r._isRaw && r.data?.result
                      ? r.data.result
                      : JSON.stringify(r, null, 2);
                    div.appendChild(pre);
                  }
                  prepend(div);
                }

                function appendLog(action, ok, text) {
                  const div = msgDiv(ok);
                  const ts  = document.createElement('div');
                  ts.className = 'ts';
                  ts.textContent = now() + ' ' + action;
                  const pre = document.createElement('pre');
                  pre.textContent = text;
                  div.appendChild(ts);
                  div.appendChild(pre);
                  prepend(div);
                }

                function msgDiv(ok) {
                  const d = document.createElement('div');
                  d.className = 'msg ' + (ok ? 'ok' : 'err');
                  return d;
                }

                function prepend(el) {
                  const out = document.getElementById('out');
                  out.insertBefore(el, out.firstChild);
                }

                function now() {
                  return new Date().toLocaleTimeString();
                }

                // ── API key (persistent via localStorage) ──────────────────
                const API_KEY_STORAGE = 'apex_api_key';
                function getApiKey() { return localStorage.getItem(API_KEY_STORAGE) || ''; }
                function withKey(url) {
                  const k = getApiKey();
                  if (!k) return url;
                  return url + (url.includes('?') ? '&' : '?') + 'apiKey=' + encodeURIComponent(k);
                }
                (function initApiKey() {
                  const box  = document.getElementById('apiKeyBox');
                  const show = document.getElementById('apiKeyShow');
                  if (!box) return;
                  box.value = getApiKey();
                  box.addEventListener('input', () => {
                    localStorage.setItem(API_KEY_STORAGE, box.value.trim());
                    onFmtChange();
                  });
                  show.addEventListener('click', () => {
                    box.type = box.type === 'password' ? 'text' : 'password';
                  });
                  onFmtChange();
                })();

                (function initRunTests() {
                  const btn = document.getElementById('runTestsBtn');
                  const sel = document.getElementById('runModeSel');
                  if (!btn) return;
                  btn.addEventListener('click', async () => {
                    btn.disabled = true;
                    const mode = sel.value;
                    try {
                      const r = await call('POST', '/run-tests?mode=' + encodeURIComponent(mode));
                      const status = document.getElementById('statusTxt');
                      if (r && r.success) {
                        const pid = r.data && r.data.pid ? r.data.pid : '?';
                        status.textContent = `TestRunner PID ${pid} started (${mode}) — bridge will restart mid-run`;
                      } else {
                        status.textContent = 'Run failed: ' + (r && r.error ? r.error : 'unknown error');
                      }
                    } catch (e) {
                      document.getElementById('statusTxt').textContent = 'Run failed: ' + e.message;
                    } finally {
                      btn.disabled = false;
                    }
                  });
                })();

                // ── Format bar ─────────────────────────────────────────────
                function onFmtChange() {
                  const fmt = document.getElementById('fmtSel').value;
                  const ext = fmt === 'json' ? 'json' : fmt;
                  document.getElementById('lnkHelp').href    = withKey('/help.'    + ext);
                  document.getElementById('lnkStatus').href  = withKey('/status.'  + ext);
                  document.getElementById('lnkWindows').href = withKey('/windows.' + ext);
                }

                // ── API ─────────────────────────────────────────────────────
                async function call(method, path, body) {
                  const fmt  = document.getElementById('fmtSel')?.value ?? 'json';
                  const sep  = path.includes('?') ? '&' : '?';
                  const url  = path + sep + 'format=' + fmt;
                  const opts = { method, headers: {} };
                  const key  = getApiKey();
                  if (key) opts.headers['X-Api-Key'] = key;
                  if (body !== undefined && method !== 'GET') {
                    opts.headers['Content-Type'] = 'application/json';
                    opts.body = JSON.stringify(body);
                  }
                  const res = await fetch(url, opts);
                  if (fmt === 'pdf') {
                    const blob = await res.blob();
                    return { success: res.ok, action: path, data: { result: URL.createObjectURL(blob) }, _isPdf: true };
                  }
                  if (fmt === 'html' || fmt === 'text') {
                    const text = await res.text();
                    return { success: res.ok, action: path, data: { result: text }, _isRaw: true };
                  }
                  return res.json();
                }
                </script>
                </body>
                </html>
                """;

            byte[] buf = Encoding.UTF8.GetBytes(html);
            res.ContentType     = "text/html; charset=utf-8";
            res.ContentLength64 = buf.Length;
            res.StatusCode      = 200;
            try   { await res.OutputStream.WriteAsync(buf); }
            finally { res.Close(); }
        }

        // ── JSON parsing ──────────────────────────────────────────────────

        // HttpListenerRequest.QueryString decodes '+' as space (form-encoding), which breaks
        // key combo strings like "Ctrl+A". Parse 'value' from the raw URL to preserve '+'.
        private static string? RawQueryValue(HttpListenerRequest req)
        {
            var raw = req.Url?.Query;
            if (raw == null) return null;
            foreach (var part in raw.TrimStart('?').Split('&'))
            {
                var eq = part.IndexOf('=');
                if (eq < 0) continue;
                if (Uri.UnescapeDataString(part[..eq]) == "value")
                    return Uri.UnescapeDataString(part[(eq + 1)..]);
            }
            return null;
        }

        private static CommandRequest FromQueryString(HttpListenerRequest req, string command, string? action = null)
            => new()
            {
                Command      = command,
                Action       = req.QueryString["action"]       ?? action,
                Value        = RawQueryValue(req),
                Window       = req.QueryString["window"],
                AutomationId = req.QueryString["id"]           ?? req.QueryString["automationId"]
                             ?? req.QueryString["element"],
                ElementName  = req.QueryString["name"]         ?? req.QueryString["elementName"],
                SearchType   = req.QueryString["type"]         ?? req.QueryString["searchType"],
                OnscreenOnly = string.Equals(req.QueryString["onscreen"], "true",
                                   StringComparison.OrdinalIgnoreCase),
                Prompt       = req.QueryString["prompt"],
                ModelPath    = req.QueryString["model"]        ?? req.QueryString["modelPath"],
                MmProjPath   = req.QueryString["proj"]         ?? req.QueryString["mmProjPath"],
                Depth          = int.TryParse(req.QueryString["depth"], out int _d) ? _d : null,
                Match          = req.QueryString["match"],
                CollapseChains = string.Equals(req.QueryString["collapseChains"], "true", StringComparison.OrdinalIgnoreCase),
                IncludePath    = string.Equals(req.QueryString["includePath"],    "true", StringComparison.OrdinalIgnoreCase),
                Properties     = req.QueryString["properties"],
            };

        private static CommandRequest FromJson(string json, string command, string? action = null)
            => CommandRequestJsonMapper.FromJson(json, command, action);

        public void Dispose() => Stop();
    }

    // ── Canonical result type ─────────────────────────────────────────────

    public sealed class ApexResult
    {
        public bool                        Success { get; init; }
        public string                      Action  { get; init; } = "";
        public Dictionary<string, string>? Data    { get; init; }
        public string?                     Error   { get; init; }

        /// <summary>Adapt a legacy CommandResponse into the canonical form.</summary>
        public static ApexResult From(string action, CommandResponse cr)
        {
            Dictionary<string, string>? data = null;
            if (!string.IsNullOrEmpty(cr.Data))
                data = new Dictionary<string, string> { ["result"] = cr.Data };
            if (!string.IsNullOrEmpty(cr.Message))
            {
                data ??= new Dictionary<string, string>();
                data["message"] = cr.Message;
            }
            return new ApexResult
            {
                Success = cr.Success,
                Action  = action,
                Data    = data,
                Error   = cr.Success ? null : cr.Message
            };
        }
    }

    // ── Format adapter ────────────────────────────────────────────────────

    internal static class FormatAdapter
    {
        private static readonly JsonSerializerOptions s_indented = new() { WriteIndented = true };

        public static string Negotiate(HttpListenerRequest req, string? extHint = null)
        {
            // 1. URL file extension takes highest priority
            if (extHint is "pdf")           return "pdf";
            if (extHint is "json")          return "json";
            if (extHint is "html" or "htm") return "html";
            if (extHint is "txt" or "text") return "text";

            // 2. ?format= query parameter
            string? qf = req.QueryString["format"]?.ToLowerInvariant();
            if (qf is "json" or "html" or "text" or "pdf") return qf;

            // 3. Accept header
            string accept = req.Headers["Accept"] ?? "";
            if (accept.Contains("application/pdf",  StringComparison.OrdinalIgnoreCase)) return "pdf";
            if (accept.Contains("text/html",        StringComparison.OrdinalIgnoreCase)) return "html";
            if (accept.Contains("text/plain",       StringComparison.OrdinalIgnoreCase)) return "text";
            if (accept.Contains("application/json", StringComparison.OrdinalIgnoreCase)) return "json";
            return "html"; // default
        }

        public static (byte[] body, string contentType, int statusCode) Render(ApexResult r, string format)
            => format switch
            {
                "json" => AsUtf8(RenderJson(r), "application/json; charset=utf-8", r.Success),
                "text" => AsUtf8(RenderText(r), "text/plain; charset=utf-8",       r.Success),
                "pdf"  => RenderPdf(r),
                _      => AsUtf8(RenderHtml(r), "text/html; charset=utf-8",        r.Success),
            };

        private static (byte[], string, int) AsUtf8(string body, string ct, bool ok)
            => (Encoding.UTF8.GetBytes(body), ct, ok ? 200 : 400);

        private static string RenderJson(ApexResult r) =>
            JsonSerializer.Serialize(
                new { success = r.Success, action = r.Action, data = r.Data, error = r.Error },
                new JsonSerializerOptions { WriteIndented = true });

        private static string RenderText(ApexResult r)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"success: {r.Success}");
            sb.AppendLine($"action:  {r.Action}");
            if (r.Error is not null) sb.AppendLine($"error:   {r.Error}");
            if (r.Data  is not null)
                foreach (var kv in r.Data)
                    sb.AppendLine($"{kv.Key}: {kv.Value}");
            return sb.ToString();
        }

        private static string RenderHtml(ApexResult r)
        {
            string embeddedJson = JsonSerializer.Serialize(
                new { success = r.Success, action = r.Action, data = r.Data, error = r.Error },
                new JsonSerializerOptions { WriteIndented = true });
            // Prevent </script> injection in embedded JSON
            embeddedJson = embeddedJson.Replace("</", @"<\/", StringComparison.Ordinal);

            var sb = new StringBuilder();
            sb.AppendLine($"success: {r.Success}");
            sb.AppendLine($"action:  {r.Action}");
            if (r.Error is not null) sb.AppendLine($"error:   {r.Error}");
            if (r.Data  is not null)
                foreach (var kv in r.Data)
                    sb.AppendLine($"{kv.Key}: {WebUtility.HtmlEncode(kv.Value)}");

            string title  = WebUtility.HtmlEncode(r.Action);
            string color  = r.Success ? "#4ec94e" : "#e05252";
            string preTxt = WebUtility.HtmlEncode(sb.ToString());

            string html = $$"""
                <!DOCTYPE html>
                <html lang="en">
                <head><meta charset="utf-8"><title>{{title}}</title>
                <style>body{font-family:monospace;padding:1em;background:#1e1e1e;color:#d4d4d4}
                h2{color:{{color}}}pre{background:#252526;padding:1em;border-radius:4px;white-space:pre-wrap}</style>
                </head>
                <body>
                <h2>{{title}}</h2>
                <pre>{{preTxt}}</pre>
                <script type="application/json" id="apex-result">
                {{embeddedJson}}
                </script>
                </body></html>
                """;
            return html;
        }

        private static (byte[], string, int) RenderPdf(ApexResult r)
        {
            var lines = new List<string>();
            lines.Add($"Apex  {(r.Success ? "OK" : "ERR")}  {r.Action}");
            lines.Add(new string('-', 64));
            if (r.Error is not null) lines.Add($"error: {r.Error}");
            if (r.Data  is not null)
                foreach (var kv in r.Data)
                {
                    string line = $"{kv.Key}: {kv.Value}";
                    while (line.Length > 90)
                    {
                        lines.Add(line[..90]);
                        line = "  " + line[90..];
                    }
                    lines.Add(line);
                }
            byte[] pdf = PdfWriter.GenerateTextPdf(lines);
            return (pdf, "application/pdf", r.Success ? 200 : 400);
        }
    }

    // ── Minimal raw PDF generator (no external dependencies) ─────────────────

    internal static class PdfWriter
    {
        public static byte[] GenerateTextPdf(List<string> lines)
        {
            const float W  = 595f, H = 842f, M = 50f; // A4, margins
            const float Sz = 9f,   Lh = 12f;
            int lpp = (int)((H - 2 * M) / Lh);        // lines per page

            // Split into pages (at least one, even if empty)
            var pages = new List<List<string>>();
            if (lines.Count == 0)
            {
                pages.Add([]);
            }
            else
            {
                for (int i = 0; i < lines.Count; i += lpp)
                    pages.Add(lines.Skip(i).Take(lpp).ToList());
            }

            // Object IDs: 1=Catalog 2=Pages 3=Font 4..=Page objs (4+n)..=Content streams
            int nPages      = pages.Count;
            int firstPage   = 4;
            int firstStream = firstPage + nPages;

            // Build in Latin-1: 1 char == 1 byte → sb.Length gives exact byte offsets
            var sb      = new StringBuilder();
            var offsets = new List<int>();

            sb.Append("%PDF-1.4\n");

            // obj 1 — Catalog
            offsets.Add(sb.Length);
            sb.Append("1 0 obj<</Type/Catalog/Pages 2 0 R>>endobj\n");

            // obj 2 — Pages
            string kids = string.Join(" ", Enumerable.Range(firstPage, nPages).Select(i => $"{i} 0 R"));
            offsets.Add(sb.Length);
            sb.Append($"2 0 obj<</Type/Pages/Kids[{kids}]/Count {nPages}>>endobj\n");

            // obj 3 — Font (Courier built-in — no embedding needed)
            offsets.Add(sb.Length);
            sb.Append("3 0 obj<</Type/Font/Subtype/Type1/BaseFont/Courier/Encoding/WinAnsiEncoding>>endobj\n");

            // Page objects
            for (int i = 0; i < nPages; i++)
            {
                offsets.Add(sb.Length);
                sb.Append($"{firstPage + i} 0 obj<</Type/Page/Parent 2 0 R" +
                          $"/MediaBox[0 0 {W} {H}]" +
                          $"/Contents {firstStream + i} 0 R" +
                          $"/Resources<</Font<</F1 3 0 R>>>>>>endobj\n");
            }

            // Content streams
            for (int i = 0; i < nPages; i++)
            {
                var cs = new StringBuilder();
                cs.Append($"BT /F1 {Sz} Tf {M} {H - M - Sz} Td {Lh} TL\n");
                foreach (var line in pages[i])
                    cs.Append($"({PdfEscapeString(line)}) Tj T*\n");
                cs.Append("ET\n");
                string stream = cs.ToString();
                int    len    = stream.Length; // Latin-1: chars == bytes

                offsets.Add(sb.Length);
                sb.Append($"{firstStream + i} 0 obj<</Length {len}>>stream\n");
                sb.Append(stream);
                sb.Append("endstream endobj\n");
            }

            // xref table
            int xrefPos   = sb.Length;
            int totalObjs = 3 + nPages + nPages; // catalog + pages + font + page objs + streams
            sb.Append($"xref\n0 {totalObjs + 1}\n");
            sb.Append("0000000000 65535 f \n");
            foreach (var off in offsets)
                sb.Append($"{off:D10} 00000 n \n");

            sb.Append($"trailer<</Size {totalObjs + 1}/Root 1 0 R>>\n");
            sb.Append($"startxref\n{xrefPos}\n%%EOF");

            return Encoding.Latin1.GetBytes(sb.ToString());
        }

        private static string PdfEscapeString(string s)
        {
            var sb = new StringBuilder(s.Length + 4);
            foreach (char c in s)
            {
                if (c == '(' || c == ')' || c == '\\') sb.Append('\\');
                // Replace non-ASCII / non-printable with a space
                sb.Append(c is >= ' ' and < (char)127 ? c : ' ');
            }
            return sb.ToString();
        }
    }

    internal static class JsonElementExtensions
    {
        public static string? Str(this JsonElement el, string name)
        {
            if (!el.TryGetProperty(name, out var p)) return null;
            return p.ValueKind switch
            {
                JsonValueKind.String => p.GetString(),
                JsonValueKind.Number => p.GetRawText(),
                _                   => null
            };
        }

        public static int? Int(this JsonElement el, string name)
        {
            if (!el.TryGetProperty(name, out var p)) return null;
            return p.ValueKind == JsonValueKind.Number && p.TryGetInt32(out int v) ? v : null;
        }

        public static float? Float(this JsonElement el, string name)
        {
            if (!el.TryGetProperty(name, out var p)) return null;
            return p.ValueKind == JsonValueKind.Number ? (float?)p.GetSingle() : null;
        }

        public static bool? Bool(this JsonElement el, string name)
        {
            if (!el.TryGetProperty(name, out var p)) return null;
            return p.ValueKind == JsonValueKind.True  ? true
                 : p.ValueKind == JsonValueKind.False ? false
                 : null;
        }
    }
}
