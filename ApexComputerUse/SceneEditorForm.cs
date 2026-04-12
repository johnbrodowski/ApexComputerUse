using System.Drawing.Drawing2D;

namespace ApexComputerUse
{
    /// <summary>
    /// MSPaint-style WinForms editor for the layered scene system.
    /// Select, drag, resize shapes; add/remove/toggle layers; inspect/edit properties.
    /// </summary>
    public partial class SceneEditorForm : Form
    {
        // ── Dependencies ──────────────────────────────────────────────────
        private readonly SceneStore _store;

        // ── Scene state ───────────────────────────────────────────────────
        private Scene?      _scene;
        private string?     _curLayerId;
        private string?     _curShapeId;

        // ── Drag state ────────────────────────────────────────────────────
        private enum DragMode { None, Move, Place, Resize }
        private DragMode _dragMode = DragMode.None;
        private PointF   _dragStart;
        private AIDrawingCommand.ShapeCommand? _dragShapeSnapshot; // shape coords at drag start
        private int      _resizeHandle = -1;
        private string   _activeTool = "arrow";

        // ── View ──────────────────────────────────────────────────────────
        private float  _scale   = 1f;
        private PointF _offset  = PointF.Empty;

        // ── Render cache ──────────────────────────────────────────────────
        private Bitmap? _cacheBitmap;
        private bool    _cacheValid = false;

        // ── Live refresh tracking ─────────────────────────────────────────
        private string _lastSceneUpdate = "";
        private int    _lastSceneCount  = -1;

        // ── Active drawing color ──────────────────────────────────────────
        private Color _activeColor = Color.FromArgb(74, 144, 217);

        // ── Constructor ───────────────────────────────────────────────────
        public SceneEditorForm(SceneStore store)
        {
            _store = store;
            InitializeComponent();
        }

        // ── Load ──────────────────────────────────────────────────────────
        private void SceneEditorForm_Load(object sender, EventArgs e)
        {
            // Enable double-buffering on the canvas panel to eliminate flicker
            typeof(Panel).GetProperty("DoubleBuffered",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(canvasPanel, true);

            RefreshSceneList();
            refreshTimer.Start();
        }

        // ── Live refresh ──────────────────────────────────────────────────
        private void refreshTimer_Tick(object sender, EventArgs e)
        {
            // Refresh scene list if scenes were added/removed externally
            var scenes = _store.ListScenes();
            if (scenes.Length != _lastSceneCount)
            {
                _lastSceneCount = scenes.Length;
                var selected = lstScenes.SelectedItem as SceneListItem;
                RefreshSceneList();
                if (selected != null)
                    for (int i = 0; i < lstScenes.Items.Count; i++)
                        if (lstScenes.Items[i] is SceneListItem si && si.Scene.Id == selected.Scene.Id)
                        { lstScenes.SelectedIndex = i; break; }
            }

            // Skip heavy UI refresh while the user is dragging — only repaint canvas
            if (_dragMode != DragMode.None) return;

            if (_scene != null)
            {
                var fresh = _store.GetScene(_scene.Id);
                if (fresh != null && fresh.UpdatedAt != _lastSceneUpdate)
                {
                    _lastSceneUpdate = fresh.UpdatedAt;
                    _scene = fresh;
                    _cacheValid = false;
                    RefreshLayerList();
                    RefreshPropsPanel();
                    canvasPanel.Invalidate();
                }
            }
        }

        // ── Scene list ────────────────────────────────────────────────────
        private void RefreshSceneList()
        {
            var scenes = _store.ListScenes();
            lstScenes.Items.Clear();
            foreach (var s in scenes)
                lstScenes.Items.Add(new SceneListItem(s));
            btnDeleteScene.Enabled = false;
        }

        private void lstScenes_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (lstScenes.SelectedItem is not SceneListItem item) return;
            LoadScene(item.Scene.Id);
            btnDeleteScene.Enabled = true;
        }

        private void btnNewScene_Click(object sender, EventArgs e)
        {
            using var dlg = new NewSceneDialog();
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            var scene = _store.CreateScene(dlg.SceneName, dlg.SceneWidth, dlg.SceneHeight, dlg.Background);
            RefreshSceneList();
            // Select the newly created scene
            for (int i = 0; i < lstScenes.Items.Count; i++)
            {
                if (lstScenes.Items[i] is SceneListItem si && si.Scene.Id == scene.Id)
                {
                    lstScenes.SelectedIndex = i;
                    break;
                }
            }
        }

