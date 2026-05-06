using System.Text.Json;

namespace ApexComputerUse
{
    /// <summary>
    /// Thread-safe, JSON-persisted store for <see cref="RegionMonitor"/> objects.
    /// One file per monitor at <c>%LOCALAPPDATA%\ApexComputerUse\monitors\{id}.json</c>.
    /// Mirrors <see cref="RegionMapStore"/> / <see cref="SceneStore"/> shape so all CRUD
    /// callers behave consistently.
    /// </summary>
    public sealed class RegionMonitorStore
    {
        private static readonly string DefaultDir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                         "ApexComputerUse", "monitors");

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            WriteIndented               = true,
            PropertyNameCaseInsensitive = true
        };

        private readonly string _dir;
        private readonly Dictionary<string, RegionMonitor> _monitors = new();
        private readonly object _lock = new();

        public RegionMonitorStore(string? dir = null)
        {
            _dir = dir ?? DefaultDir;
            Directory.CreateDirectory(_dir);
            LoadAllFromDisk();
        }

        // -- CRUD -----------------------------------------------------------

        public RegionMonitor Create(RegionMonitor proto)
        {
            if (proto == null) throw new ArgumentNullException(nameof(proto));
            lock (_lock)
            {
                string id = UniqueId();
                var m = new RegionMonitor
                {
                    Id           = id,
                    Name         = string.IsNullOrEmpty(proto.Name) ? "Untitled" : proto.Name,
                    Regions      = proto.Regions ?? new List<MonitorRegion>(),
                    IntervalMs   = Math.Max(100, proto.IntervalMs == 0 ? 1000 : proto.IntervalMs),
                    ThresholdPct = Math.Clamp(proto.ThresholdPct <= 0 ? 5.0 : proto.ThresholdPct, 0.01, 100.0),
                    Tolerance    = Math.Clamp(proto.Tolerance == 0 ? 8 : proto.Tolerance, 0, 255),
                    Enabled      = proto.Enabled,
                    Notes        = proto.Notes,
                };
                _monitors[id] = m;
                SaveToDisk(m);
                return m;
            }
        }

        public RegionMonitor? Get(string id)
        {
            lock (_lock) return _monitors.TryGetValue(id, out var m) ? m : null;
        }

        public RegionMonitor[] List()
        {
            lock (_lock) return _monitors.Values.ToArray();
        }

        public RegionMonitor Update(string id,
                                    string? name = null,
                                    List<MonitorRegion>? regions = null,
                                    int? intervalMs = null,
                                    double? thresholdPct = null,
                                    int? tolerance = null,
                                    bool? enabled = null,
                                    string? notes = null)
        {
            lock (_lock)
            {
                if (!_monitors.TryGetValue(id, out var m))
                    throw new KeyNotFoundException($"RegionMonitor '{id}' not found.");
                if (name         != null) m.Name         = name;
                if (regions      != null) m.Regions      = regions;
                if (intervalMs   != null) m.IntervalMs   = Math.Max(100, intervalMs.Value);
                if (thresholdPct != null) m.ThresholdPct = Math.Clamp(thresholdPct.Value, 0.01, 100.0);
                if (tolerance    != null) m.Tolerance    = Math.Clamp(tolerance.Value, 0, 255);
                if (enabled      != null) m.Enabled      = enabled.Value;
                if (notes        != null) m.Notes        = notes;
                m.Touch();
                SaveToDisk(m);
                return m;
            }
        }

        /// <summary>
        /// Persist transient runtime telemetry without rate-limiting writes. Called by the
        /// runner when a fire occurs. Uses lightweight serialization on the same file.
        /// </summary>
        public void RecordFire(string id, int regionIndex, double percentDiff)
        {
            lock (_lock)
            {
                if (!_monitors.TryGetValue(id, out var m)) return;
                m.LastFiredUtc    = DateTime.UtcNow;
                m.LastPercentDiff = percentDiff;
                m.LastRegionIndex = regionIndex;
                m.HitCount       += 1;
                m.Touch();
                SaveToDisk(m);
            }
        }

        public bool Delete(string id)
        {
            lock (_lock)
            {
                if (!_monitors.Remove(id)) return false;
                try
                {
                    string path = Path.Combine(_dir, id + ".json");
                    if (File.Exists(path)) File.Delete(path);
                }
                catch { /* tolerate transient disk errors; in-memory state is authoritative */ }
                return true;
            }
        }

        // -- Persistence ----------------------------------------------------

        private void SaveToDisk(RegionMonitor m)
        {
            try
            {
                string path = Path.Combine(_dir, m.Id + ".json");
                string tmp  = path + ".tmp";
                File.WriteAllText(tmp, JsonSerializer.Serialize(m, JsonOpts));
                File.Move(tmp, path, overwrite: true);
            }
            catch (Exception ex)
            {
                AppLog.Warning($"[RegionMonitorStore] save failed for {m.Id}: {ex.Message}");
            }
        }

        private void LoadAllFromDisk()
        {
            try
            {
                if (!Directory.Exists(_dir)) return;
                foreach (var path in Directory.EnumerateFiles(_dir, "*.json"))
                {
                    try
                    {
                        var m = JsonSerializer.Deserialize<RegionMonitor>(
                            File.ReadAllText(path), JsonOpts);
                        if (m != null && !string.IsNullOrEmpty(m.Id))
                            _monitors[m.Id] = m;
                    }
                    catch (Exception ex)
                    {
                        AppLog.Warning($"[RegionMonitorStore] skipping corrupt monitor {path}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                AppLog.Warning($"[RegionMonitorStore] LoadAllFromDisk failed: {ex.Message}");
            }
        }

        private string UniqueId()
        {
            for (int i = 0; i < 10; i++)
            {
                string id = SceneIds.New();
                if (!_monitors.ContainsKey(id)) return id;
            }
            return Guid.NewGuid().ToString("N").Substring(0, 8);
        }
    }
}
