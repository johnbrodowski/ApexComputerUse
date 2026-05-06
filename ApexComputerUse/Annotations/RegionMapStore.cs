using System.Text.Json;

namespace ApexComputerUse
{
    /// <summary>
    /// Thread-safe, JSON-persisted store for <see cref="RegionMap"/> objects.
    /// One file per map at <c>%LOCALAPPDATA%\ApexComputerUse\regionmaps\{id}.json</c>.
    /// Mirrors <see cref="SceneStore"/> shape so all CRUD callers behave consistently.
    /// </summary>
    public sealed class RegionMapStore
    {
        private static readonly string DefaultDir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                         "ApexComputerUse", "regionmaps");

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            WriteIndented               = true,
            PropertyNameCaseInsensitive = true
        };

        private readonly string _dir;
        private readonly Dictionary<string, RegionMap> _maps = new();
        private readonly object _lock = new();

        public RegionMapStore(string? dir = null)
        {
            _dir = dir ?? DefaultDir;
            Directory.CreateDirectory(_dir);
            LoadAllFromDisk();
        }

        // -- CRUD -----------------------------------------------------------

        public RegionMap Create(RegionMap proto)
        {
            if (proto == null) throw new ArgumentNullException(nameof(proto));
            lock (_lock)
            {
                string id = UniqueId();
                var map = new RegionMap
                {
                    Id           = id,
                    Name         = string.IsNullOrEmpty(proto.Name) ? "Untitled" : proto.Name,
                    WindowTitle  = proto.WindowTitle,
                    ElementHash  = proto.ElementHash,
                    OriginX      = proto.OriginX,
                    OriginY      = proto.OriginY,
                    CellWidth    = Math.Max(1, proto.CellWidth),
                    CellHeight   = Math.Max(1, proto.CellHeight),
                    Rows         = Math.Max(1, proto.Rows),
                    Cols         = Math.Max(1, proto.Cols),
                    Color        = string.IsNullOrEmpty(proto.Color) ? "#33FF33" : proto.Color,
                    Labels       = proto.Labels,
                    Notes        = proto.Notes,
                };
                _maps[id] = map;
                SaveToDisk(map);
                return map;
            }
        }

        public RegionMap? Get(string id)
        {
            lock (_lock)
                return _maps.TryGetValue(id, out var m) ? m : null;
        }

        public RegionMap[] List()
        {
            lock (_lock)
                return [.. _maps.Values.OrderBy(m => m.CreatedUtc)];
        }

        public RegionMap[] FindByScope(string? windowTitle, string? elementHash)
        {
            lock (_lock)
            {
                IEnumerable<RegionMap> q = _maps.Values;
                if (!string.IsNullOrEmpty(windowTitle))
                    q = q.Where(m => string.Equals(m.WindowTitle, windowTitle, StringComparison.Ordinal));
                if (!string.IsNullOrEmpty(elementHash))
                    q = q.Where(m => string.Equals(m.ElementHash, elementHash, StringComparison.Ordinal));
                return [.. q.OrderBy(m => m.CreatedUtc)];
            }
        }

        /// <summary>Patches geometry/labels/notes — pass null to leave a field unchanged.</summary>
        public RegionMap Update(string id,
                                string? name, string? windowTitle, string? elementHash,
                                int? originX, int? originY, int? cellWidth, int? cellHeight,
                                int? rows, int? cols, string? color, string? notes,
                                string?[][]? labels)
        {
            lock (_lock)
            {
                var m = Require(id);
                if (name        != null) m.Name        = name;
                if (windowTitle != null) m.WindowTitle = string.IsNullOrEmpty(windowTitle) ? null : windowTitle;
                if (elementHash != null) m.ElementHash = string.IsNullOrEmpty(elementHash) ? null : elementHash;
                if (originX     != null) m.OriginX     = originX.Value;
                if (originY     != null) m.OriginY     = originY.Value;
                if (cellWidth   != null) m.CellWidth   = Math.Max(1, cellWidth.Value);
                if (cellHeight  != null) m.CellHeight  = Math.Max(1, cellHeight.Value);
                if (rows        != null) m.Rows        = Math.Max(1, rows.Value);
                if (cols        != null) m.Cols        = Math.Max(1, cols.Value);
                if (color       != null) m.Color       = color;
                if (notes       != null) m.Notes       = string.IsNullOrEmpty(notes) ? null : notes;
                if (labels      != null) m.Labels      = labels;
                m.Touch();
                SaveToDisk(m);
                return m;
            }
        }

        public void Delete(string id)
        {
            lock (_lock)
            {
                if (!_maps.Remove(id)) return;
                string path = MapFilePath(id);
                if (File.Exists(path)) try { File.Delete(path); } catch { }
            }
        }

        // -- Helpers (geometry + draw-request building) ---------------------

        /// <summary>
        /// Returns the centre pixel of (row, col) in screen coordinates.
        /// Throws if row/col are outside the grid — callers must validate.
        /// </summary>
        public static (int x, int y) CellToPixel(RegionMap map, int row, int col)
        {
            if (row < 0 || row >= map.Rows) throw new ArgumentOutOfRangeException(nameof(row));
            if (col < 0 || col >= map.Cols) throw new ArgumentOutOfRangeException(nameof(col));
            int x = map.OriginX + (col * map.CellWidth)  + (map.CellWidth  / 2);
            int y = map.OriginY + (row * map.CellHeight) + (map.CellHeight / 2);
            return (x, y);
        }

        /// <summary>
        /// Builds a DrawRequest that paints the grid lines and (optionally) per-cell labels.
        /// The caller chooses the canvas — typically "screen" for an overlay calibration loop
        /// or a base64 PNG for an offline rendered visual.
        /// </summary>
        public static AIDrawingCommand.DrawRequest BuildGridDrawRequest(
            RegionMap map, string canvas = "screen", bool labelCells = true,
            int canvasWidth = 1920, int canvasHeight = 1080)
        {
            var req = new AIDrawingCommand.DrawRequest
            {
                Canvas = canvas,
                Width  = canvasWidth,
                Height = canvasHeight,
                Shapes = []
            };

            int x0 = map.OriginX;
            int y0 = map.OriginY;
            int x1 = map.OriginX + map.Cols * map.CellWidth;
            int y1 = map.OriginY + map.Rows * map.CellHeight;
            string color = map.Color;

            // Vertical lines (cols + 1)
            for (int c = 0; c <= map.Cols; c++)
            {
                int x = x0 + c * map.CellWidth;
                req.Shapes.Add(new AIDrawingCommand.ShapeCommand
                {
                    Type = "line", X = x, Y = y0, X2 = x, Y2 = y1,
                    Color = color, StrokeWidth = 2
                });
            }
            // Horizontal lines (rows + 1)
            for (int r = 0; r <= map.Rows; r++)
            {
                int y = y0 + r * map.CellHeight;
                req.Shapes.Add(new AIDrawingCommand.ShapeCommand
                {
                    Type = "line", X = x0, Y = y, X2 = x1, Y2 = y,
                    Color = color, StrokeWidth = 2
                });
            }

            if (labelCells)
            {
                for (int r = 0; r < map.Rows; r++)
                {
                    for (int c = 0; c < map.Cols; c++)
                    {
                        string? cellLabel = null;
                        if (map.Labels != null && r < map.Labels.Length && map.Labels[r] != null
                            && c < map.Labels[r].Length)
                            cellLabel = map.Labels[r][c];
                        string text = cellLabel ?? $"{r},{c}";
                        int cx = x0 + c * map.CellWidth + map.CellWidth / 2;
                        int cy = y0 + r * map.CellHeight + map.CellHeight / 2;
                        req.Shapes.Add(new AIDrawingCommand.ShapeCommand
                        {
                            Type = "text", X = cx, Y = cy - 6,
                            Text = text, Color = color, FontSize = 10, Align = "center"
                        });
                    }
                }
            }

            return req;
        }

        // -- Disk persistence -----------------------------------------------

        private void SaveToDisk(RegionMap map)
        {
            try
            {
                File.WriteAllText(MapFilePath(map.Id),
                    JsonSerializer.Serialize(map, JsonOpts));
            }
            catch (Exception ex) { AppLog.Warning($"RegionMapStore: failed to persist '{map.Id}' - {ex.Message}"); }
        }

        private void LoadAllFromDisk()
        {
            foreach (string file in Directory.GetFiles(_dir, "*.json"))
            {
                try
                {
                    string json = File.ReadAllText(file);
                    var map  = JsonSerializer.Deserialize<RegionMap>(json, JsonOpts);
                    if (map != null && !string.IsNullOrEmpty(map.Id))
                        _maps[map.Id] = map;
                }
                catch (Exception ex) { AppLog.Warning($"RegionMapStore: skipping corrupt file '{Path.GetFileName(file)}' - {ex.Message}"); }
            }
        }

        private string MapFilePath(string id) => Path.Combine(_dir, $"{id}.json");

        private RegionMap Require(string id) =>
            _maps.TryGetValue(id, out var m) ? m
            : throw new KeyNotFoundException($"RegionMap '{id}' not found.");

        private string UniqueId()
        {
            string id;
            do { id = SceneIds.New(); } while (_maps.ContainsKey(id));
            return id;
        }
    }
}