        private void btnDeleteScene_Click(object sender, EventArgs e)
        {
            if (_scene == null) return;
            if (MessageBox.Show($"Delete scene \"{_scene.Name}\"?", "Confirm",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            _store.DeleteScene(_scene.Id);
            _scene = null; _curLayerId = null; _curShapeId = null;
            _cacheValid = false;
            canvasPanel.Invalidate();
            RefreshSceneList();
            RefreshLayerList();
            RefreshPropsPanel();
        }

        // ── Load scene ────────────────────────────────────────────────────
        private void LoadScene(string sceneId)
        {
            _scene = _store.GetScene(sceneId);
            if (_scene == null) return;

            _curShapeId = null;
            _cacheValid = false;

            // Fit scene to canvas
            FitToCanvas();
            RefreshLayerList();
            RefreshPropsPanel();
            canvasPanel.Invalidate();

            Text = $"Scene Editor — {_scene.Name}";
            lblSceneInfo.Text = $"{_scene.Name}  {_scene.Width} × {_scene.Height}";

            // Select first layer
            if (_scene.Layers.Count > 0 && (_curLayerId == null || !_scene.Layers.Any(l => l.Id == _curLayerId)))
                _curLayerId = _scene.Layers.OrderBy(l => l.ZIndex).First().Id;
        }

        private void FitToCanvas()
        {
            if (_scene == null) return;
            float sx = (canvasPanel.Width  - 20f) / _scene.Width;
            float sy = (canvasPanel.Height - 20f) / _scene.Height;
            _scale  = Math.Min(sx, sy);
            _offset = new PointF(
                (canvasPanel.Width  - _scene.Width  * _scale) / 2f,
                (canvasPanel.Height - _scene.Height * _scale) / 2f);
        }

        // ── Layer list ────────────────────────────────────────────────────
        private void RefreshLayerList()
        {
            lstLayers.Items.Clear();
            if (_scene == null) return;
            foreach (var l in _scene.Layers.OrderByDescending(l => l.ZIndex))
                lstLayers.Items.Add(new LayerListItem(l));

            // Reselect active layer
            for (int i = 0; i < lstLayers.Items.Count; i++)
            {
                if (lstLayers.Items[i] is LayerListItem li && li.Layer.Id == _curLayerId)
                {
                    lstLayers.SelectedIndex = i;
                    return;
                }
            }
            if (lstLayers.Items.Count > 0)
                lstLayers.SelectedIndex = 0;
        }

        private void lstLayers_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (lstLayers.SelectedItem is LayerListItem li)
                _curLayerId = li.Layer.Id;
        }

        private void lstLayers_MouseDown(object sender, MouseEventArgs e)
        {
            if (_scene == null) return;
            int idx = lstLayers.IndexFromPoint(e.Location);
            if (idx < 0) return;
            // Click in the left ~28px = eye icon area → toggle visibility
            if (e.X < 28 && lstLayers.Items[idx] is LayerListItem li)
            {
                _store.UpdateLayer(_scene.Id, li.Layer.Id, null, !li.Layer.Visible, null, null, null);
                _scene = _store.GetScene(_scene.Id)!;
                _cacheValid = false;
                RefreshLayerList();
                canvasPanel.Invalidate();
            }
        }

        private void btnLayerUp_Click(object sender, EventArgs e)
        {
            if (_scene == null || _curLayerId == null) return;
            var ordered = _scene.Layers.OrderBy(l => l.ZIndex).ToList();
            int idx = ordered.FindIndex(l => l.Id == _curLayerId);
            if (idx < 0 || idx >= ordered.Count - 1) return;
            int za = ordered[idx].ZIndex, zb = ordered[idx + 1].ZIndex;
            _store.ReorderLayer(_scene.Id, ordered[idx].Id,     zb);
            _store.ReorderLayer(_scene.Id, ordered[idx + 1].Id, za);
            _scene = _store.GetScene(_scene.Id)!;
            _cacheValid = false;
            RefreshLayerList();
            canvasPanel.Invalidate();
        }

        private void btnLayerDown_Click(object sender, EventArgs e)
        {
            if (_scene == null || _curLayerId == null) return;
            var ordered = _scene.Layers.OrderBy(l => l.ZIndex).ToList();
            int idx = ordered.FindIndex(l => l.Id == _curLayerId);
            if (idx <= 0) return;
            int za = ordered[idx].ZIndex, zb = ordered[idx - 1].ZIndex;
            _store.ReorderLayer(_scene.Id, ordered[idx].Id,     zb);
            _store.ReorderLayer(_scene.Id, ordered[idx - 1].Id, za);
            _scene = _store.GetScene(_scene.Id)!;
            _cacheValid = false;
            RefreshLayerList();
            canvasPanel.Invalidate();
        }

        private void btnAddLayer_Click(object sender, EventArgs e)
        {
            if (_scene == null) return;
            string name = $"Layer {_scene.Layers.Count + 1}";
            var layer = _store.AddLayer(_scene.Id, name);
            _scene = _store.GetScene(_scene.Id)!;
            _curLayerId = layer.Id;
            RefreshLayerList();
        }

        private void btnDeleteLayer_Click(object sender, EventArgs e)
        {
            if (_scene == null || _curLayerId == null) return;
            if (_scene.Layers.Count <= 1)
            {
                MessageBox.Show("Cannot delete the last layer.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            _store.DeleteLayer(_scene.Id, _curLayerId);
            _scene = _store.GetScene(_scene.Id)!;
            _curLayerId = _scene.Layers.Count > 0 ? _scene.Layers.OrderBy(l => l.ZIndex).First().Id : null;
            _curShapeId = null;
            _cacheValid = false;
            RefreshLayerList();
            RefreshPropsPanel();
            canvasPanel.Invalidate();
        }

        // ── Tool selection ────────────────────────────────────────────────
        private void SetTool(string tool)
        {
            _activeTool = tool;
            foreach (var btn in new[] { btnToolArrow, btnToolRect, btnToolEllipse,
                                        btnToolCircle, btnToolLine, btnToolText })
                btn.FlatAppearance.BorderSize = 0;

            var active = tool switch
            {
                "arrow"   => btnToolArrow,
                "rect"    => btnToolRect,
                "ellipse" => btnToolEllipse,
                "circle"  => btnToolCircle,
                "line"    => btnToolLine,
                "text"    => btnToolText,
                _         => btnToolArrow
            };
            active.FlatAppearance.BorderSize = 2;
            active.FlatAppearance.BorderColor = Color.FromArgb(0, 153, 204);
        }

        private void btnFillColor_Click(object sender, EventArgs e)
        {
            using var dlg = new ColorDialog { Color = _activeColor, FullOpen = true };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            _activeColor = dlg.Color;
            btnFillColor.BackColor = _activeColor;

            // Apply to selected shape immediately
            var ss = GetSelectedSceneShape();
            if (ss != null && _scene != null && _curShapeId != null)
            {
                string? lid = FindLayerIdForShape(_curShapeId);
                if (lid != null)
                {
                    string hex = ColorToHex(_activeColor);
                    _store.PatchShapeStyle(_scene.Id, lid, _curShapeId, color: hex);
                    ss.Shape.Color = hex;
                    _cacheValid = false;
                    canvasPanel.Invalidate();
                    RefreshPropsPanel();
                }
            }
        }

        private static string ColorToHex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

        private void btnToolArrow_Click(object s, EventArgs e)   => SetTool("arrow");
        private void btnToolRect_Click(object s, EventArgs e)    => SetTool("rect");
        private void btnToolEllipse_Click(object s, EventArgs e) => SetTool("ellipse");
        private void btnToolCircle_Click(object s, EventArgs e)  => SetTool("circle");
        private void btnToolLine_Click(object s, EventArgs e)    => SetTool("line");
        private void btnToolText_Click(object s, EventArgs e)    => SetTool("text");
        private void btnDeleteShape_Click(object s, EventArgs e) => DeleteSelectedShape();

        // ── Canvas painting ───────────────────────────────────────────────
        private void canvasPanel_Paint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode      = SmoothingMode.AntiAlias;
            g.InterpolationMode  = InterpolationMode.HighQualityBicubic;

            if (_scene == null)
            {
                g.Clear(Color.FromArgb(30, 30, 30));
                using var tf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString("Select or create a scene", Font, Brushes.Gray,
                    new RectangleF(0, 0, canvasPanel.Width, canvasPanel.Height), tf);
                return;
            }

            g.Clear(Color.FromArgb(20, 20, 20));

            // Apply view transform
            g.TranslateTransform(_offset.X, _offset.Y);
            g.ScaleTransform(_scale, _scale);

            // Build or use cached bitmap
            if (!_cacheValid || _cacheBitmap == null ||
                _cacheBitmap.Width != _scene.Width || _cacheBitmap.Height != _scene.Height)
            {
                _cacheBitmap?.Dispose();
                _cacheBitmap = RenderSceneToBitmap();
                _cacheValid  = true;
            }

            g.DrawImageUnscaled(_cacheBitmap, 0, 0);

            // Draw selected shape on top (live during move/resize)
            var selSs = GetSelectedSceneShape();
            if (selSs != null && (_dragMode == DragMode.Move || _dragMode == DragMode.Resize))
            {
                using var selBmp = new Bitmap(_scene.Width, _scene.Height);
                using var sg = Graphics.FromImage(selBmp);
                sg.SmoothingMode = SmoothingMode.AntiAlias;
                AIDrawingCommand.RenderShapeTo(sg, selSs.Shape);
                g.DrawImageUnscaled(selBmp, 0, 0);
            }

            // Selection box
            if (selSs != null)
                DrawSelectionBox(g, selSs.Shape);
        }

        private Bitmap RenderSceneToBitmap()
        {
            // While moving/resizing, exclude the shape from the cache so it doesn't ghost
            string? excludeId = (_dragMode == DragMode.Move || _dragMode == DragMode.Resize) ? _curShapeId : null;

            var bmp = new Bitmap(_scene!.Width, _scene.Height);
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            using var bg = new SolidBrush(ParseColor(_scene.Background, Color.White));
            g.FillRectangle(bg, 0, 0, _scene.Width, _scene.Height);

            foreach (var layer in _scene.Layers.OrderBy(l => l.ZIndex))
            {
                if (!layer.Visible) continue;
                foreach (var ss in layer.Shapes.OrderBy(s => s.ZIndex))
                {
                    if (!ss.Visible) continue;
                    if (ss.Id == excludeId) continue;
                    AIDrawingCommand.RenderShapeTo(g, ss.Shape);
                }
            }
            return bmp;
        }

        private static Color ParseColor(string? spec, Color fallback)
        {
            if (string.IsNullOrWhiteSpace(spec)) return fallback;
            try { return ColorTranslator.FromHtml(spec); }
            catch { }
            try { return Color.FromName(spec); }
            catch { }
            return fallback;
        }

        private void DrawSelectionBox(Graphics g, AIDrawingCommand.ShapeCommand s)
        {
            var bb = ShapeBBox(s);
            if (bb == null) return;
            var rect = bb.Value;
            rect.Inflate(4, 4);

            using var pen = new Pen(Color.FromArgb(0, 230, 153), 1f / _scale);
            pen.DashPattern = new float[] { 4f / _scale, 3f / _scale };
            g.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);

            // Handles
            int hsz = (int)Math.Ceiling(8f / _scale);
            using var hBrush = new SolidBrush(Color.FromArgb(0, 230, 153));
            var corners = new PointF[]
            {
                new(rect.Left,             rect.Top),
                new(rect.Left + rect.Width/2, rect.Top),
                new(rect.Right,            rect.Top),
                new(rect.Left,             rect.Top + rect.Height/2),
                new(rect.Right,            rect.Top + rect.Height/2),
                new(rect.Left,             rect.Bottom),
                new(rect.Left + rect.Width/2, rect.Bottom),
                new(rect.Right,            rect.Bottom)
            };
            foreach (var pt in corners)
                g.FillRectangle(hBrush, pt.X - hsz/2f, pt.Y - hsz/2f, hsz, hsz);
        }

        // ── Mouse ─────────────────────────────────────────────────────────
        private void canvasPanel_MouseDown(object sender, MouseEventArgs e)
        {
            if (_scene == null || _curLayerId == null) return;
            var sc = ScreenToScene(e.Location);

            if (_activeTool == "arrow")
            {
                // 1. Check if clicking a resize handle on the selected shape
                int handle = _curShapeId != null ? HitTestHandle(sc) : -1;
                if (handle >= 0)
                {
                    var selForResize = GetSelectedSceneShape()!;
                    _dragMode          = DragMode.Resize;
                    _dragStart         = sc;
                    _resizeHandle      = handle;
                    _dragShapeSnapshot = CloneShape(selForResize.Shape);
                    _cacheValid        = false;
                    InvalidateCanvas(full: false);
                    return;
                }

                // 2. Check body hit
                var hit = HitTest(sc);
                if (hit != null)
                {
                    _curShapeId        = hit.Id;
                    _dragMode          = DragMode.Move;
                    _dragStart         = sc;
                    _dragShapeSnapshot = CloneShape(hit.Shape);
                    _cacheValid        = false;
                    InvalidateCanvas(full: false);
                }
                else
                {
                    _curShapeId = null;
                    InvalidateCanvas(full: false);
                }
                RefreshPropsPanel();
                return;
            }

            // Placing a new shape — start drag
            _dragMode  = DragMode.Place;
            _dragStart = sc;
            var newShape = MakeShape(_activeTool, sc);
            var layer = _scene.Layers.FirstOrDefault(l => l.Id == _curLayerId);
            if (layer == null) return;
            var ss = _store.AddShape(_scene.Id, _curLayerId, newShape, _activeTool);
            _scene = _store.GetScene(_scene.Id)!;
            _curShapeId = ss.Id;
            _cacheValid = false;
            canvasPanel.Invalidate();
            RefreshPropsPanel();
        }

        private void canvasPanel_MouseMove(object sender, MouseEventArgs e)
        {
            if (_scene == null) return;
            var sc = ScreenToScene(e.Location);
            lblCursor.Text = $"x: {sc.X:0}  y: {sc.Y:0}";

            // Update cursor based on hover / active drag
            if (_dragMode == DragMode.Resize)
                canvasPanel.Cursor = HandleCursor(_resizeHandle);
            else if (_dragMode == DragMode.None && _activeTool == "arrow" && _curShapeId != null)
                canvasPanel.Cursor = HitTestHandle(sc) is int h && h >= 0 ? HandleCursor(h) : Cursors.Default;
            else
                canvasPanel.Cursor = Cursors.Default;

            if (_dragMode == DragMode.Move && _dragShapeSnapshot != null && _curShapeId != null)
            {
                float dx = sc.X - _dragStart.X;
                float dy = sc.Y - _dragStart.Y;
                var ss = GetSelectedSceneShape();
                if (ss == null) return;
                var s = ss.Shape;
                s.X = (_dragShapeSnapshot.X) + dx;
                s.Y = (_dragShapeSnapshot.Y) + dy;
                if (_dragShapeSnapshot.X2 != 0 || _dragShapeSnapshot.Y2 != 0)
                {
                    s.X2 = _dragShapeSnapshot.X2 + dx;
                    s.Y2 = _dragShapeSnapshot.Y2 + dy;
                }
                if (_dragShapeSnapshot.Points != null)
                {
                    var pts = (float[])_dragShapeSnapshot.Points.Clone();
                    for (int i = 0; i < pts.Length - 1; i += 2) { pts[i] += dx; pts[i+1] += dy; }
                    s.Points = pts;
                }
                canvasPanel.Invalidate();
            }
            else if (_dragMode == DragMode.Resize && _dragShapeSnapshot != null && _curShapeId != null)
            {
                var ss = GetSelectedSceneShape();
                if (ss == null) return;
                ApplyHandleResize(ss.Shape, _dragShapeSnapshot, _resizeHandle, _dragStart, sc);
                _cacheValid = false;
                canvasPanel.Invalidate();
            }
            else if (_dragMode == DragMode.Place && _curShapeId != null)
            {
                var ss = GetSelectedSceneShape();
                if (ss == null) return;
                ResizeShapeDrag(ss.Shape, _dragStart, sc);
                _cacheValid = false;
                canvasPanel.Invalidate();
            }
        }

        private void canvasPanel_MouseUp(object sender, MouseEventArgs e)
        {
            if (_scene == null) return;
            var sc = ScreenToScene(e.Location);

            if (_dragMode == DragMode.Resize && _curShapeId != null)
            {
                var ss = GetSelectedSceneShape();
                if (ss != null && _dragShapeSnapshot != null)
                {
                    ApplyHandleResize(ss.Shape, _dragShapeSnapshot, _resizeHandle, _dragStart, sc);
                    var s = ss.Shape;
                    string? lid = FindLayerIdForShape(_curShapeId);
                    if (lid != null)
                        _store.PatchShapeGeometry(_scene.Id, lid, _curShapeId,
                            s.X, s.Y,
                            (s.X2 != 0 || s.Y2 != 0) ? s.X2 : null,
                            (s.X2 != 0 || s.Y2 != 0) ? s.Y2 : null,
                            s.W > 0 ? s.W : null, s.H > 0 ? s.H : null, null, r: s.R > 0 ? s.R : null);
                }
                _dragMode = DragMode.None; _dragShapeSnapshot = null; _resizeHandle = -1;
                _cacheValid = false;
                canvasPanel.Invalidate();
                RefreshPropsPanel();
                return;
            }

            if (_dragMode == DragMode.Move && _curShapeId != null)
            {
                var ss = GetSelectedSceneShape();
                if (ss != null)
                {
                    var s = ss.Shape;
                    _store.PatchShapeGeometry(_scene.Id, FindLayerIdForShape(_curShapeId)!, _curShapeId,
                        s.X, s.Y,
                        (s.X2 != 0 || s.Y2 != 0) ? s.X2 : null,
                        (s.X2 != 0 || s.Y2 != 0) ? s.Y2 : null,
                        null, null, s.Points);
                }
                _dragMode = DragMode.None; _dragShapeSnapshot = null;
                _cacheValid = false;
                canvasPanel.Invalidate();
                RefreshPropsPanel();
                return;
            }

            if (_dragMode == DragMode.Place && _curShapeId != null)
            {
                var ss = GetSelectedSceneShape();
                if (ss != null)
                {
                    ResizeShapeDrag(ss.Shape, _dragStart, sc);
                    var s = ss.Shape;
                    _store.PatchShapeGeometry(_scene.Id, _curLayerId!, _curShapeId,
                        s.X, s.Y,
                        (s.X2 != 0 || s.Y2 != 0) ? s.X2 : null,
                        (s.X2 != 0 || s.Y2 != 0) ? s.Y2 : null,
                        s.W > 0 ? s.W : null, s.H > 0 ? s.H : null, null);
                }
                _dragMode = DragMode.None;
                _cacheValid = false;
                SetTool("arrow");
                canvasPanel.Invalidate();
                RefreshPropsPanel();
            }
        }

        // ── Resize handle hit-test & apply ────────────────────────────────
        private int HitTestHandle(PointF sc)
        {
            var selSs = GetSelectedSceneShape();
            if (selSs == null) return -1;
            var s = selSs.Shape;
            float tol = Math.Max(6f, 8f / _scale);

            // Lines/arrows: two endpoint handles only
            if (s.Type is "line" or "arrow")
            {
                if (Dist(sc, new PointF(s.X,  s.Y))  <= tol) return 0;
                if (Dist(sc, new PointF(s.X2, s.Y2)) <= tol) return 7;
                return -1;
            }

            var bb = ShapeBBox(s);
            if (bb == null) return -1;
            var rect = bb.Value;
            rect.Inflate(4, 4);
            var handles = HandlePositions(rect);
            for (int i = 0; i < handles.Length; i++)
                if (Dist(sc, handles[i]) <= tol) return i;
            return -1;
        }

        private static PointF[] HandlePositions(RectangleF r) =>
        [
            new(r.Left,              r.Top),              // 0 top-left
            new(r.Left + r.Width/2,  r.Top),              // 1 top-center
            new(r.Right,             r.Top),              // 2 top-right
            new(r.Left,              r.Top + r.Height/2), // 3 mid-left
            new(r.Right,             r.Top + r.Height/2), // 4 mid-right
            new(r.Left,              r.Bottom),            // 5 bot-left
            new(r.Left + r.Width/2,  r.Bottom),            // 6 bot-center
            new(r.Right,             r.Bottom)             // 7 bot-right
        ];

        private static void ApplyHandleResize(AIDrawingCommand.ShapeCommand s,
            AIDrawingCommand.ShapeCommand snap, int handle, PointF start, PointF cur)
        {
            float dx = cur.X - start.X, dy = cur.Y - start.Y;
            switch (s.Type)
            {
                case "rect":
                case "ellipse":
                {
                    float x = snap.X, y = snap.Y, w = snap.W, h = snap.H;
                    switch (handle)
                    {
                        case 0: x += dx; y += dy; w -= dx; h -= dy; break; // top-left
                        case 1:          y += dy;           h -= dy; break; // top-center
                        case 2:          y += dy; w += dx;  h -= dy; break; // top-right
                        case 3: x += dx;          w -= dx;           break; // mid-left
                        case 4:                   w += dx;           break; // mid-right
                        case 5: x += dx;          w -= dx;  h += dy; break; // bot-left
                        case 6:                             h += dy; break; // bot-center
                        case 7:                   w += dx;  h += dy; break; // bot-right
                    }
                    s.X = x; s.Y = y;
                    s.W = Math.Max(4, w); s.H = Math.Max(4, h);
                    break;
                }
                case "circle":
                {
                    // Distance from centre to current mouse = new radius
                    float dist = Dist(cur, new PointF(snap.X, snap.Y));
                    s.R = Math.Max(4, dist);
                    break;
                }
                case "line":
                case "arrow":
                    if (handle == 0) { s.X  = snap.X  + dx; s.Y  = snap.Y  + dy; }
                    else             { s.X2 = snap.X2 + dx; s.Y2 = snap.Y2 + dy; }
                    break;
            }
        }

        private static float Dist(PointF a, PointF b) =>
            MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

        private static Cursor HandleCursor(int handle) => handle switch
        {
            0 or 7 => Cursors.SizeNWSE,  // top-left / bottom-right
            2 or 5 => Cursors.SizeNESW,  // top-right / bottom-left
            1 or 6 => Cursors.SizeNS,    // top / bottom edge
            3 or 4 => Cursors.SizeWE,    // left / right edge
            _      => Cursors.SizeAll
        };

        private void canvasPanel_MouseWheel(object sender, MouseEventArgs e)
        {
            float factor = e.Delta > 0 ? 1.1f : 1f / 1.1f;
            _scale = Math.Clamp(_scale * factor, 0.05f, 20f);
            canvasPanel.Invalidate();
        }

        private void canvasPanel_Resize(object sender, EventArgs e)
        {
            FitToCanvas();
            canvasPanel.Invalidate();
        }

        // ── Keyboard ──────────────────────────────────────────────────────
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Delete || keyData == Keys.Back)
            {
                DeleteSelectedShape();
                return true;
            }
            if (keyData == Keys.Escape)
            {
                _curShapeId = null; InvalidateCanvas(full: false); RefreshPropsPanel(); return true;
            }
            if (keyData == (Keys.Control | Keys.S)) { /* already persisted */ return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void DeleteSelectedShape()
        {
            if (_scene == null || _curShapeId == null) return;
            string? lid = FindLayerIdForShape(_curShapeId);
            if (lid == null) return;
            _store.DeleteShape(_scene.Id, lid, _curShapeId);
            _scene = _store.GetScene(_scene.Id)!;
            _curShapeId = null;
            _cacheValid = false;
            canvasPanel.Invalidate();
            RefreshPropsPanel();
        }

        // ── Properties panel ──────────────────────────────────────────────
        private void RefreshPropsPanel()
        {
            pnlProps.Controls.Clear();
            var ss = GetSelectedSceneShape();
            if (ss == null)
            {
                lblSelected.Text = "nothing selected";
                return;
            }
            var s = ss.Shape;
            lblSelected.Text = $"selected: {s.Type}";

            // Sync toolbar color button to selected shape's color
            if (!string.IsNullOrEmpty(s.Color))
                try { _activeColor = ColorTranslator.FromHtml(s.Color); btnFillColor.BackColor = _activeColor; } catch { }

            // Geometry fields
            AddPropField("x",  s.X.ToString("0"),       v => { s.X = float.Parse(v); CommitGeometry(s); });
            AddPropField("y",  s.Y.ToString("0"),       v => { s.Y = float.Parse(v); CommitGeometry(s); });
            if (s.W != 0) AddPropField("w", s.W.ToString("0"), v => { s.W = float.Parse(v); CommitGeometry(s); });
            if (s.H != 0) AddPropField("h", s.H.ToString("0"), v => { s.H = float.Parse(v); CommitGeometry(s); });
            if (s.R != 0) AddPropField("r", s.R.ToString("0"), v => { s.R = float.Parse(v); CommitGeometry(s); });
            if (s.X2 != 0 || s.Y2 != 0)
            {
                AddPropField("x2", s.X2.ToString("0"), v => { s.X2 = float.Parse(v); CommitGeometry(s); });
                AddPropField("y2", s.Y2.ToString("0"), v => { s.Y2 = float.Parse(v); CommitGeometry(s); });
            }

            // Color field
            AddColorField(s);
        }

        private void AddColorField(AIDrawingCommand.ShapeCommand s)
        {
            Color current;
            try { current = ColorTranslator.FromHtml(s.Color ?? "#4a90d9"); } catch { current = Color.Gray; }

            var row = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = true, Margin = new Padding(0, 6, 0, 0),
                Width = pnlProps.Width - 8
            };
            var lbl = new Label
            {
                Text = "color", Width = 36, TextAlign = ContentAlignment.MiddleRight,
                Font = new Font("Consolas", 8f), ForeColor = Color.Gray
            };
            var swatch = new Button
            {
                BackColor = current, Width = 48, Height = 20,
                FlatStyle = FlatStyle.Flat, Text = "", Cursor = Cursors.Hand
            };
            swatch.FlatAppearance.BorderColor = Color.FromArgb(120, 120, 120);
            swatch.FlatAppearance.BorderSize = 1;

            var hexLbl = new Label
            {
                Text = s.Color ?? "", Width = 62,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Consolas", 8f), ForeColor = Color.Gray
            };

            swatch.Click += (_, __) =>
            {
                using var dlg = new ColorDialog { Color = swatch.BackColor, FullOpen = true };
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                _activeColor = dlg.Color;
                btnFillColor.BackColor = _activeColor;
                swatch.BackColor = _activeColor;
                string hex = ColorToHex(_activeColor);
                hexLbl.Text = hex;
                s.Color = hex;
                string? lid = _curShapeId != null ? FindLayerIdForShape(_curShapeId) : null;
                if (lid != null && _curShapeId != null && _scene != null)
                    _store.PatchShapeStyle(_scene.Id, lid, _curShapeId, color: hex);
                _cacheValid = false;
                canvasPanel.Invalidate();
            };

            row.Controls.Add(lbl);
            row.Controls.Add(swatch);
            row.Controls.Add(hexLbl);
            pnlProps.Controls.Add(row);
        }

