using System.Net;
using System.Text.Json;

namespace ApexComputerUse
{
    public partial class HttpCommandServer
    {
        // -- Annotation + RegionMap REST routes ----------------------------
        // /annotate, /unannotate, /exclude, /unexclude, /annotations, /excluded
        // /regionmap, /regionmap/{id}, /regionmap/{id}/overlay, /regionmap/{id}/cell

        private ApexResult? TryHandleAnnotationRoute(string method, string path,
                                                     string body, HttpListenerRequest req)
        {
            try
            {
                // -- Annotations --------------------------------------------
                if (path == "/annotate"     && method == "POST")  return DoAnnotate(body, set: true);
                if (path == "/unannotate"   && method == "POST")  return DoAnnotate(body, set: false);
                if (path == "/exclude"      && method == "POST")  return DoExclude(body, excluded: true);
                if (path == "/unexclude"    && method == "POST")  return DoExclude(body, excluded: false);
                if (path == "/annotations"  && method == "GET")   return DoListAnnotations();
                if (path == "/excluded"     && method == "GET")   return DoListExcluded();

                // -- RegionMaps ---------------------------------------------
                if (path == "/regionmap" && method == "GET")  return DoListRegionMaps(req);
                if (path == "/regionmap" && method == "POST") return DoCreateRegionMap(body);

                if (path.StartsWith("/regionmap/", StringComparison.Ordinal))
                {
                    var seg = path.Split('/');
                    // ["", "regionmap", "{id}", "overlay"|"cell"?]
                    if (seg.Length == 3)
                    {
                        string id = seg[2];
                        if (method == "GET")    return DoGetRegionMap(id);
                        if (method == "PUT" || method == "PATCH") return DoUpdateRegionMap(id, body);
                        if (method == "DELETE") return DoDeleteRegionMap(id);
                    }
                    if (seg.Length == 4 && seg[3] == "overlay" && method == "POST")
                        return DoOverlayRegionMap(seg[2], body);
                    if (seg.Length == 4 && seg[3] == "render"  && method == "POST")
                        return DoRenderRegionMap(seg[2], body);
                    if (seg.Length == 4 && seg[3] == "cell"    && method == "POST")
                        return DoRegionMapCell(seg[2], body);
                }
            }
            catch (KeyNotFoundException ex)
            {
                return new ApexResult { Success = false, Action = $"{method} {path}", Error = ex.Message };
            }
            catch (Exception ex)
            {
                return new ApexResult { Success = false, Action = $"{method} {path}", Error = ex.Message };
            }
            return null;
        }

        // -- Annotation handlers -------------------------------------------

        private ApexResult DoAnnotate(string body, bool set)
        {
            if (_processor.ElementAnnotations == null)
                return Err("annotate", "ElementAnnotationStore not initialised.");
            using var d = JsonDocument.Parse(body.Length > 0 ? body : "{}");
            if (!ResolveTargetHash(d.RootElement, out string hash, out string err))
                return Err("annotate", err);

            string? note = set ? d.RootElement.Str("note") : null;
            (string? ct, string? n, string? aid) = LookupDescriptorForElement(d.RootElement);
            var record = _processor.ElementAnnotations.SetNote(hash, note, ct, n, aid);
            return AnnOk(set ? "annotate" : "unannotate", record);
        }

        private ApexResult DoExclude(string body, bool excluded)
        {
            if (_processor.ElementAnnotations == null)
                return Err("exclude", "ElementAnnotationStore not initialised.");
            using var d = JsonDocument.Parse(body.Length > 0 ? body : "{}");
            if (!ResolveTargetHash(d.RootElement, out string hash, out string err))
                return Err(excluded ? "exclude" : "unexclude", err);
            (string? ct, string? n, string? aid) = LookupDescriptorForElement(d.RootElement);
            var record = _processor.ElementAnnotations.SetExcluded(hash, excluded, ct, n, aid);
            return AnnOk(excluded ? "exclude" : "unexclude", record);
        }

        private ApexResult DoListAnnotations()
        {
            if (_processor.ElementAnnotations == null)
                return Err("annotations", "ElementAnnotationStore not initialised.");
            var arr = _processor.ElementAnnotations.ListAll();
            return new ApexResult
            {
                Success = true, Action = "annotations",
                Data = new Dictionary<string, string>
                {
                    ["count"]       = arr.Length.ToString(),
                    ["annotations"] = JsonSerializer.Serialize(arr, FormatAdapter.s_compact)
                }
            };
        }

