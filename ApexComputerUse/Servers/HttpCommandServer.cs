using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ApexComputerUse
{
    /// <summary>
    /// Lightweight HTTP server exposing the Apex command set via JSON REST endpoints.
    /// Runs on a background thread; thread-safe via CommandProcessor's lock.
    /// </summary>
    public partial class HttpCommandServer : IDisposable
    {
        private readonly HttpListener        _listener  = new();
        private readonly CommandProcessor    _processor;
        private readonly CommandDispatcher   _dispatcher;
        private readonly EventBroker         _events;
        private readonly SceneStore          _store;
        private readonly AiChatService?      _chatService;
        private readonly ClientStore?        _clientStore;
        private readonly string?             _apiKey;
        private readonly bool                _enableShellRun;
        private readonly bool                _bindAll;
        private readonly string?             _testRunnerExePath;
        private readonly string?             _testRunnerConfigPath;
        private readonly DateTime            _startTime = DateTime.UtcNow;
        private          CancellationTokenSource? _cts;
        private          Task?              _listenTask;
        private          int                _activeRequests;   // Interlocked counter for graceful drain

        // Metrics
        private long _totalRequests;
        private long _errorRequests;
        private readonly ConcurrentDictionary<string, long>   _routeCounts       = new();
        private readonly ConcurrentDictionary<string, double> _routeLastLatencyMs = new();
        private const int MaxRequestBodyBytes = 1 * 1024 * 1024; // 1 MiB request-body cap

        public int    Port      { get; private set; }
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
    /// false (default) -> <c>http://localhost:{port}/</c> (loopback only, safer default).
    /// true -> <c>http://+:{port}/</c> (all interfaces; set APEX_HTTP_BIND_ALL=true or HttpBindAll in appsettings.json).
        public HttpCommandServer(int port, CommandProcessor processor, SceneStore store,
                                 AiChatService? chatService = null,
                                 string? apiKey = null, bool enableShellRun = false,
                                 bool bindAll = false,
                                 string? testRunnerExePath = null,
                                 string? testRunnerConfigPath = null,
                                 ClientStore? clientStore = null)
        {
            Port            = port;
            _processor      = processor;
            _dispatcher     = new CommandDispatcher(processor);
            // Event broker shares stable IDs with /windows by snapshotting through the processor.
            // Idle by default - the poll thread only runs while at least one /events client is connected.
            _events         = new EventBroker(processor.SnapshotDesktopWindows);
            _store          = store;
            _chatService    = chatService;
            _clientStore    = clientStore;
            _apiKey         = string.IsNullOrWhiteSpace(apiKey) ? null : apiKey.Trim();
            _enableShellRun = enableShellRun;
            _bindAll        = bindAll;
            _testRunnerExePath    = string.IsNullOrWhiteSpace(testRunnerExePath)    ? null : testRunnerExePath.Trim();
            _testRunnerConfigPath = string.IsNullOrWhiteSpace(testRunnerConfigPath) ? null : testRunnerConfigPath.Trim();
        }

        // Lifecycle

        public void Start2()
        {
            if (IsRunning) return;
            _listener.Prefixes.Clear();
            string prefix = _bindAll ? $"http://+:{Port}/" : $"http://localhost:{Port}/";
            _listener.Prefixes.Add(prefix);
            _listener.Start();
            IsRunning = true;
            _cts = new CancellationTokenSource();
            _listenTask = Task.Run(() => ListenLoop(_cts.Token));
            string bindDesc = _bindAll ? $"http://+:{Port}/ (all interfaces)" : $"http://localhost:{Port}/";
            OnLog?.Invoke($"HTTP server listening on {bindDesc}");

        }

        public void Start()
        {
            if (IsRunning) return;
            // Try the configured port first, then increment until one binds.
            const int maxTries = 100;
            for (int attempt = 0; attempt < maxTries; attempt++)
            {
                int tryPort = Port + attempt;
                _listener.Prefixes.Clear();
                string prefix = _bindAll ? $"http://+:{tryPort}/" : $"http://localhost:{tryPort}/";
                _listener.Prefixes.Add(prefix);
                try
                {
                    _listener.Start();
                    if (attempt > 0)
                    {
                        OnLog?.Invoke($"Port {Port} in use; bound to {tryPort} instead.");
                        Port = tryPort;
                    }
                    break;
                }
                catch (System.Net.HttpListenerException hex)
                    when (attempt < maxTries - 1 && (hex.ErrorCode == 183 || hex.ErrorCode == 32))
                {
                    // address/port already in use - try the next one
                }
            }
            IsRunning   = true;
            _cts        = new CancellationTokenSource();
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

        // Accept loop

        private async Task ListenLoop(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var ctx = await _listener.GetContextAsync().WaitAsync(ct);
                    _ = Task.Run(async () =>
                    {
                        Interlocked.Increment(ref _activeRequests);
                        try   { await HandleAsync(ctx); }
                        finally { Interlocked.Decrement(ref _activeRequests); }
                    });
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex) when (!ct.IsCancellationRequested)
                {
                    OnLog?.Invoke($"HTTP listener error: {ex.Message}");
                }
            }
        }

        // Request handler 
        // Authentication
        
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
                return KeyEquals(authHeader[7..].Trim(), _apiKey);

            // X-Api-Key: <key>
            string? apiKeyHeader = req.Headers["X-Api-Key"];
            if (apiKeyHeader != null)
                return KeyEquals(apiKeyHeader.Trim(), _apiKey);

            // ?apiKey=<key>  (convenient for browser / curl testing)
            string? queryKey = req.QueryString["apiKey"];
            if (queryKey != null)
                return KeyEquals(queryKey.Trim(), _apiKey);

            return false;
        }

        private static bool KeyEquals(string supplied, string expected)
        {
            var a = Encoding.UTF8.GetBytes(supplied);
            var b = Encoding.UTF8.GetBytes(expected);
            return CryptographicOperations.FixedTimeEquals(a, b);
        }

        // Per-client permissions

        private static readonly ClientPermissions _fullPermissions = new()
        {
            AllowAutomation = true, AllowCapture = true, AllowAi    = true,
            AllowScenes     = true, AllowShellRun = true, AllowClients = true,
            AllowDiagnostics = true
        };

        private static readonly ClientPermissions _noPermissions = new()
        {
            AllowAutomation = false, AllowCapture = false, AllowAi    = false,
            AllowScenes     = false, AllowShellRun = false, AllowClients = false,
            AllowDiagnostics = false
        };

        private ClientPermissions ResolvePermissions(HttpListenerRequest req)
        {
            // Loopback (localhost / 127.0.0.1 / ::1) always gets full access.
            var addr = req.RemoteEndPoint?.Address;
            if (addr == null || System.Net.IPAddress.IsLoopback(addr)) return _fullPermissions;

            // Match against the client list by IP string.
            string host = addr.ToString();
            var client = _clientStore?.FindByHost(host);
            if (client != null) return client.Permissions;

            // Unknown non-loopback caller: if they have a valid API key, grant full access.
            // The API key is the authentication - the client list is for per-client restriction only.
            if (IsAuthenticated(req)) return _fullPermissions;

            return _noPermissions;
        }

        internal static bool IsPathAllowed(string path, ClientPermissions p)
        {
            // /health is unauthenticated (used by load-balancers and uptime monitors).
            if (path == "/health") return true;

            // Diagnostics - gated by AllowDiagnostics; loopback always gets _fullPermissions so
            // localhost callers are unaffected.
            if (path is "/ping" or "/metrics" or "/sysinfo" or "/env" or "/ls" or "/help" or "/status" or "/settings" or "/file" or "/events")
                return p.AllowDiagnostics;

            if (path is "/run") return p.AllowShellRun;

            if (path.StartsWith("/capture") || path.StartsWith("/ocr"))
                return p.AllowCapture;

            if (path.StartsWith("/ai") || path.StartsWith("/chat"))
                return p.AllowAi;

            if (path.StartsWith("/scenes") || path == "/editor")
                return p.AllowScenes;

            if (path.StartsWith("/clients"))
                return p.AllowClients;

            // find, exec, elements, windows, uimap, draw and everything else is automation.
            return p.AllowAutomation;
        }

        private static readonly byte[] ForbiddenBody =
            Encoding.UTF8.GetBytes(
                """{"success":false,"error":"Forbidden. Your client does not have permission for this endpoint."}""");

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

            // Unauthenticated health probe (safe to expose; no sensitive data)
            if (method == "GET" && rawPath.TrimEnd('/').Equals("/health", StringComparison.OrdinalIgnoreCase))
            {
                statusCode = await WriteResponse(res, HandleHealth(), "json");
                return;
            }

            // Auth gate
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

            // Per-client permission gate
            var perms = ResolvePermissions(req);
            if (!IsPathAllowed(path, perms))
            {
                statusCode = 403;
                Interlocked.Increment(ref _errorRequests);
                res.StatusCode      = 403;
                res.ContentType     = "application/json; charset=utf-8";
                res.ContentLength64 = ForbiddenBody.Length;
                try   { await res.OutputStream.WriteAsync(ForbiddenBody); }
                finally { res.Close(); }
                return;
            }

            ApexResult result;

            try
            {
                if (req.HasEntityBody && req.ContentLength64 > MaxRequestBodyBytes)
                {
                    result = new ApexResult
                    {
                        Success = false,
                        Action = $"{method} {path}",
                        Error = $"Request body too large. Limit is {MaxRequestBodyBytes} bytes."
                    };
                    statusCode = 413;
                    var buf = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
                    {
                        success = false,
                        action = result.Action,
                        data = (object?)null,
                        error = result.Error
                    }, FormatAdapter.s_indented));
                    res.ContentType = "application/json; charset=utf-8";
                    res.ContentLength64 = buf.Length;
                    res.StatusCode = statusCode;
                    try { await res.OutputStream.WriteAsync(buf); }
                    finally { res.Close(); }
                    Interlocked.Increment(ref _errorRequests);
                    return;
                }

                string body = "";
                try
                {
                    body = req.HasEntityBody
                        ? await ReadBodyWithLimitAsync(req, MaxRequestBodyBytes)
                        : "";
                }
                catch (PayloadTooLargeException ex)
                {
                    result = new ApexResult
                    {
                        Success = false,
                        Action = $"{method} {path}",
                        Error = ex.Message
                    };
                    statusCode = 413;
                    var buf = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
                    {
                        success = false,
                        action = result.Action,
                        data = (object?)null,
                        error = result.Error
                    }, FormatAdapter.s_indented));
                    res.ContentType = "application/json; charset=utf-8";
                    res.ContentLength64 = buf.Length;
                    res.StatusCode = statusCode;
                    try { await res.OutputStream.WriteAsync(buf); }
                    finally { res.Close(); }
                    Interlocked.Increment(ref _errorRequests);
                    return;
                }

                // Test page  served directly, bypasses format adapter
                if (method == "GET" && (path == "" || path == "/"))
                {
                    await ServeTestPage(res);
                    return;
                }

                // Scene editor page  served directly
                if (method == "GET" && path == "/editor")
                {
                    await ServeEditorPage(res);
                    return;
                }

                // Remote desktop page  served directly
                if (method == "GET" && path == "/remote")
                {
                    await ServeRemotePage(res);
                    return;
                }

                                // Help reference page
                if (method == "GET" && path == "/help" && format is "html")
                {
                    await ServeHelpPage(res);
                    return;
                }

                // Control panel - served directly
                if (method == "GET" && path == "/settings")
                {
                    await ServeSettingsPage(res);
                    return;
                }

                // AI Chat page  served directly with key embedded
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

                // Desktop event stream - long-lived SSE response. Streams window-created /
                // window-closed / window-title-changed events. Server-side filtered by
                // ?types=<csv> and optional ?windowId=<id>.
                if (method == "GET" && path == "/events")
                {
                    await HandleEventsAsync(req, res);
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

                // Graceful shutdown  host decides how to exit (WinForms: Application.Exit;
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
                            // No host wired  exit the process directly so the remote caller
                            // still gets what they asked for.
                            Environment.Exit(0);
                        }
                    });
                    return;
                }

                // Scene REST routes  handled before the main switch
                var sceneResult = TryHandleSceneRoute(method, path, body, req);
                if (sceneResult != null)
                {
                    statusCode = await WriteResponse(res, sceneResult, format);
                    if (statusCode >= 400) Interlocked.Increment(ref _errorRequests);
                    return;
                }

                // Annotation + region-map routes — same dispatch shape as scene routes
                var annotationResult = TryHandleAnnotationRoute(method, path, body, req);
                if (annotationResult != null)
                {
                    statusCode = await WriteResponse(res, annotationResult, format);
                    if (statusCode >= 400) Interlocked.Increment(ref _errorRequests);
                    return;
                }

                // /run is async  handled before the sync switch
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
                            ? (req.QueryString["cmd"] ?? req.QueryString["command"])
                            : (FromJson(body, "run").Value ?? ParseJsonString(body, "command"));
                        result = await HandleRunAsync(cmd);
                    }
                }
                else
                {
                    result = (method, path) switch
                    {
                        // Observability routes
                        ("GET", "/health")  => HandleHealth(),
                        ("GET", "/metrics") => HandleMetrics(),
                        // Diagnostic routes (auth-gated)
                        ("GET", "/ping")    => HandlePing(),
                        ("GET", "/sysinfo") => HandleSysinfo(),
                        ("GET", "/env")     => HandleEnv(),
                        ("GET", "/ls")      => HandleLs(req.QueryString["path"]),
                        // Read-only file content. Disabled by default - controlled by EnableFileIo
                        // and constrained to FileIoAllowedRoots.
                        ("GET", "/file")    => HandleFileRead(req.QueryString["path"]),
                        ("POST","/file")    => HandleFileRead(ParseJsonString(body, "path")),
                        ("GET",  "/winrun") => HandleWinRun(req.QueryString["target"], req.QueryString["args"]),
                        ("POST", "/winrun") => HandleWinRun(ParseJsonString(body, "target"), ParseJsonString(body, "args")),

                        // Existing routes  adapted to ApexResult
                        ("GET", "/status")
                            => ApexResult.From("status",     _dispatcher.Dispatch(new CommandRequest { Command = "status" })),
                        ("GET", "/windows")
                            => ApexResult.From("windows",    _dispatcher.Dispatch(new CommandRequest { Command = "windows" })),
                        ("GET", "/help")
                            => HandleHelp(req),
                        ("GET", "/elements")
                            => ApexResult.From("elements",   _dispatcher.Dispatch(new CommandRequest
                            {
                                Command        = "elements",
                                SearchType     = req.QueryString["type"],
                                AutomationId   = req.QueryString["id"],       // numeric  expands a subtree from a previously-mapped element
                                Depth          = int.TryParse(req.QueryString["depth"], out int _elemDepth) ? _elemDepth : null,
                                OnscreenOnly   = string.Equals(req.QueryString["onscreen"],       "true", StringComparison.OrdinalIgnoreCase),
                                Unfiltered     = string.Equals(req.QueryString["unfiltered"],    "true", StringComparison.OrdinalIgnoreCase),
                                // " Browser-friendly tree shaping (opt-in; see README) "
                                Match          = req.QueryString["match"],                        // text-search Name/AutomationId/Value
                                CollapseChains = string.Equals(req.QueryString["collapseChains"], "true", StringComparison.OrdinalIgnoreCase),
                                IncludePath    = string.Equals(req.QueryString["includePath"],    "true", StringComparison.OrdinalIgnoreCase),
            Properties     = req.QueryString["properties"],                   // "extra" -> value + helpText
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
                if (rawPath != "/ping")
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

        public void Dispose()
        {
            Stop();
            try { _events.Dispose();  } catch { }
            try { _listener.Close(); } catch { }
        }
    }
}

