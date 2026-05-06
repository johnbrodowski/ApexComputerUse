using System.Net;
using System.Text;

namespace ApexComputerUse
{
    public partial class HttpCommandServer
    {
        // Remote desktop page

        private static async Task ServeRemotePage(HttpListenerResponse res)
        {
            const string html = """
                <!DOCTYPE html>
                <html lang="en">
                <head>
                <meta charset="utf-8">
                <title>ApexComputerUse  Remote Desktop</title>
                <style>
                  *, *::before, *::after { box-sizing: border-box; margin: 0; padding: 0; }
                  body { font-family: monospace; font-size: 13px; background: #1e1e1e; color: #d4d4d4;
                         display: flex; flex-direction: column; height: 100vh; overflow: hidden; }

                  /* Header */
                  header { background: #252526; border-bottom: 1px solid #3c3c3c;
                           padding: .35em 1em; display: flex; align-items: center; gap: .75em;
                           flex-shrink: 0; flex-wrap: wrap; }
                  #dot { width: 8px; height: 8px; border-radius: 50%; background: #555; flex-shrink: 0; }
                  #dot.ok  { background: #4ec94e; }
                  #dot.err { background: #e05252; }
                  #dot.live { background: #4ec94e; box-shadow: 0 0 6px #4ec94e; }
                  #statusTxt { font-size: .78em; color: #888; }
                  header span.spacer { flex: 1; }
                  header span.brand  { font-size: .82em; color: #9cdcfe; }
                  header label { font-size: .75em; color: #9cdcfe; display: flex; align-items: center; gap: .3em; }
                  header select, header input[type=number], header input[type=password], header input[type=text] {
                    background: #3c3c3c; color: #d4d4d4; border: 1px solid #555; border-radius: 2px;
                    padding: .15em .3em; font: inherit; font-size: .76em;
                  }
                  header button {
                    background: #0e639c; color: #fff; border: none; border-radius: 2px;
                    padding: .25em .7em; font: inherit; font-size: .76em; cursor: pointer;
                  }
                  header button:hover { background: #1177bb; }
                  header button.off { background: #2d2d30; color: #bbb; border: 1px solid #444; }
                  header button.off:hover { background: #3e3e42; color: #d4d4d4; }

                  /* Main 2-col layout */
                  main { display: grid; grid-template-columns: 240px 1fr; flex: 1; overflow: hidden; min-height: 0; }

                  /* Left sidebar */
                  aside { background: #252526; border-right: 1px solid #3c3c3c;
                          display: flex; flex-direction: column; overflow: hidden; min-height: 0; }

                  .panel { display: flex; flex-direction: column; overflow: hidden; border-bottom: 1px solid #3c3c3c; }
                  .panel.wins  { flex: 0 0 240px; }
                  .panel.keys  { flex: 0 0 auto; }
                  .panel.log   { flex: 1; min-height: 0; }

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

                  .keypad { padding: .5em; display: grid; grid-template-columns: repeat(3, 1fr); gap: .25em; }
                  .keypad button {
                    background: #2d2d30; color: #bbb; border: 1px solid #444; border-radius: 3px;
                    padding: .3em .2em; cursor: pointer; font: inherit; font-size: .72em;
                  }
                  .keypad button:hover { background: #3e3e42; color: #d4d4d4; }

                  .typebox { padding: .5em; display: flex; flex-direction: column; gap: .3em; }
                  .typebox textarea {
                    background: #1e1e1e; color: #d4d4d4; border: 1px solid #3c3c3c; border-radius: 3px;
                    padding: .3em .5em; font: inherit; font-size: .82em; resize: vertical; min-height: 3em;
                  }
                  .typebox button {
                    background: #0e639c; color: #fff; border: none; border-radius: 2px;
                    padding: .3em .7em; font: inherit; font-size: .76em; cursor: pointer;
                  }
                  .typebox button:hover { background: #1177bb; }

                  /* Right - viewport */
                  section { display: flex; flex-direction: column; overflow: hidden; min-height: 0; background: #0d0d0d; }

                  #viewportWrap {
                    flex: 1; position: relative; overflow: auto; min-height: 0;
                    display: flex; align-items: center; justify-content: center;
                    background: repeating-linear-gradient(45deg, #161616 0 8px, #1a1a1a 8px 16px);
                  }
                  #screen {
                    max-width: 100%; max-height: 100%; display: block; cursor: crosshair;
                    box-shadow: 0 0 30px rgba(0,0,0,.6); image-rendering: pixelated;
                    background: #000;
                  }
                  #screen.fitWidth  { width: 100%;  height: auto; max-height: none; }
                  #screen.fitNone   { max-width: none; max-height: none; }

                  #overlay {
                    position: absolute; pointer-events: none; border: 2px solid #4ec94e;
                    background: rgba(78, 201, 78, .15); border-radius: 4px;
                    transition: opacity .15s; opacity: 0;
                  }

                  #footer {
                    background: #252526; border-top: 1px solid #3c3c3c;
                    padding: .25em .8em; font-size: .72em; color: #888;
                    display: flex; gap: 1.2em; flex-shrink: 0;
                  }
                  #footer span.k { color: #9cdcfe; }

                  .msg { border-left: 3px solid #555; background: #1e1e1e; border-radius: 2px;
                         padding: .35em .55em; margin: .25em .5em; font-size: .72em; }
                  .msg.ok  { border-color: #4ec94e; }
                  .msg.err { border-color: #e05252; }
                  .msg .ts { color: #666; font-size: .9em; }
                  #log { overflow-y: auto; min-height: 0; }
                </style>
                </head>
                <body>

                <header>
                  <span id="dot"></span>
                  <span id="statusTxt">connecting</span>
                  <span class="spacer"></span>

                  <label>API Key
                    <input id="apiKeyBox" type="password" placeholder="paste key" autocomplete="off" style="width:140px;">
                    <button type="button" id="apiKeyShow" class="off" style="padding:.15em .4em;">Show</button>
                  </label>

                  <label>FPS
                    <input id="fpsBox" type="number" min="1" max="30" value="4" style="width:44px;">
                  </label>

                  <label>Quality
                    <select id="qualSel">
                      <option value="full">Full</option>
                      <option value="window" selected>Active Window</option>
                    </select>
                  </label>

                  <label>Fit
                    <select id="fitSel">
                      <option value="contain" selected>Contain</option>
                      <option value="width">Fit Width</option>
                      <option value="none">1:1</option>
                    </select>
                  </label>

                  <button type="button" id="streamBtn">Start</button>
                  <button type="button" id="snapBtn" class="off">Snap</button>

                  <span class="brand">ApexComputerUse  Remote</span>
                </header>

                <main>
                  <!-- Left sidebar -->
                  <aside>
                    <div class="panel wins">
                      <div class="phead">
                        <span>Windows</span>
                        <button onclick="loadWindows()" title="Refresh">refresh</button>
                      </div>
                      <select class="lst" id="winList" size="10" onchange="onWinSelect()"></select>
                    </div>

                    <div class="panel keys">
                      <div class="phead"><span>Special Keys</span></div>
                      <div class="keypad">
                        <button onclick="sendKeys('{ENTER}')">Enter</button>
                        <button onclick="sendKeys('{ESC}')">Esc</button>
                        <button onclick="sendKeys('{TAB}')">Tab</button>
                        <button onclick="sendKeys('{BS}')">Bksp</button>
                        <button onclick="sendKeys('{DEL}')">Del</button>
                        <button onclick="sendKeys('{HOME}')">Home</button>
                        <button onclick="sendKeys('{END}')">End</button>
                        <button onclick="sendKeys('{PGUP}')">PgUp</button>
                        <button onclick="sendKeys('{PGDN}')">PgDn</button>
                        <button onclick="sendKeys('{UP}')">Up</button>
                        <button onclick="sendKeys('{DOWN}')">Down</button>
                        <button onclick="sendKeys('{LEFT}')">Left</button>
                        <button onclick="sendKeys('{RIGHT}')">Rt</button>
                        <button onclick="sendKeys('Ctrl+a')">Ctrl+A</button>
                        <button onclick="sendKeys('Ctrl+c')">Ctrl+C</button>
                        <button onclick="sendKeys('Ctrl+v')">Ctrl+V</button>
                        <button onclick="sendKeys('Ctrl+x')">Ctrl+X</button>
                        <button onclick="sendKeys('Ctrl+z')">Ctrl+Z</button>
                        <button onclick="sendKeys('Alt+{TAB}')">Alt+Tab</button>
                        <button onclick="sendKeys('Alt+{F4}')">Alt+F4</button>
                        <button onclick="sendKeys('{LWIN}')">Win</button>
                      </div>
                      <div class="typebox">
                        <textarea id="typeBox" placeholder="Type text here&#10;(Ctrl+Enter to send)"
                                  onkeydown="if(event.key==='Enter'&&(event.ctrlKey||event.metaKey)){event.preventDefault();typeText();}"></textarea>
                        <button onclick="typeText()">Type</button>
                      </div>
                    </div>

                    <div class="panel log">
                      <div class="phead">
                        <span>Log</span>
                        <button onclick="document.getElementById('log').innerHTML=''" title="Clear">x</button>
                      </div>
                      <div id="log"></div>
                    </div>
                  </aside>

                  <!-- Right - viewport -->
                  <section>
                    <div id="viewportWrap"
                         oncontextmenu="event.preventDefault(); onScreenClick(event, 'right'); return false;">
                      <img id="screen" alt="(no signal)" draggable="false"
                           onclick="onScreenClick(event, 'left')"
                           ondblclick="onScreenClick(event, 'double')">
                      <div id="overlay"></div>
                    </div>
                    <div id="footer">
                      <span><span class="k">FPS:</span> <span id="fpsStat">0.0</span></span>
                      <span><span class="k">Frame:</span> <span id="frameStat">  </span></span>
                      <span><span class="k">Latency:</span> <span id="latStat">  ms</span></span>
                      <span><span class="k">Cursor:</span> <span id="curStat">  ,  </span></span>
                      <span><span class="k">Window:</span> <span id="winStat">(none)</span></span>
                    </div>
                  </section>
                </main>

                <script>
                'use strict';

                let curWin = '';
                let curWinId = '';
                let streaming = false;
                let streamTimer = null;
                let lastFrameAt = 0;
                let frameCount = 0;
                let lastFps = 0;
                let fpsTick = setInterval(() => {
                  document.getElementById('fpsStat').textContent = lastFps.toFixed(1);
                  lastFps = 0;
                }, 1000);

                // Boot
                (async () => {
                  try {
                    const r = await call('GET', '/ping');
                    setDot(true);
                    document.getElementById('statusTxt').textContent =
                      'ok ' + (r.data && r.data.timestamp ? r.data.timestamp : '');
                  } catch (e) {
                    setDot(false);
                    document.getElementById('statusTxt').textContent = 'server unreachable';
                  }
                  await loadWindows();
                  applyFit();
                })();

                function setDot(state) {
                  const d = document.getElementById('dot');
                  d.className = state === true ? 'ok' : state === 'live' ? 'live' : 'err';
                }

                // Windows
                async function loadWindows() {
                  try {
                    const r = await call('GET', '/windows');
                    const raw = r.data && r.data.result ? r.data.result : '[]';
                    let wins = [];
                    try { wins = JSON.parse(raw); } catch (_) {}
                    const sel = document.getElementById('winList');
                    sel.innerHTML = '';
                    wins.forEach(w => {
                      const o = document.createElement('option');
                      o.value = String(w.id || '');
                      o.textContent = w.title || String(w);
                      o.dataset.title = w.title || '';
                      o.dataset.id = String(w.id || '');
                      sel.appendChild(o);
                    });
                    appendLog('windows', true, wins.length + ' window(s)');
                  } catch (e) {
                    appendLog('windows', false, String(e));
                  }
                }

                async function onWinSelect() {
                  const sel = document.getElementById('winList');
                  const o = sel.options[sel.selectedIndex];
                  if (!o) return;
                  curWin = o.dataset.title;
                  curWinId = o.dataset.id;
                  document.getElementById('winStat').textContent = curWin;
                  try {
                    await call('POST', '/find', { window: curWin });
                    appendLog('find', true, 'focused: ' + curWin);
                  } catch (e) {
                    appendLog('find', false, String(e));
                  }
                }

                // Streaming
                document.getElementById('streamBtn').addEventListener('click', () => {
                  streaming ? stopStream() : startStream();
                });
                document.getElementById('snapBtn').addEventListener('click', () => grabFrame());
                document.getElementById('fpsBox').addEventListener('change', () => {
                  if (streaming) { stopStream(); startStream(); }
                });
                document.getElementById('fitSel').addEventListener('change', applyFit);

                function applyFit() {
                  const v = document.getElementById('fitSel').value;
                  const img = document.getElementById('screen');
                  img.classList.remove('fitWidth', 'fitNone');
                  if (v === 'width') img.classList.add('fitWidth');
                  else if (v === 'none') img.classList.add('fitNone');
                }

                function startStream() {
                  streaming = true;
                  setDot('live');
                  document.getElementById('streamBtn').textContent = 'Stop';
                  document.getElementById('streamBtn').classList.add('off');
                  const fps = Math.max(1, Math.min(30, parseInt(document.getElementById('fpsBox').value) || 4));
                  const interval = Math.floor(1000 / fps);
                  const tick = async () => {
                    if (!streaming) return;
                    await grabFrame();
                    streamTimer = setTimeout(tick, interval);
                  };
                  tick();
                }

                function stopStream() {
                  streaming = false;
                  setDot(true);
                  document.getElementById('streamBtn').textContent = 'Start';
                  document.getElementById('streamBtn').classList.remove('off');
                  if (streamTimer) { clearTimeout(streamTimer); streamTimer = null; }
                }

                async function grabFrame() {
                  const t0 = performance.now();
                  try {
                    const target = document.getElementById('qualSel').value === 'window' && curWinId ? 'window' : 'screen';
                    const body = target === 'window' ? { action: 'window' } : { action: 'screen' };
                    const r = await call('POST', '/capture', body);
                    const b64 = r && r.data && r.data.result;
                    if (b64 && b64.length > 100) {
                      const img = document.getElementById('screen');
                      img.src = 'data:image/png;base64,' + b64;
                      frameCount++;
                      lastFps++;
                      const dt = performance.now() - t0;
                      lastFrameAt = Date.now();
                      document.getElementById('frameStat').textContent = String(frameCount);
                      document.getElementById('latStat').textContent = dt.toFixed(0) + ' ms';
                    } else {
                      appendLog('capture', false, 'no data');
                    }
                  } catch (e) {
                    appendLog('capture', false, String(e));
                    if (streaming) stopStream();
                  }
                }

                // Mouse on the captured image. We translate click-coords on the displayed
                // image back to source-image pixels, then forward to /execute click-at,
                // using the currently-focused element (set by /find on window-select).
                function imgCoords(ev) {
                  const img = document.getElementById('screen');
                  if (!img.naturalWidth) return null;
                  const rect = img.getBoundingClientRect();
                  const sx = (ev.clientX - rect.left) / rect.width;
                  const sy = (ev.clientY - rect.top) / rect.height;
                  return {
                    x: Math.round(sx * img.naturalWidth),
                    y: Math.round(sy * img.naturalHeight),
                  };
                }

                document.getElementById('screen').addEventListener('mousemove', ev => {
                  const c = imgCoords(ev);
                  if (c) document.getElementById('curStat').textContent = c.x + ', ' + c.y;
                });

                async function onScreenClick(ev, button) {
                  const c = imgCoords(ev);
                  if (!c) return;
                  flashOverlay(ev);
                  const action = button === 'right' ? 'right-click' :
                                 button === 'double' ? 'double-click' : 'click-at';
                  try {
                    if (action === 'click-at') {
                      // click-at uses element-relative coords; with a window selected,
                      // the offset is from that window's top-left.
                      await call('POST', '/execute', { action: 'click-at', value: c.x + ',' + c.y });
                      appendLog('click-at', true, c.x + ',' + c.y);
                    } else {
                      await call('POST', '/execute', { action });
                      appendLog(action, true, '');
                    }
                  } catch (e) {
                    appendLog(action, false, String(e));
                  }
                }

                function flashOverlay(ev) {
                  const wrap = document.getElementById('viewportWrap');
                  const ov = document.getElementById('overlay');
                  const wrapRect = wrap.getBoundingClientRect();
                  ov.style.left = (ev.clientX - wrapRect.left + wrap.scrollLeft - 12) + 'px';
                  ov.style.top  = (ev.clientY - wrapRect.top  + wrap.scrollTop  - 12) + 'px';
                  ov.style.width = '24px';
                  ov.style.height = '24px';
                  ov.style.opacity = '1';
                  setTimeout(() => { ov.style.opacity = '0'; }, 250);
                }

                // Keyboard
                async function sendKeys(keys) {
                  try {
                    await call('POST', '/execute', { action: 'keys', value: keys });
                    appendLog('keys', true, keys);
                  } catch (e) {
                    appendLog('keys', false, String(e));
                  }
                }

                async function typeText() {
                  const txt = document.getElementById('typeBox').value;
                  if (!txt) return;
                  try {
                    await call('POST', '/execute', { action: 'type', value: txt });
                    appendLog('type', true, txt.length + ' chars');
                  } catch (e) {
                    appendLog('type', false, String(e));
                  }
                }

                // Forward physical key presses while viewport is focused
                document.addEventListener('keydown', ev => {
                  if (ev.target && (ev.target.tagName === 'INPUT' || ev.target.tagName === 'TEXTAREA' ||
                                    ev.target.tagName === 'SELECT')) return;
                  if (!curWin) return;
                  ev.preventDefault();
                  const mods = [];
                  if (ev.ctrlKey)  mods.push('Ctrl');
                  if (ev.altKey)   mods.push('Alt');
                  if (ev.shiftKey) mods.push('Shift');
                  let key = ev.key;
                  const named = {
                    'Enter':'{ENTER}','Escape':'{ESC}','Tab':'{TAB}','Backspace':'{BS}','Delete':'{DEL}',
                    'ArrowUp':'{UP}','ArrowDown':'{DOWN}','ArrowLeft':'{LEFT}','ArrowRight':'{RIGHT}',
                    'Home':'{HOME}','End':'{END}','PageUp':'{PGUP}','PageDown':'{PGDN}',
                    ' ':' '
                  };
                  if (named[key] !== undefined) key = named[key];
                  else if (key.length > 1) return; // ignore F-keys, etc, unless mapped
                  const combo = (mods.length ? mods.join('+') + '+' : '') + key;
                  sendKeys(combo);
                });

                // Logging
                function appendLog(action, ok, text) {
                  const div = document.createElement('div');
                  div.className = 'msg ' + (ok ? 'ok' : 'err');
                  const ts = document.createElement('span');
                  ts.className = 'ts';
                  ts.textContent = new Date().toLocaleTimeString() + ' ';
                  const sp = document.createElement('span');
                  sp.textContent = action + (text ? '  ' + text : '');
                  div.appendChild(ts);
                  div.appendChild(sp);
                  const log = document.getElementById('log');
                  log.insertBefore(div, log.firstChild);
                  while (log.children.length > 60) log.removeChild(log.lastChild);
                }

                // API key
                const API_KEY_STORAGE = 'apex_api_key';
                function getApiKey() { return localStorage.getItem(API_KEY_STORAGE) || ''; }
                (function initApiKey() {
                  const box  = document.getElementById('apiKeyBox');
                  const show = document.getElementById('apiKeyShow');
                  if (!box) return;
                  box.value = getApiKey();
                  box.addEventListener('input', () => localStorage.setItem(API_KEY_STORAGE, box.value.trim()));
                  show.addEventListener('click', () => {
                    box.type = box.type === 'password' ? 'text' : 'password';
                  });
                })();

                async function call(method, path, body) {
                  const sep = path.includes('?') ? '&' : '?';
                  const url = path + sep + 'format=json';
                  const opts = { method, headers: {} };
                  const key = getApiKey();
                  if (key) opts.headers['X-Api-Key'] = key;
                  if (body !== undefined && method !== 'GET') {
                    opts.headers['Content-Type'] = 'application/json';
                    opts.body = JSON.stringify(body);
                  }
                  const res = await fetch(url, opts);
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

    }
}