        private void AddPropField(string label, string value, Action<string> onChange)
        {
            var row   = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight,
                                              AutoSize = true, Margin = new Padding(0,2,0,0), Width = pnlProps.Width - 8 };
            var lbl   = new Label { Text = label, Width = 28, TextAlign = ContentAlignment.MiddleRight,
                                    Font = new Font("Consolas", 8f), ForeColor = Color.Gray };
            var txt   = new TextBox { Text = value, Width = 70, BackColor = Color.FromArgb(60,60,60),
                                      ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle,
                                      Font = new Font("Consolas", 8f) };
            txt.Leave += (_, __) =>
            {
                try { onChange(txt.Text); _cacheValid = false; canvasPanel.Invalidate(); RefreshPropsPanel(); }
                catch { }
            };
            row.Controls.Add(lbl);
            row.Controls.Add(txt);
            pnlProps.Controls.Add(row);
        }

        private void CommitGeometry(AIDrawingCommand.ShapeCommand s)
        {
            if (_scene == null || _curShapeId == null) return;
            string? lid = FindLayerIdForShape(_curShapeId);
            if (lid == null) return;
            _store.PatchShapeGeometry(_scene.Id, lid, _curShapeId,
                s.X, s.Y,
                (s.X2 != 0 || s.Y2 != 0) ? s.X2 : null,
                (s.X2 != 0 || s.Y2 != 0) ? s.Y2 : null,
                s.W > 0 ? s.W : null, s.H > 0 ? s.H : null, null);
        }

