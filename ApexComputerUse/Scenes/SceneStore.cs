using System.Text.Json;

namespace ApexComputerUse
{
    /// <summary>
    /// Thread-safe, in-memory store for <see cref="Scene"/> objects with automatic
    /// JSON persistence to &lt;exe&gt;/scenes/{id}.json.
    ///
    /// Construct once (in Form1) and inject into both HttpCommandServer and CommandProcessor.
    /// </summary>
    public sealed class SceneStore
    {
        // ── Storage paths ─────────────────────────────────────────────────

        private static readonly string DefaultScenesDir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                         "ApexComputerUse", "scenes");

        private readonly string _scenesDir;

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            WriteIndented            = true,
            PropertyNameCaseInsensitive = true
        };

        // ── In-memory state ───────────────────────────────────────────────

        private readonly Dictionary<string, Scene> _scenes = new();
        private readonly object _lock = new();

        // ── Lifecycle ─────────────────────────────────────────────────────

        public SceneStore(string? scenesDir = null)
        {
            _scenesDir = scenesDir ?? DefaultScenesDir;
            MigrateFromExeDirIfNeeded();
            Directory.CreateDirectory(_scenesDir);
            LoadAllFromDisk();
        }

        private void MigrateFromExeDirIfNeeded()
        {
            if (Directory.Exists(_scenesDir) && Directory.EnumerateFiles(_scenesDir, "*.json").Any()) return;
            string legacy = Path.Combine(AppContext.BaseDirectory, "scenes");
            if (!Directory.Exists(legacy)) return;
            Directory.CreateDirectory(_scenesDir);
            foreach (string file in Directory.GetFiles(legacy, "*.json"))
                File.Copy(file, Path.Combine(_scenesDir, Path.GetFileName(file)), overwrite: false);
        }

        // ── Scene CRUD ────────────────────────────────────────────────────

        public Scene CreateScene(string name, int width = 800, int height = 600,
                                 string background = "white")
        {
            lock (_lock)
            {
                string id = UniqueId();
                var scene = new Scene
                {
                    Id         = id,
                    Name       = name.Length > 0 ? name : "Untitled",
                    Width      = Math.Clamp(width,  1, 8192),
                    Height     = Math.Clamp(height, 1, 8192),
                    Background = background,
                    Layers     = [new Layer { Name = "Layer 1", ZIndex = 0 }]
                };
                _scenes[id] = scene;
                SaveToDisk(scene);
                return scene;
            }
        }

        public Scene? GetScene(string id)
        {
            lock (_lock)
                return _scenes.TryGetValue(id, out var s) ? s : null;
        }

        public Scene[] ListScenes()
        {
            lock (_lock)
                return [.. _scenes.Values.OrderBy(s => s.CreatedAt)];
        }

        public Scene UpdateSceneMeta(string id, string? name, int? width,
                                     int? height, string? background)
        {
            lock (_lock)
            {
                var scene      = Require(id);
                if (name       != null) scene.Name       = name;
                if (width      != null) scene.Width      = Math.Clamp(width.Value,  1, 8192);
                if (height     != null) scene.Height     = Math.Clamp(height.Value, 1, 8192);
                if (background != null) scene.Background = background;
                scene.Touch();
                SaveToDisk(scene);
                return scene;
            }
        }

        public void DeleteScene(string id)
        {
            lock (_lock)
            {
                if (!_scenes.Remove(id)) return;
                string path = SceneFilePath(id);
                if (File.Exists(path)) try { File.Delete(path); } catch { }
            }
        }

        // ── Layer CRUD ────────────────────────────────────────────────────

        public Layer AddLayer(string sceneId, string name)
        {
            lock (_lock)
            {
                var scene  = Require(sceneId);
                int zIndex = scene.Layers.Count > 0 ? scene.Layers.Max(l => l.ZIndex) + 1 : 0;
                var layer  = new Layer { Name = name.Length > 0 ? name : "Layer", ZIndex = zIndex };
                scene.Layers.Add(layer);
                scene.Touch();
                SaveToDisk(scene);
                return layer;
            }
        }

        public Layer UpdateLayer(string sceneId, string layerId,
                                  string? name, bool? visible, bool? locked,
                                  float? opacity, int? zIndex)
        {
            lock (_lock)
            {
                var layer  = RequireLayer(sceneId, layerId, out var scene);
                if (name    != null) layer.Name    = name;
                if (visible != null) layer.Visible = visible.Value;
                if (locked  != null) layer.Locked  = locked.Value;
                if (opacity != null) layer.Opacity = Math.Clamp(opacity.Value, 0f, 1f);
                if (zIndex  != null) layer.ZIndex  = zIndex.Value;
                scene.Touch();
                SaveToDisk(scene);
                return layer;
            }
        }

        public void DeleteLayer(string sceneId, string layerId)
        {
            lock (_lock)
            {
                var scene = Require(sceneId);
                int removed = scene.Layers.RemoveAll(l => l.Id == layerId);
                if (removed == 0) throw new KeyNotFoundException($"Layer '{layerId}' not found.");
                scene.Touch();
                SaveToDisk(scene);
            }
        }

        public void ReorderLayer(string sceneId, string layerId, int newZIndex)
        {
            lock (_lock)
            {
                var layer   = RequireLayer(sceneId, layerId, out var scene);
                layer.ZIndex = newZIndex;
                scene.Touch();
                SaveToDisk(scene);
            }
        }

        // ── Shape CRUD ────────────────────────────────────────────────────

        public SceneShape AddShape(string sceneId, string layerId,
                                    AIDrawingCommand.ShapeCommand shape, string? name = null)
        {
            lock (_lock)
            {
                var layer  = RequireLayer(sceneId, layerId, out var scene);
                int zIndex = layer.Shapes.Count > 0 ? layer.Shapes.Max(s => s.ZIndex) + 1 : 0;
                var ss     = new SceneShape
                {
                    Name   = name ?? shape.Type,
                    ZIndex = zIndex,
                    Shape  = shape
                };
                layer.Shapes.Add(ss);
                scene.Touch();
                SaveToDisk(scene);
                return ss;
            }
        }

        public SceneShape UpdateShape(string sceneId, string layerId, string shapeId,
                                       AIDrawingCommand.ShapeCommand shape, string? name = null)
        {
            lock (_lock)
            {
                var ss    = RequireShape(sceneId, layerId, shapeId, out var scene);
                ss.Shape  = shape;
                if (name != null) ss.Name = name;
                scene.Touch();
                SaveToDisk(scene);
                return ss;
            }
        }

        /// <summary>
        /// Update only the geometric fields of a shape (called on every editor mouseup).
        /// Style fields (color, fill, opacity, etc.) are never touched.
        /// </summary>
        public SceneShape PatchShapeGeometry(string sceneId, string layerId, string shapeId,
                                              float? x, float? y, float? x2, float? y2,
                                              float? w, float? h, float[]? points,
                                              float? r = null,
                                              bool? visible = null, bool? locked = null,
                                              int? zIndex = null, string? name = null,
                                              float? rotation = null,
                                              float? startAngle = null,
                                              float? sweepAngle = null)
        {
            lock (_lock)
            {
                var ss = RequireShape(sceneId, layerId, shapeId, out var scene);
                if (x          != null) ss.Shape.X          = x.Value;
                if (y          != null) ss.Shape.Y          = y.Value;
                if (x2         != null) ss.Shape.X2         = x2.Value;
                if (y2         != null) ss.Shape.Y2         = y2.Value;
                if (w          != null) ss.Shape.W          = w.Value;
                if (h          != null) ss.Shape.H          = h.Value;
                if (r          != null) ss.Shape.R          = r.Value;
                if (points     != null) ss.Shape.Points     = points;
                if (rotation   != null) ss.Shape.Rotation   = rotation.Value;
                if (startAngle != null) ss.Shape.StartAngle = startAngle.Value;
                if (sweepAngle != null) ss.Shape.SweepAngle = sweepAngle.Value;
                if (visible    != null) ss.Visible          = visible.Value;
                if (locked     != null) ss.Locked           = locked.Value;
                if (zIndex     != null) ss.ZIndex           = zIndex.Value;
                if (name       != null) ss.Name             = name;
                scene.Touch();
                SaveToDisk(scene);
                return ss;
            }
        }

        public void PatchShapeStyle(string sceneId, string layerId, string shapeId,
                                     string? color = null, bool? fill = null,
                                     float? opacity = null, float? strokeWidth = null)
        {
            lock (_lock)
            {
                var ss = RequireShape(sceneId, layerId, shapeId, out var scene);
                if (color       != null) ss.Shape.Color       = color;
                if (fill        != null) ss.Shape.Fill        = fill.Value;
                if (opacity     != null) ss.Shape.Opacity     = opacity.Value;
                if (strokeWidth != null) ss.Shape.StrokeWidth = strokeWidth.Value;
                scene.Touch();
                SaveToDisk(scene);
            }
        }

        public void DeleteShape(string sceneId, string layerId, string shapeId)
        {
            lock (_lock)
            {
                var layer   = RequireLayer(sceneId, layerId, out var scene);
                int removed = layer.Shapes.RemoveAll(s => s.Id == shapeId);
                if (removed == 0) throw new KeyNotFoundException($"Shape '{shapeId}' not found.");
                scene.Touch();
                SaveToDisk(scene);
            }
        }

        public SceneShape MoveShapeToLayer(string sceneId, string shapeId, string targetLayerId)
        {
            lock (_lock)
            {
                var scene  = Require(sceneId);
                var source = scene.Layers.FirstOrDefault(l => l.Shapes.Any(s => s.Id == shapeId))
                             ?? throw new KeyNotFoundException($"Shape '{shapeId}' not found.");
                var target = scene.Layers.FirstOrDefault(l => l.Id == targetLayerId)
                             ?? throw new KeyNotFoundException($"Layer '{targetLayerId}' not found.");
                var ss     = source.Shapes.First(s => s.Id == shapeId);
                source.Shapes.Remove(ss);
                target.Shapes.Add(ss);
                scene.Touch();
                SaveToDisk(scene);
                return ss;
            }
        }

        // ── Render ────────────────────────────────────────────────────────

        /// <summary>Render the scene to a base-64 PNG string.</summary>
        public string RenderScene(string sceneId)
        {
            // Lock only long enough to build the DrawRequest (clones all shapes);
            // the actual GDI+ render happens outside the lock.
            AIDrawingCommand.DrawRequest drawReq;
            lock (_lock)
            {
                var scene = Require(sceneId);
                drawReq   = scene.ToDrawRequest();
            }
            return AIDrawingCommand.Render(drawReq);
        }

        // ── Disk persistence ──────────────────────────────────────────────

        private void SaveToDisk(Scene scene)
        {
            // Called inside _lock — small JSON files, negligible I/O time.
            try
            {
                File.WriteAllText(SceneFilePath(scene.Id),
                    JsonSerializer.Serialize(scene, JsonOpts));
            }
            catch (Exception ex) { AppLog.Warning($"SceneStore: failed to persist scene '{scene.Id}' to disk — {ex.Message}"); }
        }

        private void LoadAllFromDisk()
        {
            foreach (string file in Directory.GetFiles(_scenesDir, "*.json"))
            {
                try
                {
                    string json   = File.ReadAllText(file);
                    var    scene  = JsonSerializer.Deserialize<Scene>(json, JsonOpts);
                    if (scene != null)
                        _scenes[scene.Id] = scene;
                }
                catch (Exception ex) { AppLog.Warning($"SceneStore: skipping corrupt scene file '{Path.GetFileName(file)}' — {ex.Message}"); }
            }
        }

        private string SceneFilePath(string id) =>
            Path.Combine(_scenesDir, $"{id}.json");

        // ── Internal helpers ──────────────────────────────────────────────

        private Scene Require(string id) =>
            _scenes.TryGetValue(id, out var s) ? s
            : throw new KeyNotFoundException($"Scene '{id}' not found.");

        private Layer RequireLayer(string sceneId, string layerId, out Scene scene)
        {
            scene = Require(sceneId);
            return scene.Layers.FirstOrDefault(l => l.Id == layerId)
                   ?? throw new KeyNotFoundException($"Layer '{layerId}' not found.");
        }

        private SceneShape RequireShape(string sceneId, string layerId, string shapeId,
                                         out Scene scene)
        {
            var layer = RequireLayer(sceneId, layerId, out scene);
            return layer.Shapes.FirstOrDefault(s => s.Id == shapeId)
                   ?? throw new KeyNotFoundException($"Shape '{shapeId}' not found.");
        }

        private string UniqueId()
        {
            string id;
            do { id = SceneIds.New(); } while (_scenes.ContainsKey(id));
            return id;
        }
    }
}
