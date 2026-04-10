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
            var req     = ctx.Request;
            var res     = ctx.Response;
            string method  = req.HttpMethod.ToUpperInvariant();
            string rawPath = req.Url?.AbsolutePath.TrimEnd('/') ?? "/";
            string ext     = Path.GetExtension(rawPath).ToLowerInvariant();
            bool   hasExt  = ext is ".json" or ".html" or ".htm" or ".txt" or ".text" or ".pdf";
            // Strip format extension for routing; keep original for format detection
            string path    = hasExt ? rawPath[..^ext.Length].ToLowerInvariant()
                                    : rawPath.ToLowerInvariant();
            string format  = FormatAdapter.Negotiate(req, hasExt ? ext[1..] : null);

            OnLog?.Invoke($"HTTP {method} {rawPath}");
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

                // /run is async — handled before the sync switch
                if (path == "/run")
                {
                    string? cmd = method == "GET"
                        ? req.QueryString["cmd"]
                        : FromJson(body, "run").Value;
                    result = await HandleRunAsync(cmd);
                }
                else
                {
                    result = (method, path) switch
                    {
                        // ── New routes ─────────────────────────────────────────────
                        ("GET", "/ping")    => HandlePing(),
                        ("GET", "/sysinfo") => HandleSysinfo(),
                        ("GET", "/env")     => HandleEnv(),
                        ("GET", "/ls")      => HandleLs(req.QueryString["path"]),

                        // ── Existing routes — adapted to ApexResult ────────────────
                        ("GET", "/status")
                            => ApexResult.From("status",     _processor.Process(new CommandRequest { Command = "status" })),
                        ("GET", "/windows")
                            => ApexResult.From("windows",    _processor.Process(new CommandRequest { Command = "windows" })),
                        ("GET", "/help")
                            => ApexResult.From("help",       _processor.Process(new CommandRequest { Command = "help" })),
                        ("GET", "/elements")
                            => ApexResult.From("elements",   _processor.Process(new CommandRequest
                            {
                                Command      = "elements",
                                SearchType   = req.QueryString["type"],
                                OnscreenOnly = string.Equals(req.QueryString["onscreen"], "true",
                                                   StringComparison.OrdinalIgnoreCase)
                            })),
                        ("GET", "/uimap")
                            => ApexResult.From("uimap",      _processor.Process(new CommandRequest { Command = "uimap" })),
                        ("GET", "/ai/status")
                            => ApexResult.From("ai/status",  _processor.Process(new CommandRequest { Command = "ai", Action = "status" })),
                        ("POST", "/find") or ("GET", "/find")
                            => ApexResult.From("find",    _processor.Process(
                                method == "POST" ? FromJson(body, "find") : FromQueryString(req, "find"))),
                        ("POST", "/execute") or ("GET", "/execute")
                            => ApexResult.From("execute", _processor.Process(
                                method == "POST" ? FromJson(body, "execute") : FromQueryString(req, "execute"))),
                        ("POST", "/exec") or ("GET", "/exec")
                            => ApexResult.From("execute", _processor.Process(
                                method == "POST" ? FromJson(body, "execute") : FromQueryString(req, "execute"))),
                        ("POST", "/ocr") or ("GET", "/ocr")
                            => ApexResult.From("ocr",     _processor.Process(
                                method == "POST" ? FromJson(body, "ocr") : FromQueryString(req, "ocr"))),
                        ("POST", "/capture") or ("GET", "/capture")
                            => ApexResult.From("capture", _processor.Process(
                                method == "POST" ? FromJson(body, "capture") : FromQueryString(req, "capture"))),
                        ("POST", "/ai/init")
                            => ApexResult.From("ai/init",    _processor.Process(FromJson(body, "ai", "init"))),
                        ("POST", "/ai/describe")
                            => ApexResult.From("ai/describe",_processor.Process(FromJson(body, "ai", "describe"))),
                        ("POST", "/ai/file")
                            => ApexResult.From("ai/file",    _processor.Process(FromJson(body, "ai", "file"))),
                        ("POST", "/ai/ask")
                            => ApexResult.From("ai/ask",     _processor.Process(FromJson(body, "ai", "ask"))),
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

            await WriteResponse(res, result, format);
        }

        private static async Task WriteResponse(HttpListenerResponse res, ApexResult result, string format)
        {
            var (buf, contentType, statusCode) = FormatAdapter.Render(result, format);
            res.ContentType     = contentType;
            res.ContentLength64 = buf.Length;
            res.StatusCode      = statusCode;
            try   { await res.OutputStream.WriteAsync(buf); }
            finally { res.Close(); }
        }

        // ── New route handlers ────────────────────────────────────────────

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
            string dir = string.IsNullOrWhiteSpace(requestedPath)
                ? Environment.CurrentDirectory : requestedPath;

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

                  .vrow { display: flex; gap: .5em; align-items: center; }
                  .vrow label { font-size: .76em; color: #888; white-space: nowrap; }
                  .vrow input { flex: 1; background: #1e1e1e; color: #d4d4d4; border: 1px solid #3c3c3c;
                                border-radius: 3px; padding: .28em .5em; font: inherit; font-size: .85em; }
                  .vrow input:focus { outline: 1px solid #0e639c; }
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
                  </span>
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
                        <input id="val" placeholder="(optional — text, x,y, row,col, …)"
                               onkeydown="if(event.key==='Enter')doExec()">
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

                // ── Format bar ─────────────────────────────────────────────
                function onFmtChange() {
                  const fmt = document.getElementById('fmtSel').value;
                  const ext = fmt === 'json' ? 'json' : fmt;
                  document.getElementById('lnkHelp').href    = '/help.'    + ext;
                  document.getElementById('lnkStatus').href  = '/status.'  + ext;
                  document.getElementById('lnkWindows').href = '/windows.' + ext;
                }

                // ── API ─────────────────────────────────────────────────────
                async function call(method, path, body) {
                  const fmt  = document.getElementById('fmtSel')?.value ?? 'json';
                  const sep  = path.includes('?') ? '&' : '?';
                  const url  = path + sep + 'format=' + fmt;
                  const opts = { method };
                  if (body !== undefined && method !== 'GET') {
                    opts.headers = { 'Content-Type': 'application/json' };
                    opts.body    = JSON.stringify(body);
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

        private static CommandRequest FromQueryString(HttpListenerRequest req, string command, string? action = null)
            => new()
            {
                Command      = command,
                Action       = req.QueryString["action"]       ?? action,
                Value        = req.QueryString["value"],
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
            };

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
                r.AutomationId = root.Str("automationId") ?? root.Str("id") ?? root.Str("element");
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
    }
}
