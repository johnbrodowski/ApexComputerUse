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
        // â”€â”€ Test page â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

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

                  /* â”€â”€ Header â”€â”€ */
                  header { background: #252526; border-bottom: 1px solid #3c3c3c;
                           padding: .35em 1em; display: flex; align-items: center; gap: .75em;
                           flex-shrink: 0; }
                  #dot { width: 8px; height: 8px; border-radius: 50%; background: #555; flex-shrink: 0; }
                  #dot.ok  { background: #4ec94e; }
                  #dot.err { background: #e05252; }
                  #statusTxt { font-size: .78em; color: #888; }
                  header span.spacer { flex: 1; }
                  header span.brand  { font-size: .82em; color: #9cdcfe; }

                  /* â”€â”€ Main 2-col layout â”€â”€ */
                  main { display: grid; grid-template-columns: 270px 1fr; flex: 1; overflow: hidden; min-height: 0; }

                  /* â”€â”€ Left sidebar â”€â”€ */
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

                  /* â”€â”€ Right panel â”€â”€ */
                  section { display: flex; flex-direction: column; overflow: hidden; min-height: 0; }

                  #selInfo { padding: .3em .8em; font-size: .75em; color: #9cdcfe; background: #252526;
                             border-bottom: 1px solid #3c3c3c; white-space: nowrap; overflow: hidden;
                             text-overflow: ellipsis; flex-shrink: 0; min-height: 1.8em; }

                  /* â”€â”€ Command builder â”€â”€ */
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

                  /* â”€â”€ Format bar â”€â”€ */
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

                  /* â”€â”€ Output â”€â”€ */
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
                  <span id="statusTxt">connectingâ€¦</span>
                  <span class="spacer"></span>
                  <label style="font-size:.75em;color:#9cdcfe;">API Key
                    <input id="apiKeyBox" type="password" placeholder="paste keyâ€¦" autocomplete="off"
                           style="background:#3c3c3c;color:#d4d4d4;border:1px solid #555;border-radius:2px;
                                  padding:.15em .3em;font:inherit;font-size:.76em;width:140px;margin-left:.3em;">
                    <button type="button" id="apiKeyShow" title="Show / hide"
                            style="background:none;border:none;color:#888;cursor:pointer;font-size:1em;padding:0 .2em;">ðŸ‘</button>
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
                  <!-- â”€â”€ Left sidebar â”€â”€ -->
                  <aside>
                    <div class="panel wins">
                      <div class="phead">
                        <span>Windows</span>
                        <button onclick="loadWindows()" title="Refresh">â†º</button>
                      </div>
                      <select class="lst" id="winList" size="8" onchange="onWinSelect()"></select>
                    </div>

                    <div class="panel elems">
                      <div class="phead">
                        <span>Elements</span>
                        <button onclick="loadElements()" title="Refresh">â†º</button>
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

                  <!-- â”€â”€ Right panel â”€â”€ -->
                  <section>
                    <div id="selInfo">No element selected â€” pick a window to start</div>

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
                          <button class="a" onclick="act('scroll-up')">â†‘</button>
                          <button class="a" onclick="act('scroll-down')">â†“</button>
                          <button class="a" onclick="act('scroll-left')">â†</button>
                          <button class="a" onclick="act('scroll-right')">â†’</button>
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
                          <button class="a cap" onclick="doDraw()" title="POST /draw â€” Value field must be a JSON DrawRequest (shapes array). Canvas: blank|white|black|screen|window|element">draw</button>
                          <button class="a cap" onclick="doDrawDemo()" title="GET /draw/demo â€” renders the built-in space scene. Add ?overlay=true to also show on screen.">demo</button>
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
                        <textarea id="val" rows="1" placeholder="(optional â€” text, x,y, JSON, â€¦)  Ctrl+Enter to execute"
                                  onkeydown="if(event.key==='Enter'&&(event.ctrlKey||event.metaKey)){event.preventDefault();doExec();}"
                                  oninput="this.rows=Math.min(12,Math.max(1,this.value.split('\n').length))"></textarea>
                        <button id="go" onclick="doExec()">â–¶ Execute</button>
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

                // â”€â”€ Boot â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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

                // â”€â”€ Windows â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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

                // â”€â”€ Elements â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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

                // â”€â”€ Command builder â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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

                // â”€â”€ Capture & OCR â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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

                // â”€â”€ UI Map â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
                async function doUiMap() {
                  appendLog('uimap', true, 'Rendering UI map\u2026');
                  try {
                    const r = await call('GET', '/uimap');
                    logCapture(r, 'uimap');
                  } catch (e) {
                    appendLog('uimap', false, String(e));
                  }
                }

                // â”€â”€ Draw demo â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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

                // â”€â”€ Draw â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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

                // â”€â”€ AI Vision â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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

                // â”€â”€ Logging â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
                function log(r) {
                  const ok  = r && r.success;
                  const div = msgDiv(ok);
                  const ts  = document.createElement('div');
                  ts.className = 'ts';
                  ts.textContent = now() + ' ' + (r && r.action ? r.action : '');
                  div.appendChild(ts);
                  if (r._isPdf && r.data?.result) {
                    // PDF â€” show open link (blob URL)
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

                // â”€â”€ API key (persistent via localStorage) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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
                        status.textContent = `TestRunner PID ${pid} started (${mode}) â€” bridge will restart mid-run`;
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

                // â”€â”€ Format bar â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
                function onFmtChange() {
                  const fmt = document.getElementById('fmtSel').value;
                  const ext = fmt === 'json' ? 'json' : fmt;
                  document.getElementById('lnkHelp').href    = withKey('/help.'    + ext);
                  document.getElementById('lnkStatus').href  = withKey('/status.'  + ext);
                  document.getElementById('lnkWindows').href = withKey('/windows.' + ext);
                }

                // â”€â”€ API â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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

    }
}
