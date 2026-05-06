using System.Text.Json;

namespace ApexComputerUse
{
    public partial class CommandProcessor
    {
        // -- RegionMonitor verbs ---------------------------------------------
        // Single umbrella command "monitor" with an Action sub-verb so non-HTTP callers
        // (named pipe, Telegram) can drive the same calibration loop. The HTTP server
        // exposes a richer REST surface in HttpCommandServer.MonitorRoutes.

        private CommandResponse CmdMonitor(CommandRequest req)
        {
            if (RegionMonitors == null) return Fail("RegionMonitorStore not initialised.");
            string action = (req.Action ?? "list").ToLowerInvariant();

            return action switch
            {
                "list"   => MonitorList(),
                "get"    => MonitorGet(req),
                "delete" => MonitorDelete(req),
                "start"  => MonitorToggle(req, enabled: true),
                "stop"   => MonitorToggle(req, enabled: false),
                "check"  => MonitorCheckOnce(req),
                _        => Fail($"Unknown monitor action '{action}'. Try list|get|delete|start|stop|check."),
            };
        }

        private CommandResponse MonitorList() =>
            Ok($"{RegionMonitors!.List().Length} monitor(s)",
               JsonSerializer.Serialize(RegionMonitors!.List(), FormatAdapter.s_compact));

        private CommandResponse MonitorGet(CommandRequest req)
        {
            string id = req.Value ?? req.AutomationId ?? "";
            if (string.IsNullOrEmpty(id)) return Fail("monitor get requires id in value field.");
            var m = RegionMonitors!.Get(id);
            return m == null ? Fail($"RegionMonitor '{id}' not found.")
                             : Ok("ok", JsonSerializer.Serialize(m, FormatAdapter.s_compact));
        }

        private CommandResponse MonitorDelete(CommandRequest req)
        {
            string id = req.Value ?? req.AutomationId ?? "";
            if (string.IsNullOrEmpty(id)) return Fail("monitor delete requires id in value field.");
            bool ok = RegionMonitors!.Delete(id);
            MonitorRunner?.Sync();
            return ok ? Ok($"Deleted monitor '{id}'") : Fail($"RegionMonitor '{id}' not found.");
        }

        private CommandResponse MonitorToggle(CommandRequest req, bool enabled)
        {
            string id = req.Value ?? req.AutomationId ?? "";
            if (string.IsNullOrEmpty(id)) return Fail("monitor start/stop requires id in value field.");
            try
            {
                var m = RegionMonitors!.Update(id, enabled: enabled);
                MonitorRunner?.Sync();
                return Ok(enabled ? $"Started monitor '{m.Name}'" : $"Stopped monitor '{m.Name}'",
                          JsonSerializer.Serialize(m, FormatAdapter.s_compact));
            }
            catch (KeyNotFoundException) { return Fail($"RegionMonitor '{id}' not found."); }
        }

        private CommandResponse MonitorCheckOnce(CommandRequest req)
        {
            string id = req.Value ?? req.AutomationId ?? "";
            if (string.IsNullOrEmpty(id)) return Fail("monitor check requires id in value field.");
            if (MonitorRunner == null) return Fail("MonitorRunner not active (HTTP server not started).");
            try
            {
                double[] diffs = MonitorRunner.CheckOnce(id);
                return Ok($"checked {diffs.Length} region(s)",
                          JsonSerializer.Serialize(diffs, FormatAdapter.s_compact));
            }
            catch (KeyNotFoundException) { return Fail($"RegionMonitor '{id}' not found."); }
        }
    }
}