        // ── Helpers ───────────────────────────────────────────────────────
        private PointF ScreenToScene(Point p) =>
            new((p.X - _offset.X) / _scale, (p.Y - _offset.Y) / _scale);

        private SceneShape? HitTest(PointF sc)
        {
            if (_scene == null) return null;
            foreach (var layer in _scene.Layers.OrderByDescending(l => l.ZIndex))
            {
                if (!layer.Visible) continue;
                foreach (var ss in layer.Shapes.OrderByDescending(s => s.ZIndex))
                {
                    if (!ss.Visible) continue;
                    var bb = ShapeBBox(ss.Shape);
                    if (bb != null && bb.Value.Contains(sc)) return ss;
                }
            }
            return null;
        }

        private SceneShape? GetSelectedSceneShape()
        {
            if (_scene == null || _curShapeId == null) return null;
            foreach (var l in _scene.Layers)
                foreach (var ss in l.Shapes)
                    if (ss.Id == _curShapeId) return ss;
            return null;
        }

        private string? FindLayerIdForShape(string shapeId)
        {
            if (_scene == null) return null;
            foreach (var l in _scene.Layers)
                if (l.Shapes.Any(s => s.Id == shapeId)) return l.Id;
            return null;
        }

        private static RectangleF? ShapeBBox(AIDrawingCommand.ShapeCommand s) =>
            s.Type switch
            {
                "rect"    or "ellipse" => new RectangleF(s.X, s.Y, s.W > 0 ? s.W : 1, s.H > 0 ? s.H : 1),
                "circle"               => new RectangleF(s.X - s.R, s.Y - s.R, s.R * 2, s.R * 2),
                "line"    or "arrow"   => new RectangleF(
                                              Math.Min(s.X, s.X2), Math.Min(s.Y, s.Y2),
                                              Math.Max(1, Math.Abs(s.X2 - s.X)), Math.Max(1, Math.Abs(s.Y2 - s.Y))),
                "text"                 => new RectangleF(s.X, s.Y, (s.Text?.Length ?? 4) * (s.FontSize > 0 ? s.FontSize : 14) * 0.6f, (s.FontSize > 0 ? s.FontSize : 14) + 4),
                "polygon" when s.Points != null && s.Points.Length >= 4 => PolygonBBox(s.Points),
                _                      => null
            };

