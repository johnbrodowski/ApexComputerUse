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
        private          CancellationTokenSource? _cts;
        private          Task?              _listenTask;

        public int    Port      { get; }
        public bool   IsRunning { get; private set; }
        public event  Action<string>? OnLog;

        public HttpCommandServer(int port, CommandProcessor processor)
        {
            Port       = port;
            _processor = processor;
        }

        // ── Lifecycle ─────────────────────────────────────────────────────

        public void Start()
        {
            if (IsRunning) return;
            _listener.Prefixes.Clear();
            _listener.Prefixes.Add($"http://+:{Port}/");
            _listener.Start();
            IsRunning  = true;
            _cts       = new CancellationTokenSource();
            _listenTask = Task.Run(() => ListenLoop(_cts.Token));
            OnLog?.Invoke($"HTTP server listening on http://localhost:{Port}/");
        }

        public void Stop()
        {
            if (!IsRunning) return;
            _cts?.Cancel();
            try { _listener.Stop(); } catch { /* already stopped */ }
            IsRunning = false;
            OnLog?.Invoke("HTTP server stopped.");
        }

        // ── Accept loop ───────────────────────────────────────────────────

        private async Task ListenLoop(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var ctx = await _listener.GetContextAsync().WaitAsync(ct);
                    _ = Task.Run(() => HandleAsync(ctx), ct);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex) when (!ct.IsCancellationRequested)
                {
                    OnLog?.Invoke($"HTTP listener error: {ex.Message}");
                }
            }
        }

        // ── Request handler ───────────────────────────────────────────────

        private async Task HandleAsync(HttpListenerContext ctx)
        {
            var req    = ctx.Request;
            var res    = ctx.Response;
            string path   = req.Url?.AbsolutePath.TrimEnd('/').ToLowerInvariant() ?? "/";
            string method = req.HttpMethod.ToUpperInvariant();

            OnLog?.Invoke($"HTTP {method} {path}");

            CommandResponse response;
            try
            {
                string body = req.HasEntityBody
                    ? await new StreamReader(req.InputStream, req.ContentEncoding).ReadToEndAsync()
                    : "";

                response = (method, path) switch
                {
                    ("GET",  "/status")      => _processor.Process(new CommandRequest { Command = "status" }),
                    ("GET",  "/windows")     => _processor.Process(new CommandRequest { Command = "windows" }),
                    ("GET",  "/help")        => _processor.Process(new CommandRequest { Command = "help" }),
                    ("GET",  "/elements")    => _processor.Process(new CommandRequest
                    {
                        Command      = "elements",
                        SearchType   = req.QueryString["type"],
                        OnscreenOnly = string.Equals(req.QueryString["onscreen"], "true",
                                           StringComparison.OrdinalIgnoreCase)
                    }),
                    ("GET",  "/ai/status")   => _processor.Process(new CommandRequest { Command = "ai", Action = "status" }),
                    ("POST", "/find")        => _processor.Process(FromJson(body, "find")),
                    ("POST", "/execute")     => _processor.Process(FromJson(body, "execute")),
                    ("POST", "/exec")        => _processor.Process(FromJson(body, "execute")),
                    ("POST", "/ocr")         => _processor.Process(FromJson(body, "ocr")),
                    ("POST", "/capture")     => _processor.Process(FromJson(body, "capture")),
                    ("POST", "/ai/init")     => _processor.Process(FromJson(body, "ai", "init")),
                    ("POST", "/ai/describe") => _processor.Process(FromJson(body, "ai", "describe")),
                    ("POST", "/ai/file")     => _processor.Process(FromJson(body, "ai", "file")),
                    ("POST", "/ai/ask")      => _processor.Process(FromJson(body, "ai", "ask")),
                    _ => new CommandResponse { Success = false,
                             Message = $"Unknown: {method} {path}. GET /help for endpoints." }
                };
            }
            catch (Exception ex)
            {
                response = new CommandResponse { Success = false, Message = ex.Message };
            }

            await WriteResponse(res, response);
        }

        private static async Task WriteResponse(HttpListenerResponse res, CommandResponse cr)
        {
            try
            {
                byte[] buf = Encoding.UTF8.GetBytes(cr.ToJson());
                res.ContentType     = "application/json; charset=utf-8";
                res.ContentLength64 = buf.Length;
                res.StatusCode      = cr.Success ? 200 : 400;
                await res.OutputStream.WriteAsync(buf);
            }
            finally { res.Close(); }
        }

        // ── JSON parsing ──────────────────────────────────────────────────

        private static CommandRequest FromJson(string json, string command) =>
            FromJson(json, command, null);

        private static CommandRequest FromJson(string json, string command, string? action)
        {
            var r = new CommandRequest { Command = command, Action = action };
            if (string.IsNullOrWhiteSpace(json)) return r;
            try
            {
                using var doc  = JsonDocument.Parse(json);
                var root = doc.RootElement;
                r.Window       = root.Str("window");
                r.AutomationId = root.Str("automationId") ?? root.Str("id");
                r.ElementName  = root.Str("elementName")  ?? root.Str("name");
                r.SearchType   = root.Str("searchType")   ?? root.Str("type");
                r.Action       = root.Str("action") ?? action;
                r.Value        = root.Str("value");
                r.ModelPath    = root.Str("model")   ?? root.Str("modelPath");
                r.MmProjPath   = root.Str("proj")    ?? root.Str("mmProjPath");
                r.Prompt       = root.Str("prompt");
            }
            catch { /* malformed JSON — return partial */ }
            return r;
        }

        public void Dispose() => Stop();
    }

    internal static class JsonElementExtensions
    {
        public static string? Str(this JsonElement el, string name) =>
            el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String
                ? p.GetString() : null;
    }
}
