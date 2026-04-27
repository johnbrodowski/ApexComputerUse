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
        // â”€â”€ Editor page â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private static async Task ServeEditorPage(HttpListenerResponse res)
        {
            const string html = """
                <!DOCTYPE html>
                <html lang="en">
                <head>
                <meta charset="utf-8">
                <title>Scene Editor â€” ApexComputerUse</title>
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
                    <div class="ph"><span>Scenes</span><button onclick="loadScenes()" title="Refresh">â†º</button></div>
                    <select class="lst" id="sceneList" size="10" onchange="onSceneSelect()"></select>
                    <div class="btnrow">
                      <input id="newSceneName" placeholder="scene nameâ€¦">
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
                      <span id="sCursor">x: â€” y: â€”</span>
                      <span id="sSelected">nothing selected</span>
                      <span id="sScene"></span>
                    </div>
                  </div>
                </main>

                <script>
                'use strict';

                // â”€â”€ State â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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

                // â”€â”€ Boot â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
                (async () => { await loadScenes(); })();

                // â”€â”€ Scene management â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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
                  document.getElementById('sScene').textContent   = scene.width + ' Ã— ' + scene.height;
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

                // â”€â”€ Layer management â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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

                // â”€â”€ Shape helpers â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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

                // â”€â”€ Canvas drawing â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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

                // â”€â”€ Mouse events â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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

                // â”€â”€ Tool buttons â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
                function setTool(t) {
                  tool = t;
                  document.querySelectorAll('#toolbar button.sm').forEach(b => b.classList.remove('on'));
                  const btn = document.getElementById('tool' + t.charAt(0).toUpperCase() + t.slice(1));
                  if (btn) btn.classList.add('on');
                }

                // â”€â”€ Properties panel â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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
                    ${sel.type==='arc' ? '<div class="prop"><label>startÂ°</label><input type="number" value="'+(sel.start_angle??0)+'" onchange="patchProp(\'start_angle\',+this.value)"></div>' : ''}
                    ${sel.type==='arc' ? '<div class="prop"><label>sweepÂ°</label><input type="number" value="'+(sel.sweep_angle??90)+'" onchange="patchProp(\'sweep_angle\',+this.value)"></div>' : ''}
                    <div class="prop"><label>rotateÂ°</label><input type="number" value="${sel.rotation??0}" onchange="patchProp('rotation',+this.value)"></div>`;
                }

                async function patchProp(field, val) {
                  const ss = curShapeId ? findShape(curShapeId) : null;
                  if (!ss) return;
                  ss.shape[field] = val;
                  draw();
                  await api('PATCH', '/scenes/' + curScene.id + '/layers/' + ss.layer.id + '/shapes/' + curShapeId, { [field]: val });
                }

                // â”€â”€ Keyboard â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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

                // â”€â”€ API helper â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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

    }
}
