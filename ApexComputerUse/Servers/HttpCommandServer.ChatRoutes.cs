using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ApexComputerUse
{
    public partial class HttpCommandServer
    {
        // AI Chat

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
            <title>AI Chat ApexComputerUse</title>
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
              .typing::after { content: "–‹"; animation: blink .7s step-end infinite; }
              @keyframes blink { 50% { opacity: 0; } }
            </style>
            </head>
            <body>
            <header>
              <span class="title">AI Chat</span>
              <span id="badge">”</span>
              <span id="modelLabel">loading¦</span>
              <button id="resetBtn" title="Clear conversation history on server">New chat</button>
            </header>
            <div id="messages"></div>
            <div id="statusBar"></div>
            <div id="inputArea">
              <textarea id="inputBox" rows="1" placeholder="Message (Enter to send, Shift+Enter for newline)"></textarea>
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
                  $badge.textContent = d.data.provider || '”';
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
              setStatus('Thinking¦');

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

    }
}