        private ApexResult DoListExcluded()
        {
            if (_processor.ElementAnnotations == null)
                return Err("excluded", "ElementAnnotationStore not initialised.");
            var arr = _processor.ElementAnnotations.ListExcluded();
            return new ApexResult
            {
                Success = true, Action = "excluded",
                Data = new Dictionary<string, string>
                {
                    ["count"]    = arr.Length.ToString(),
                    ["excluded"] = JsonSerializer.Serialize(arr, FormatAdapter.s_compact)
                }
            };
        }

        // -- RegionMap handlers --------------------------------------------

        private ApexResult DoListRegionMaps(HttpListenerRequest req)
        {
            if (_processor.RegionMaps == null) return Err("regionmap/list", "RegionMapStore not initialised.");
            string? winFilter  = req.QueryString["window"];
            string? hashFilter = req.QueryString["hash"];
            var maps = (winFilter != null || hashFilter != null)
                ? _processor.RegionMaps.FindByScope(winFilter, hashFilter)
                : _processor.RegionMaps.List();
            return new ApexResult
            {
                Success = true, Action = "regionmap/list",
                Data = new Dictionary<string, string>
                {
                    ["count"] = maps.Length.ToString(),
                    ["maps"]  = JsonSerializer.Serialize(maps, FormatAdapter.s_compact)
                }
            };
        }

        private ApexResult DoCreateRegionMap(string body)
        {
            if (_processor.RegionMaps == null) return Err("regionmap/create", "RegionMapStore not initialised.");
            using var d = JsonDocument.Parse(body.Length > 0 ? body : "{}");
            var proto = new RegionMap
            {
                Name        = d.RootElement.Str("name") ?? "Untitled",
                WindowTitle = d.RootElement.Str("windowTitle") ?? d.RootElement.Str("window"),
                ElementHash = d.RootElement.Str("elementHash") ?? d.RootElement.Str("hash"),
                OriginX     = d.RootElement.Int("originX")    ?? 0,
                OriginY     = d.RootElement.Int("originY")    ?? 0,
                CellWidth   = d.RootElement.Int("cellWidth")  ?? 64,
                CellHeight  = d.RootElement.Int("cellHeight") ?? 64,
                Rows        = d.RootElement.Int("rows")       ?? 8,
                Cols        = d.RootElement.Int("cols")       ?? 8,
                Color       = d.RootElement.Str("color")      ?? "#33FF33",
                Notes       = d.RootElement.Str("notes")
            };
            proto.Labels = ParseLabels(d.RootElement);
            var created = _processor.RegionMaps.Create(proto);
            return RegionMapOk("regionmap/create", created);
        }

        private ApexResult DoGetRegionMap(string id)
        {
            if (_processor.RegionMaps == null) return Err("regionmap/get", "RegionMapStore not initialised.");
            var m = _processor.RegionMaps.Get(id);
            if (m == null) return Err("regionmap/get", $"RegionMap '{id}' not found.");
            return RegionMapOk("regionmap/get", m);
        }

        private ApexResult DoUpdateRegionMap(string id, string body)
        {
            if (_processor.RegionMaps == null) return Err("regionmap/update", "RegionMapStore not initialised.");
            using var d = JsonDocument.Parse(body.Length > 0 ? body : "{}");
            var m = _processor.RegionMaps.Update(id,
                name:        d.RootElement.Str("name"),
                windowTitle: d.RootElement.Str("windowTitle") ?? d.RootElement.Str("window"),
                elementHash: d.RootElement.Str("elementHash") ?? d.RootElement.Str("hash"),
                originX:     d.RootElement.Int("originX"),
                originY:     d.RootElement.Int("originY"),
                cellWidth:   d.RootElement.Int("cellWidth"),
                cellHeight:  d.RootElement.Int("cellHeight"),
                rows:        d.RootElement.Int("rows"),
                cols:        d.RootElement.Int("cols"),
                color:       d.RootElement.Str("color"),
                notes:       d.RootElement.Str("notes"),
                labels:      ParseLabels(d.RootElement));
            return RegionMapOk("regionmap/update", m);
        }

        private ApexResult DoDeleteRegionMap(string id)
        {
            if (_processor.RegionMaps == null) return Err("regionmap/delete", "RegionMapStore not initialised.");
            _processor.RegionMaps.Delete(id);
            return new ApexResult
            {
                Success = true, Action = "regionmap/delete",
                Data = new Dictionary<string, string> { ["deleted"] = id }
            };
        }