        private static RectangleF PolygonBBox(float[] pts)
        {
            float minX = pts[0], maxX = pts[0], minY = pts[1], maxY = pts[1];
            for (int i = 0; i < pts.Length - 1; i += 2)
            {
                if (pts[i]   < minX) minX = pts[i];
                if (pts[i]   > maxX) maxX = pts[i];
                if (pts[i+1] < minY) minY = pts[i+1];
                if (pts[i+1] > maxY) maxY = pts[i+1];
            }
            return new RectangleF(minX, minY, Math.Max(1, maxX - minX), Math.Max(1, maxY - minY));
        }

        private AIDrawingCommand.ShapeCommand MakeShape(string tool, PointF at)
        {
            string hex = ColorToHex(_activeColor);
            return tool switch
            {
                "rect"    => new AIDrawingCommand.ShapeCommand { Type = "rect",    X = at.X, Y = at.Y, W = 80, H = 50, Color = hex, Fill = true },
                "ellipse" => new AIDrawingCommand.ShapeCommand { Type = "ellipse", X = at.X, Y = at.Y, W = 80, H = 50, Color = hex, Fill = true },
                "circle"  => new AIDrawingCommand.ShapeCommand { Type = "circle",  X = at.X, Y = at.Y, R = 40, Color = hex, Fill = true },
                "line"    => new AIDrawingCommand.ShapeCommand { Type = "line",    X = at.X, Y = at.Y, X2 = at.X + 80, Y2 = at.Y, Color = hex, StrokeWidth = 2 },
                "text"    => new AIDrawingCommand.ShapeCommand { Type = "text",    X = at.X, Y = at.Y, Text = "Text", FontSize = 16, Color = hex },
                _         => new AIDrawingCommand.ShapeCommand { Type = "rect",    X = at.X, Y = at.Y, W = 80, H = 50, Color = hex, Fill = true }
            };
        }

