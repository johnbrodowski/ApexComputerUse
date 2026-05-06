using System.Text.Json;

namespace ApexComputerUse
{
    public partial class CommandProcessor
    {
        // -- RegionMap verbs -----------------------------------------------
        // Single umbrella command "regionmap" with an Action sub-verb so non-HTTP callers
        // (named pipe, Telegram) can drive the same calibration loop. The HTTP server has
        // a richer REST shape in HttpCommandServer.AnnotationRoutes but ultimately calls
        // back into RegionMapStore, so the two paths stay in sync.

        private CommandResponse CmdRegionMap(CommandRequest req)
        {
            if (RegionMaps == null) return Fail("RegionMapStore not initialised.");
            string action = (req.Action ?? "list").ToLowerInvariant();

            return action switch
            {
                "list"     => RegionMapList(req),
                "get"      => RegionMapGet(req),
                "delete"   => RegionMapDelete(req),
                "overlay"  => RegionMapOverlay(req),
                "render"   => RegionMapRender(req),
                "cell"     => RegionMapCell(req),
                _          => Fail($"Unknown regionmap action '{action}'. Try list|get|delete|overlay|render|cell."),
            };
        }

        private CommandResponse RegionMapList(CommandRequest req)
        {
            // Optional scope filter via Window= or Value=hash:<hash>
            string? winFilter  = req.Window;
            string? hashFilter = (req.Value != null && req.Value.StartsWith("hash:", StringComparison.OrdinalIgnoreCase))
                                 ? req.Value.Substring("hash:".Length) : null;
            var maps = (winFilter != null || hashFilter != null)
                ? RegionMaps!.FindByScope(winFilter, hashFilter)
                : RegionMaps!.List();
            return Ok($"{maps.Length} region map(s)",
                      JsonSerializer.Serialize(maps, FormatAdapter.s_compact));
        }

        private CommandResponse RegionMapGet(CommandRequest req)
        {
            string id = req.Value ?? req.AutomationId ?? "";
            if (string.IsNullOrEmpty(id)) return Fail("regionmap get requires id in value field.");
            var m = RegionMaps!.Get(id);
            return m == null ? Fail($"RegionMap '{id}' not found.")
                             : Ok("ok", JsonSerializer.Serialize(m, FormatAdapter.s_compact));
        }

        private CommandResponse RegionMapDelete(CommandRequest req)
        {
            string id = req.Value ?? req.AutomationId ?? "";
            if (string.IsNullOrEmpty(id)) return Fail("regionmap delete requires id in value field.");
            RegionMaps!.Delete(id);
            return Ok($"Deleted region map '{id}'");
        }

        private CommandResponse RegionMapOverlay(CommandRequest req)
        {
            string id = req.Value ?? req.AutomationId ?? "";
            if (string.IsNullOrEmpty(id)) return Fail("regionmap overlay requires id in value field.");
            var m = RegionMaps!.Get(id);
            if (m == null) return Fail($"RegionMap '{id}' not found.");

            var screen = System.Windows.Forms.Screen.PrimaryScreen?.Bounds
                         ?? new System.Drawing.Rectangle(0, 0, 1920, 1080);
            var drawReq = RegionMapStore.BuildGridDrawRequest(m, canvas: "blank",
                              labelCells: true, canvasWidth: screen.Width, canvasHeight: screen.Height);
            drawReq.Background = "white";
            drawReq.Overlay    = true;
            drawReq.OverlayMs  = req.Timeout ?? 5000;

            // ShowOverlay must run on the UI thread.
            try
            {
                System.Windows.Forms.Application.OpenForms[0]?.BeginInvoke(
                    () => AIDrawingCommand.ShowOverlay(drawReq));
            }
            catch { /* no UI host (service mode) - fall through */ }
            return Ok($"Overlaying grid '{m.Name}' ({m.Rows}x{m.Cols} @ {m.OriginX},{m.OriginY})");
        }

        private CommandResponse RegionMapRender(CommandRequest req)
        {
            string id = req.Value ?? req.AutomationId ?? "";
            if (string.IsNullOrEmpty(id)) return Fail("regionmap render requires id in value field.");
            var m = RegionMaps!.Get(id);
            if (m == null) return Fail($"RegionMap '{id}' not found.");

            var screen = System.Windows.Forms.Screen.PrimaryScreen?.Bounds
                         ?? new System.Drawing.Rectangle(0, 0, 1920, 1080);
            var drawReq = RegionMapStore.BuildGridDrawRequest(m, canvas: "screen",
                              labelCells: true, canvasWidth: screen.Width, canvasHeight: screen.Height);
            string b64 = AIDrawingCommand.Render(drawReq);
            return Ok($"Rendered grid '{m.Name}' over current screen", b64);
        }

        private CommandResponse RegionMapCell(CommandRequest req)
        {
            // value="<id>:<row>,<col>" e.g. "abc12345:3,4"
            string raw = req.Value ?? "";
            int colon = raw.IndexOf(':');
            if (colon < 0) return Fail("regionmap cell requires value=\"<id>:<row>,<col>\"");
            string id = raw[..colon];
            var (r, c) = ParsePair(raw[(colon + 1)..]);
            var m = RegionMaps!.Get(id);
            if (m == null) return Fail($"RegionMap '{id}' not found.");
            try
            {
                var (x, y) = RegionMapStore.CellToPixel(m, r, c);
                return Ok($"cell ({r},{c}) = {x},{y}", $"{x},{y}");
            }
            catch (ArgumentOutOfRangeException ex)
            {
                return Fail($"Cell out of range: {ex.Message}");
            }
        }
    }
}