        private ApexResult DoOverlayRegionMap(string id, string body)
        {
            if (_processor.RegionMaps == null) return Err("regionmap/overlay", "RegionMapStore not initialised.");
            var m = _processor.RegionMaps.Get(id);
            if (m == null) return Err("regionmap/overlay", $"RegionMap '{id}' not found.");

            using var d = JsonDocument.Parse(body.Length > 0 ? body : "{}");
            int durationMs = d.RootElement.Int("durationMs") ?? d.RootElement.Int("ms") ?? 5000;

            var screen = System.Windows.Forms.Screen.PrimaryScreen?.Bounds
                         ?? new System.Drawing.Rectangle(0, 0, 1920, 1080);
            var drawReq = RegionMapStore.BuildGridDrawRequest(m, canvas: "blank",
                              labelCells: true, canvasWidth: screen.Width, canvasHeight: screen.Height);
            drawReq.Background = "white";
            drawReq.Overlay    = true;
            drawReq.OverlayMs  = durationMs;

            try
            {
                System.Windows.Forms.Application.OpenForms[0]?.BeginInvoke(
                    () => AIDrawingCommand.ShowOverlay(drawReq));
            }
            catch { /* service mode - no UI host */ }

            return new ApexResult
            {
                Success = true, Action = "regionmap/overlay",
                Data = new Dictionary<string, string>
                {
                    ["id"]       = id,
                    ["duration"] = durationMs.ToString(),
                    ["message"]  = $"Overlaying '{m.Name}' for {durationMs}ms"
                }
            };
        }

