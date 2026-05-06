using System.Net;
using System.Text.Json;

namespace ApexComputerUse
{
    public partial class HttpCommandServer
    {
        // -- /monitor REST routes -------------------------------------------
        // /monitor              GET (list)  | POST (create)
        // /monitor/{id}         GET | PUT | PATCH | DELETE
        // /monitor/{id}/start   POST
        // /monitor/{id}/stop    POST
        // /monitor/{id}/check   POST   manual one-shot diff vs baseline
        // /monitor/{id}/snapshot POST  base64 PNG of region [?index=N]

        private ApexResult? TryHandleMonitorRoute(string method, string path, string body, HttpListenerRequest req)
        {
            try
            {
                if (path == "/monitor" && method == "GET")  return DoListMonitors();
                if (path == "/monitor" && method == "POST") return DoCreateMonitor(body);

                if (!path.StartsWith("/monitor/", StringComparison.Ordinal)) return null;
                var seg = path.Split('/');   // ["", "monitor", "{id}", ("start"|"stop"|"check"|"snapshot")?]

                if (seg.Length == 3)
                {
                    string id = seg[2];
                    if (method == "GET") return DoGetMonitor(id);
                    if (method == "PUT" || method == "PATCH") return DoUpdateMonitor(id, body);
                    if (method == "DELETE") return DoDeleteMonitor(id);
                }
                if (seg.Length == 4 && method == "POST")
                {
                    string id = seg[2];
                    return seg[3] switch
                    {
                        "start"    => DoToggleMonitor(id, enabled: true),
                        "stop"     => DoToggleMonitor(id, enabled: false),
                        "check"    => DoCheckMonitor(id),
                        "snapshot" => DoSnapshotMonitor(id, req),
                        _ => null
                    };
                }
            }
            catch (KeyNotFoundException ex) { return Err($"{method} {path}", ex.Message); }
            catch (Exception ex)             { return Err($"{method} {path}", ex.Message); }
            return null;
        }

        // -- Handlers -------------------------------------------------------

        private ApexResult DoListMonitors()
        {
            if (_processor.RegionMonitors == null) return Err("monitor/list", "RegionMonitorStore not initialised.");
            var arr = _processor.RegionMonitors.List();
            return new ApexResult
            {
                Success = true, Action = "monitor/list",
                Data = new Dictionary<string, string>
                {
                    ["count"]    = arr.Length.ToString(),
                    ["monitors"] = JsonSerializer.Serialize(arr, FormatAdapter.s_compact)
                }
            };
        }

        private ApexResult DoCreateMonitor(string body)
        {
            if (_processor.RegionMonitors == null) return Err("monitor/create", "RegionMonitorStore not initialised.");
            using var d = JsonDocument.Parse(body.Length > 0 ? body : "{}");
            var proto = new RegionMonitor
            {
                Name         = d.RootElement.Str("name") ?? "Untitled",
                Regions      = ParseRegions(d.RootElement),
                IntervalMs   = d.RootElement.Int("intervalMs")    ?? 1000,
                ThresholdPct = d.RootElement.Dbl("thresholdPct")  ?? 5.0,
                Tolerance    = d.RootElement.Int("tolerance")     ?? 8,
                Enabled      = d.RootElement.Bool("enabled")      ?? false,
                Notes        = d.RootElement.Str("notes")
            };
            var created = _processor.RegionMonitors.Create(proto);
            _processor.MonitorRunner?.Sync();
            return MonitorOk("monitor/create", created);
        }

        private ApexResult DoGetMonitor(string id)
        {
            if (_processor.RegionMonitors == null) return Err("monitor/get", "RegionMonitorStore not initialised.");
            var m = _processor.RegionMonitors.Get(id);
            return m == null ? Err("monitor/get", $"RegionMonitor '{id}' not found.")
                             : MonitorOk("monitor/get", m);
        }

        private ApexResult DoUpdateMonitor(string id, string body)
        {
            if (_processor.RegionMonitors == null) return Err("monitor/update", "RegionMonitorStore not initialised.");
            using var d = JsonDocument.Parse(body.Length > 0 ? body : "{}");
            List<MonitorRegion>? regions =
                d.RootElement.TryGetProperty("regions", out _) ? ParseRegions(d.RootElement) : null;
            var m = _processor.RegionMonitors.Update(id,
                name:         d.RootElement.Str("name"),
                regions:      regions,
                intervalMs:   d.RootElement.Int("intervalMs"),
                thresholdPct: d.RootElement.Dbl("thresholdPct"),
                tolerance:    d.RootElement.Int("tolerance"),
                enabled:      d.RootElement.Bool("enabled"),
                notes:        d.RootElement.Str("notes"));
            _processor.MonitorRunner?.Sync();
            return MonitorOk("monitor/update", m);
        }