        private static void ResizeShapeDrag(AIDrawingCommand.ShapeCommand s, PointF start, PointF end)
        {
            float dx = end.X - start.X, dy = end.Y - start.Y;
            switch (s.Type)
            {
                case "rect": case "ellipse":
                    s.W = Math.Max(4, Math.Abs(dx)); s.H = Math.Max(4, Math.Abs(dy));
                    if (dx < 0) s.X = start.X + dx;
                    if (dy < 0) s.Y = start.Y + dy;
                    break;
                case "circle":
                    s.R = Math.Max(4, MathF.Sqrt(dx*dx + dy*dy));
                    break;
                case "line": case "arrow":
                    s.X2 = end.X; s.Y2 = end.Y;
                    break;
            }
        }

        private static AIDrawingCommand.ShapeCommand CloneShape(AIDrawingCommand.ShapeCommand s) =>
            new()
            {
                Type = s.Type, X = s.X, Y = s.Y, X2 = s.X2, Y2 = s.Y2,
                W = s.W, H = s.H, R = s.R,
                Points = s.Points != null ? (float[])s.Points.Clone() : null,
                Color = s.Color, Fill = s.Fill, StrokeWidth = s.StrokeWidth,
                Opacity = s.Opacity, Text = s.Text, FontSize = s.FontSize
            };