        private ApexResult DoRenderRegionMap(string id, string body)
        {
            if (_processor.RegionMaps == null) return Err("regionmap/render", "RegionMapStore not initialised.");
            var m = _processor.RegionMaps.Get(id);
            if (m == null) return Err("regionmap/render", $"RegionMap '{id}' not found.");

            using var d = JsonDocument.Parse(body.Length > 0 ? body : "{}");
            // canvas: "screen" (default) | "window" | "<base64 png/jpg>"
            string canvas = d.RootElement.Str("canvas") ?? "screen";

            // "window" mode: capture the bound window (or current foreground) and use
            // it as the canvas. Grid coords are screen-absolute, so we translate them
            // into the window's local coordinate space before drawing.
            int offsetX = 0, offsetY = 0;
            int canvasW, canvasH;
            if (string.Equals(canvas, "window", StringComparison.OrdinalIgnoreCase))
            {
                var bounds = _processor.GetCurrentWindowBounds();
                if (bounds.HasValue)
                {
                    var b = bounds.Value;
                    offsetX = b.x; offsetY = b.y;
                    canvasW = b.w; canvasH = b.h;
                    using var shot = new System.Drawing.Bitmap(canvasW, canvasH,
                        System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                    using (var gs = System.Drawing.Graphics.FromImage(shot))
                        gs.CopyFromScreen(new System.Drawing.Point(b.x, b.y),
                            System.Drawing.Point.Empty, new System.Drawing.Size(b.w, b.h));
                    using var ms = new MemoryStream();
                    shot.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                    canvas = Convert.ToBase64String(ms.ToArray());
                }
                else
                {
                    canvas = "screen";
                    var sc = System.Windows.Forms.Screen.PrimaryScreen?.Bounds
                             ?? new System.Drawing.Rectangle(0, 0, 1920, 1080);
                    canvasW = sc.Width; canvasH = sc.Height;
                }
            }
            else
            {
                var sc = System.Windows.Forms.Screen.PrimaryScreen?.Bounds
                         ?? new System.Drawing.Rectangle(0, 0, 1920, 1080);
                canvasW = sc.Width; canvasH = sc.Height;
            }

            // Build the grid request, translating origin into local coords if windowed.
            var translated = m;
            if (offsetX != 0 || offsetY != 0)
            {
                translated = new RegionMap
                {
                    Id = m.Id, Name = m.Name, WindowTitle = m.WindowTitle, ElementHash = m.ElementHash,
                    OriginX = m.OriginX - offsetX, OriginY = m.OriginY - offsetY,
                    CellWidth = m.CellWidth, CellHeight = m.CellHeight,
                    Rows = m.Rows, Cols = m.Cols, Color = m.Color,
                    Labels = m.Labels, Notes = m.Notes,
                    CreatedUtc = m.CreatedUtc, UpdatedUtc = m.UpdatedUtc
                };
            }
            var drawReq = RegionMapStore.BuildGridDrawRequest(translated, canvas: canvas,
                              labelCells: true, canvasWidth: canvasW, canvasHeight: canvasH);
            string b64 = AIDrawingCommand.Render(drawReq);
            return new ApexResult
            {
                Success = true, Action = "regionmap/render",
                Data = new Dictionary<string, string>
                {
                    ["id"]     = id,
                    ["mode"]   = string.Equals(d.RootElement.Str("canvas"), "window",
                                    StringComparison.OrdinalIgnoreCase) ? "window" : "screen",
                    ["width"]  = canvasW.ToString(),
                    ["height"] = canvasH.ToString(),
                    ["result"] = b64
                }
            };
        }

        private ApexResult DoRegionMapCell(string id, string body)
        {
            if (_processor.RegionMaps == null) return Err("regionmap/cell", "RegionMapStore not initialised.");
            var m = _processor.RegionMaps.Get(id);
            if (m == null) return Err("regionmap/cell", $"RegionMap '{id}' not found.");

            using var d = JsonDocument.Parse(body.Length > 0 ? body : "{}");
            int row = d.RootElement.Int("row") ?? -1;
            int col = d.RootElement.Int("col") ?? -1;
            try
            {
                var (x, y) = RegionMapStore.CellToPixel(m, row, col);
                return new ApexResult
                {
                    Success = true, Action = "regionmap/cell",
                    Data = new Dictionary<string, string>
                    {
                        ["row"]    = row.ToString(),
                        ["col"]    = col.ToString(),
                        ["x"]      = x.ToString(),
                        ["y"]      = y.ToString(),
                        ["coords"] = $"{x},{y}"
                    }
                };
            }
            catch (ArgumentOutOfRangeException ex)
            {
                return Err("regionmap/cell", $"Cell out of range: {ex.Message}");
            }
        }

        // -- Helpers -------------------------------------------------------

        /// <summary>
        /// Resolves the target element-hash from a request body that may carry either
        /// a "hash" string or a numeric "id" (translated against the most-recent /elements scan).
        /// </summary>
        private bool ResolveTargetHash(JsonElement root, out string hash, out string error)
        {
            string? explicitHash = root.Str("hash");
            if (!string.IsNullOrEmpty(explicitHash))
            {
                hash  = explicitHash;
                error = "";
                return true;
            }
            int? idMaybe = root.Int("id");
            if (idMaybe.HasValue && _processor.TryResolveHash(idMaybe.Value, out string h))
            {
                hash  = h;
                error = "";
                return true;
            }
            hash  = "";
            error = "Provide either \"id\":<numeric> (from a recent /elements scan) or \"hash\":\"<element hash>\".";
            return false;
        }

        private (string? ct, string? n, string? aid) LookupDescriptorForElement(JsonElement root)
        {
            int? idMaybe = root.Int("id");
            if (idMaybe.HasValue
                && _processor.TryResolveDescriptor(idMaybe.Value, out string ct, out string n, out string aid))
                return (ct, n, aid);
            return (null, null, null);
        }

        private static string?[][]? ParseLabels(JsonElement root)
        {
            if (!root.TryGetProperty("labels", out var labels) || labels.ValueKind != JsonValueKind.Array)
                return null;
            var rows = new List<string?[]>();
            foreach (var rowElem in labels.EnumerateArray())
            {
                if (rowElem.ValueKind != JsonValueKind.Array) { rows.Add(Array.Empty<string?>()); continue; }
                var cells = new List<string?>();
                foreach (var c in rowElem.EnumerateArray())
                    cells.Add(c.ValueKind == JsonValueKind.String ? c.GetString() : null);
                rows.Add(cells.ToArray());
            }
            return rows.ToArray();
        }

        private static ApexResult AnnOk(string action, ElementAnnotation rec) =>
            new()
            {
                Success = true, Action = action,
                Data = new Dictionary<string, string>
                {
                    ["annotation"] = JsonSerializer.Serialize(rec, FormatAdapter.s_compact)
                }
            };

        private static ApexResult RegionMapOk(string action, RegionMap m) =>
            new()
            {
                Success = true, Action = action,
                Data = new Dictionary<string, string>
                {
                    ["regionmap"] = JsonSerializer.Serialize(m, FormatAdapter.s_compact)
                }
            };

        private static ApexResult Err(string action, string message) =>
            new() { Success = false, Action = action, Error = message };
    }
}