        private ApexResult DoDeleteMonitor(string id)
        {
            if (_processor.RegionMonitors == null) return Err("monitor/delete", "RegionMonitorStore not initialised.");
            bool ok = _processor.RegionMonitors.Delete(id);
            _processor.MonitorRunner?.Sync();
            return new ApexResult
            {
                Success = ok, Action = "monitor/delete",
                Data = new Dictionary<string, string> { ["deleted"] = id },
                Error = ok ? null : $"RegionMonitor '{id}' not found."
            };
        }

        private ApexResult DoToggleMonitor(string id, bool enabled)
        {
            if (_processor.RegionMonitors == null) return Err("monitor/toggle", "RegionMonitorStore not initialised.");
            try
            {
                var m = _processor.RegionMonitors.Update(id, enabled: enabled);
                _processor.MonitorRunner?.Sync();
                return MonitorOk(enabled ? "monitor/start" : "monitor/stop", m);
            }
            catch (KeyNotFoundException) { return Err("monitor/toggle", $"RegionMonitor '{id}' not found."); }
        }

        private ApexResult DoCheckMonitor(string id)
        {
            if (_processor.RegionMonitors == null) return Err("monitor/check", "RegionMonitorStore not initialised.");
            if (_processor.MonitorRunner == null) return Err("monitor/check", "MonitorRunner not active.");
            try
            {
                double[] diffs = _processor.MonitorRunner.CheckOnce(id);
                return new ApexResult
                {
                    Success = true, Action = "monitor/check",
                    Data = new Dictionary<string, string>
                    {
                        ["id"]    = id,
                        ["count"] = diffs.Length.ToString(),
                        ["diffs"] = JsonSerializer.Serialize(diffs, FormatAdapter.s_compact)
                    }
                };
            }
            catch (KeyNotFoundException) { return Err("monitor/check", $"RegionMonitor '{id}' not found."); }
        }

        private ApexResult DoSnapshotMonitor(string id, HttpListenerRequest req)
        {
            if (_processor.RegionMonitors == null) return Err("monitor/snapshot", "RegionMonitorStore not initialised.");
            var m = _processor.RegionMonitors.Get(id);
            if (m == null) return Err("monitor/snapshot", $"RegionMonitor '{id}' not found.");
            int idx = int.TryParse(req.QueryString["index"], out int qi) ? qi : 0;
            if (idx < 0 || idx >= m.Regions.Count)
                return Err("monitor/snapshot", $"index {idx} out of range (0..{m.Regions.Count - 1}).");
            using var bmp = RegionMonitorRunner.CaptureRegion(m.Regions[idx]);
            if (bmp == null) return Err("monitor/snapshot", "capture failed (region out of bounds?).");
            using var ms = new MemoryStream();
            bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            return new ApexResult
            {
                Success = true, Action = "monitor/snapshot",
                Data = new Dictionary<string, string>
                {
                    ["id"]     = id,
                    ["index"]  = idx.ToString(),
                    ["width"]  = bmp.Width.ToString(),
                    ["height"] = bmp.Height.ToString(),
                    ["result"] = Convert.ToBase64String(ms.ToArray())
                }
            };
        }

        // -- Helpers --------------------------------------------------------

        private static List<MonitorRegion> ParseRegions(JsonElement root)
        {
            var list = new List<MonitorRegion>();
            if (!root.TryGetProperty("regions", out var arr) || arr.ValueKind != JsonValueKind.Array)
                return list;
            foreach (var r in arr.EnumerateArray())
            {
                list.Add(new MonitorRegion
                {
                    X      = r.Int("x")      ?? 0,
                    Y      = r.Int("y")      ?? 0,
                    Width  = r.Int("width")  ?? r.Int("w") ?? 0,
                    Height = r.Int("height") ?? r.Int("h") ?? 0,
                    Label  = r.Str("label")
                });
            }
            return list;
        }

        private static ApexResult MonitorOk(string action, RegionMonitor m) =>
            new()
            {
                Success = true, Action = action,
                Data = new Dictionary<string, string>
                {
                    ["monitor"] = JsonSerializer.Serialize(m, FormatAdapter.s_compact)
                }
            };
    }
}