        private void InvalidateCanvas(bool full)
        {
            if (full) _cacheValid = false;
            canvasPanel.Invalidate();
        }
    }

    // ── List item wrappers ────────────────────────────────────────────────

    internal sealed class SceneListItem
    {
        public Scene Scene { get; }
        public SceneListItem(Scene s) => Scene = s;
        public override string ToString() => $"{Scene.Name}  ({Scene.Width}×{Scene.Height})";
    }

    internal sealed class LayerListItem
    {
        public Layer Layer { get; }
        public LayerListItem(Layer l) => Layer = l;
        public override string ToString() => (Layer.Visible ? "👁 " : "   ") + Layer.Name;
    }

    // ── New scene dialog ──────────────────────────────────────────────────

    internal sealed class NewSceneDialog : Form
    {
        public string SceneName  { get; private set; } = "Untitled";
        public int    SceneWidth  { get; private set; } = 800;
        public int    SceneHeight { get; private set; } = 600;
        public string Background  { get; private set; } = "white";

        public NewSceneDialog()
        {
            Text            = "New Scene";
            ClientSize      = new Size(300, 180);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox     = false;
            MinimizeBox     = false;
            StartPosition   = FormStartPosition.CenterParent;

            var tbl = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 5,
                                              Padding = new Padding(10) };
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            var txtName = new TextBox { Text = "Untitled", Dock = DockStyle.Fill };
            var nudW    = new NumericUpDown { Minimum = 1, Maximum = 8192, Value = 800, Dock = DockStyle.Fill };
            var nudH    = new NumericUpDown { Minimum = 1, Maximum = 8192, Value = 600, Dock = DockStyle.Fill };
            var txtBg   = new TextBox { Text = "white", Dock = DockStyle.Fill };

            void AddRow(string lbl, Control ctrl, int row)
            {
                tbl.Controls.Add(new Label { Text = lbl, TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, row);
                tbl.Controls.Add(ctrl, 1, row);
            }
            AddRow("Name",       txtName, 0);
            AddRow("Width",      nudW,    1);
            AddRow("Height",     nudH,    2);
            AddRow("Background", txtBg,   3);

            var btnOk     = new Button { Text = "OK",     DialogResult = DialogResult.OK,     Dock = DockStyle.Fill };
            var btnCancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Dock = DockStyle.Fill };
            var btnRow    = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft,
                                                  Dock = DockStyle.Fill, AutoSize = true };
            btnRow.Controls.Add(btnCancel);
            btnRow.Controls.Add(btnOk);
            tbl.Controls.Add(btnRow, 0, 4);
            tbl.SetColumnSpan(btnRow, 2);

            Controls.Add(tbl);
            AcceptButton = btnOk;
            CancelButton = btnCancel;

            btnOk.Click += (_, __) =>
            {
                SceneName  = txtName.Text.Trim().Length > 0 ? txtName.Text.Trim() : "Untitled";
                SceneWidth  = (int)nudW.Value;
                SceneHeight = (int)nudH.Value;
                Background  = txtBg.Text.Trim().Length > 0 ? txtBg.Text.Trim() : "white";
            };
        }
    }
}
