using System.Net;
using System.Text;

namespace ApexComputerUse
{
    public partial class HttpCommandServer
    {
        // ── Control Panel / Settings page ────────────────────────────────────

        private static AppSettings? TryLoadAppSettings()
        {
            try
            {
                string file = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "ApexComputerUse", "settings.json");
                if (!File.Exists(file)) return null;
                return System.Text.Json.JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(file));
            }
            catch { return null; }
        }

        private async Task ServeSettingsPage(HttpListenerResponse res)
        {
            var    cfg        = AppConfig.Current;
            var    saved      = TryLoadAppSettings();

            // Merge: AppSettings (GUI persisted) wins over AppConfig (env/appsettings.json)
            string effectiveModelPath    = FirstNonEmpty(saved?.ModelPath,     cfg.ModelPath);
            string effectiveMmPath       = FirstNonEmpty(saved?.ProjPath,      cfg.MmProjPath);
            string effectiveAllowedIds   = FirstNonEmpty(saved?.AllowedChatIds, cfg.AllowedChatIds);

            bool   hasKey     = _apiKey != null;
            string authMode   = hasKey ? "enabled" : "disabled";
            string authClass  = hasKey ? "ok" : "warn";
            string bindDesc   = _bindAll ? "all interfaces" : "localhost only";
            string shellDesc  = _enableShellRun ? "enabled" : "disabled";
            string shellClass = _enableShellRun ? "warn" : "off";
            string maskedKey  = hasKey
                ? (_apiKey!.Length > 8
                    ? _apiKey[..4] + "••••••••" + _apiKey[^4..]
                    : "••••••••")
                : "(none — auth disabled)";

            string tgToken   = string.IsNullOrEmpty(cfg.TelegramToken) ? "(not configured)" : "configured";
            string tgIds     = string.IsNullOrEmpty(effectiveAllowedIds) ? "(none — open to any user)" : H(effectiveAllowedIds);
            string modelPath = string.IsNullOrEmpty(effectiveModelPath) ? "(not set)" : H(effectiveModelPath);
            string mmPath    = string.IsNullOrEmpty(effectiveMmPath)    ? "(not set)" : H(effectiveMmPath);

            string copyButtons = hasKey
                ? "<button id=\"revealBtn\" onclick=\"revealKey()\">Reveal</button>" +
                  "<button id=\"copyBtn\" onclick=\"copyKey()\">Copy</button>"
                : "";

            // Extra config rows only shown when configured
            var extraRows = new StringBuilder();
            if (!string.IsNullOrEmpty(cfg.TestRunnerExePath))
                extraRows.Append(Row("TestRunnerExePath", cfg.TestRunnerExePath, "APEX_TEST_RUNNER_EXE_PATH", "Test runner executable"));
            if (!string.IsNullOrEmpty(cfg.TestRunnerConfigPath))
                extraRows.Append(Row("TestRunnerConfigPath", cfg.TestRunnerConfigPath, "APEX_TEST_RUNNER_CONFIG_PATH", "Test runner config file"));

            string safeKey = hasKey ? H(_apiKey!) : "";

            var sb = new StringBuilder(20480);
            sb.Append($$"""
                <!DOCTYPE html>
                <html lang="en">
                <head>
                <meta charset="utf-8">
                <title>ApexComputerUse — Control Panel</title>
                <style>
                  *, *::before, *::after { box-sizing: border-box; margin: 0; padding: 0; }
                  body { font-family: monospace; font-size: 13px; background: #1e1e1e; color: #d4d4d4;
                         display: flex; flex-direction: column; min-height: 100vh; }

                  header { background: #252526; border-bottom: 1px solid #3c3c3c;
                           padding: .35em 1em; display: flex; align-items: center; gap: .8em; flex-shrink: 0; }
                  .brand  { color: #9cdcfe; font-size: .82em; }
                  .htitle { color: #c8c8c8; flex: 1; font-size: .85em; }
                  nav.hlinks { display: flex; gap: .4em; }
                  nav.hlinks a { color: #888; font-size: .78em; text-decoration: none;
                                 padding: .18em .5em; border: 1px solid #3c3c3c; border-radius: 3px; }
                  nav.hlinks a:hover { background: #2d2d30; color: #d4d4d4; }

                  main { padding: 1.2em 1.5em 3em; max-width: 1100px; }

                  h2 { color: #4ec94e; font-size: .8em; text-transform: uppercase; letter-spacing: .07em;
                       margin: 1.5em 0 .6em; padding-bottom: .25em; border-bottom: 1px solid #3c3c3c; }
                  h2:first-child { margin-top: 0; }

                  /* ── Status cards ── */
                  .cards { display: flex; gap: .75em; margin-bottom: .5em; flex-wrap: wrap; }
                  .card { background: #252526; border: 1px solid #3c3c3c; border-radius: 4px;
                          padding: .7em 1em; min-width: 180px; flex: 1; }
                  .card-title { font-size: .72em; color: #888; text-transform: uppercase;
                                letter-spacing: .06em; margin-bottom: .45em; }
                  .card-status { display: flex; align-items: center; gap: .45em; font-size: .88em; margin-bottom: .35em; }
                  .dot { width: 8px; height: 8px; border-radius: 50%; flex-shrink: 0; }
                  .dot.ok   { background: #4ec94e; }
                  .dot.warn { background: #e0a852; }
                  .dot.off  { background: #555; }
                  .card-detail { font-size: .78em; color: #888; line-height: 1.6; }
                  .card-detail .v { color: #9cdcfe; }
                  .card-detail .ok   { color: #4ec94e; }
                  .card-detail .warn { color: #e0a852; }
                  .card-detail .off  { color: #555; }

                  /* ── API key row ── */
                  .keyrow { display: flex; align-items: center; gap: .5em; margin-bottom: .5em; }
                  .keybox { background: #1e1e1e; border: 1px solid #3c3c3c; border-radius: 3px;
                            padding: .28em .6em; color: #ce9178; font-family: monospace; font-size: .88em;
                            flex: 1; max-width: 460px; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
                  button { background: #2d2d30; border: 1px solid #555; color: #d4d4d4; cursor: pointer;
                           padding: .22em .65em; font-family: monospace; font-size: .78em; border-radius: 3px; }
                  button:hover { background: #3e3e42; }
                  button.done { color: #4ec94e; border-color: #4ec94e; }

                  /* ── Config table ── */
                  table { width: 100%; border-collapse: collapse; font-size: .82em; margin-bottom: .5em; }
                  th { text-align: left; color: #666; font-weight: normal; font-size: .85em;
                       padding: .15em .6em .3em; border-bottom: 1px solid #3c3c3c; }
                  td { padding: .22em .6em; vertical-align: top; border-bottom: 1px solid #252526; }
                  tr:hover td { background: #252526; }
                  td.key  { color: #9cdcfe; white-space: nowrap; width: 210px; }
                  td.val  { color: #ce9178; }
                  td.val.t { color: #4ec94e; }
                  td.val.f { color: #555; }
                  td.val.dim { color: #555; font-style: italic; }
                  td.env  { color: #555; font-size: .88em; white-space: nowrap; }
                  td.dsc  { color: #888; font-size: .88em; }

                  /* ── Metrics row ── */
                  .mrow { display: flex; gap: 2em; flex-wrap: wrap; margin-bottom: .5em; }
                  .mc   { }
                  .mv   { font-size: 1.35em; color: #9cdcfe; }
                  .ml   { font-size: .72em; color: #666; }

                  /* ── Quick links ── */
                  .links { display: flex; gap: .45em; flex-wrap: wrap; }
                  .links a { color: #9cdcfe; font-size: .82em; text-decoration: none;
                             padding: .22em .65em; border: 1px solid #3c3c3c; border-radius: 3px; }
                  .links a:hover { background: #2d2d30; }

                  .note { font-size: .75em; color: #555; margin-top: .5em; }
                  #autoRefresh { font-size: .72em; color: #555; margin-left: auto; }
                </style>
                </head>
                <body>
                <header>
                  <span class="brand">ApexComputerUse</span>
                  <span class="htitle">Control Panel</span>
                  <nav class="hlinks">
                    <a href="/">Test</a>
                    <a href="/help">Help</a>
                    <a href="/editor">Editor</a>
                    <a href="/chat">Chat</a>
                  </nav>
                </header>
                <main>

                <h2>Server Status</h2>
                <div class="cards">
                  <div class="card">
                    <div class="card-title">HTTP Server</div>
                    <div class="card-status"><div class="dot ok"></div> Running</div>
                    <div class="card-detail">
                      Port: <span class="v">{{Port}}</span><br>
                      Auth: <span class="{{authClass}}">{{authMode}}</span><br>
                      Bind: <span class="v">{{bindDesc}}</span><br>
                      Shell run: <span class="{{shellClass}}">{{shellDesc}}</span>
                    </div>
                  </div>
                  <div class="card">
                    <div class="card-title">Named Pipe</div>
                    <div class="card-status"><div class="dot off"></div> <span id="pipeLabel">—</span></div>
                    <div class="card-detail">Name: <span class="v">{{H(cfg.PipeName)}}</span></div>
                  </div>
                  <div class="card">
                    <div class="card-title">Telegram</div>
                    <div class="card-status"><div class="dot off"></div> <span id="tgLabel">—</span></div>
                    <div class="card-detail">
                      Token: <span class="v">{{tgToken}}</span><br>
                      Allowed IDs: <span class="v">{{tgIds}}</span>
                    </div>
                  </div>
                  <div class="card">
                    <div class="card-title">Runtime <span id="autoRefresh">auto-refresh 5s</span></div>
                    <div class="card-status"><div class="dot ok" id="healthDot"></div> <span id="uptime">—</span></div>
                    <div class="card-detail">
                      Requests: <span id="totalReq" class="v">—</span><br>
                      Errors: <span id="errReq" class="v">—</span><br>
                      Active: <span id="activeReq" class="v">—</span>
                    </div>
                  </div>
                </div>

                <h2>API Key</h2>
                <div class="keyrow">
                  <div class="keybox" id="keyBox">{{maskedKey}}</div>
                  {{copyButtons}}
                </div>

                <h2>Configuration</h2>
                <table>
                  <thead><tr><th>Key</th><th>Value</th><th>Environment variable</th><th>Description</th></tr></thead>
                  <tbody>
                    {{Row("HttpPort",      cfg.HttpPort.ToString(),                                   "APEX_HTTP_PORT",         "HTTP listen port")}}
                    {{Row("HttpBindAll",   B(cfg.HttpBindAll),                                        "APEX_HTTP_BIND_ALL",     "Bind to all interfaces instead of localhost")}}
                    {{Row("HttpAutoStart", B(cfg.HttpAutoStart),                                      "APEX_HTTP_AUTOSTART",    "Auto-start HTTP server on GUI launch")}}
                    {{Row("PipeName",      H(cfg.PipeName),                                           "APEX_PIPE_NAME",         "Named pipe name")}}
                    {{Row("LogLevel",      H(cfg.LogLevel),                                           "APEX_LOG_LEVEL",         "Serilog minimum level: Debug / Information / Warning / Error")}}
                    {{Row("EnableShellRun",B(cfg.EnableShellRun),                                     "APEX_ENABLE_SHELL_RUN",  "Enable POST /run shell-execution endpoint (dangerous)")}}
                    {{Row("ModelPath",     modelPath,                                                  "APEX_MODEL_PATH",        "LLM model .gguf file path")}}
                    {{Row("MmProjPath",    mmPath,                                                     "APEX_MMPROJ_PATH",       "Multimodal projector .gguf file path")}}
                    {{Row("ApiKey",        "(redacted)",                                               "APEX_API_KEY",           "HTTP authentication key", redacted: true)}}
                    {{Row("AllowedChatIds",string.IsNullOrEmpty(cfg.AllowedChatIds) ? "(not set)" : H(cfg.AllowedChatIds), "APEX_ALLOWED_CHAT_IDS", "Telegram chat ID whitelist (comma-separated)")}}
                    {{Row("TelegramToken", "(redacted)",                                               "APEX_TELEGRAM_TOKEN",    "Telegram bot token", redacted: true)}}
                    {{extraRows}}
                  </tbody>
                </table>
                <p class="note">
                  Load order: compiled defaults → appsettings.json → APEX_* env vars → GUI field overrides.
                  Edit appsettings.json or set env vars before launch to persist changes.
                </p>

                <h2>Runtime Metrics</h2>
                <div class="mrow">
                  <div class="mc"><div class="mv" id="m_total">—</div><div class="ml">total requests</div></div>
                  <div class="mc"><div class="mv" id="m_err">—</div><div class="ml">errors</div></div>
                  <div class="mc"><div class="mv" id="m_active">—</div><div class="ml">active now</div></div>
                  <div class="mc"><div class="mv" id="m_up">—</div><div class="ml">uptime</div></div>
                  <div class="mc"><div class="mv" id="m_model">—</div><div class="ml">model loaded</div></div>
                </div>

                <h2>Quick Links</h2>
                <div class="links">
                  <a href="/">Test Console</a>
                  <a href="/help">API Reference</a>
                  <a href="/editor">Scene Editor</a>
                  <a href="/chat">AI Chat</a>
                  <a href="/health.json" target="_blank">Health JSON</a>
                  <a href="/metrics.json" target="_blank">Metrics JSON</a>
                  <a href="/sysinfo.json" target="_blank">Sysinfo JSON</a>
                  <a href="/env.json" target="_blank">Env Vars JSON</a>
                  <a href="/status.json" target="_blank">Status JSON</a>
                  <a href="/windows.json" target="_blank">Windows JSON</a>
                </div>

                </main>
                <script>
                const KEY = '{{safeKey}}';

                function revealKey() {
                  document.getElementById('keyBox').textContent = KEY || '(none)';
                  const btn = document.getElementById('revealBtn');
                  if (btn) { btn.textContent = 'Hide'; btn.onclick = hideKey; }
                }
                function hideKey() {
                  document.getElementById('keyBox').textContent = document.getElementById('keyBox').textContent.length > 12
                    ? KEY.slice(0,4) + '••••••••' + KEY.slice(-4)
                    : '••••••••';
                  const btn = document.getElementById('revealBtn');
                  if (btn) { btn.textContent = 'Reveal'; btn.onclick = revealKey; }
                }
                function copyKey() {
                  if (!KEY) return;
                  navigator.clipboard.writeText(KEY).then(() => {
                    const btn = document.getElementById('copyBtn');
                    if (!btn) return;
                    btn.textContent = 'Copied!';
                    btn.classList.add('done');
                    setTimeout(() => { btn.textContent = 'Copy'; btn.classList.remove('done'); }, 1600);
                  });
                }

                async function refresh() {
                  try {
                    const qs = KEY ? '?apiKey=' + encodeURIComponent(KEY) : '';
                    const r  = await fetch('/health.json' + qs);
                    if (!r.ok) return;
                    const j = await r.json();
                    const d = j.data || {};
                    document.getElementById('uptime').textContent     = d.uptime          || '—';
                    document.getElementById('totalReq').textContent   = d.total_requests  || '0';
                    document.getElementById('errReq').textContent     = d.error_requests  || '0';
                    document.getElementById('activeReq').textContent  = d.active_requests || '0';
                    document.getElementById('m_total').textContent    = d.total_requests  || '0';
                    document.getElementById('m_err').textContent      = d.error_requests  || '0';
                    document.getElementById('m_active').textContent   = d.active_requests || '0';
                    document.getElementById('m_up').textContent       = d.uptime          || '—';
                    document.getElementById('m_model').textContent    = d.model_loaded    || '—';
                    document.getElementById('healthDot').className    = 'dot ok';
                  } catch {
                    document.getElementById('healthDot').className = 'dot warn';
                  }
                }

                refresh();
                setInterval(refresh, 5000);

                if (KEY) {
                  document.querySelectorAll('a[href]').forEach(a => {
                    try {
                      const url = new URL(a.href, location.href);
                      url.searchParams.set('apiKey', KEY);
                      a.href = url.toString();
                    } catch {}
                  });
                }
                </script>
                </body>
                </html>
                """);

            var buf = Encoding.UTF8.GetBytes(sb.ToString());
            res.ContentType     = "text/html; charset=utf-8";
            res.ContentLength64 = buf.Length;
            res.StatusCode      = 200;
            try   { await res.OutputStream.WriteAsync(buf); }
            finally { res.Close(); }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static string FirstNonEmpty(string? a, string? b) =>
            !string.IsNullOrWhiteSpace(a) ? a : (b ?? "");

        private static string H(string s) =>
            s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");

        private static string B(bool v) => v ? "true" : "false";

        private static string Row(string key, string value, string envVar, string desc, bool redacted = false)
        {
            string valClass = "val";
            if      (redacted)       valClass += " dim";
            else if (value == "true")  valClass += " t";
            else if (value == "false") valClass += " f";
            else if (value is "(not set)" or "(not configured)") valClass += " dim";
            return $"<tr><td class=\"key\">{H(key)}</td>" +
                   $"<td class=\"{valClass}\">{H(value)}</td>" +
                   $"<td class=\"env\">{H(envVar)}</td>" +
                   $"<td class=\"dsc\">{H(desc)}</td></tr>\n";
        }
    }
}
