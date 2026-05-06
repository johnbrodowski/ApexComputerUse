using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace ApexComputerUse
{
    /// <summary>
    /// Background dispatcher that polls every enabled <see cref="RegionMonitor"/> on its own
    /// cadence, captures each region, diffs against the previous capture, and fires a
    /// <c>monitor.fired</c> event on the SSE stream when any region exceeds its threshold.
    ///
    /// One Task per monitor, gated by <see cref="RegionMonitor.IntervalMs"/>. Overlapping
    /// ticks are skipped — a slow tick never queues another. First tick per region is the
    /// baseline (no fire). Disabled monitors are not polled.
    /// </summary>
    public sealed class RegionMonitorRunner : IDisposable
    {
        private readonly RegionMonitorStore _store;
        private readonly EventBroker        _events;
        private readonly object             _gate = new();
        private readonly Dictionary<string, MonitorWorker> _workers = new();
        private CancellationTokenSource?   _cts;
        private bool                       _disposed;

        public RegionMonitorRunner(RegionMonitorStore store, EventBroker events)
        {
            _store  = store;
            _events = events;
        }

        public void Start()
        {
            lock (_gate)
            {
                if (_cts != null) return;
                _cts = new CancellationTokenSource();
                foreach (var m in _store.List())
                    if (m.Enabled) StartWorkerLocked(m.Id);
            }
        }

        /// <summary>Re-evaluate workers. Call after any monitor's enabled flag changes.</summary>
        public void Sync()
        {
            lock (_gate)
            {
                if (_cts == null) return;
                var all = _store.List();
                var liveIds = new HashSet<string>(all.Select(m => m.Id));

                // Stop workers whose monitor was deleted or disabled.
                foreach (var id in _workers.Keys.ToList())
                {
                    var m = all.FirstOrDefault(x => x.Id == id);
                    if (m == null || !m.Enabled) StopWorkerLocked(id);
                }
                // Start workers for enabled monitors that aren't running.
                foreach (var m in all)
                    if (m.Enabled && !_workers.ContainsKey(m.Id)) StartWorkerLocked(m.Id);
            }
        }

        private void StartWorkerLocked(string id)
        {
            if (_cts == null || _workers.ContainsKey(id)) return;
            var worker = new MonitorWorker(id, _store, _events, _cts.Token);
            _workers[id] = worker;
            worker.Start();
        }

        private void StopWorkerLocked(string id)
        {
            if (_workers.Remove(id, out var w)) w.Stop();
        }

        /// <summary>One-shot manual check. Returns per-region percent diffs vs baseline.</summary>
        public double[] CheckOnce(string id)
        {
            var m = _store.Get(id) ?? throw new KeyNotFoundException($"RegionMonitor '{id}' not found.");
            var result = new double[m.Regions.Count];
            for (int i = 0; i < m.Regions.Count; i++)
            {
                var r = m.Regions[i];
                using var shot = CaptureRegion(r);
                if (shot == null) { result[i] = -1; continue; }
                MonitorWorker worker;
                lock (_gate) _workers.TryGetValue(id, out worker!);
                Bitmap? prev = worker?.GetBaseline(i);
                if (prev == null) { result[i] = -1; }
                else result[i] = DiffPercent(prev, shot, m.Tolerance);
            }
            return result;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            lock (_gate)
            {
                foreach (var w in _workers.Values) w.Stop();
                _workers.Clear();
                try { _cts?.Cancel(); } catch { }
                _cts?.Dispose();
                _cts = null;
            }
        }

        // -- Per-monitor worker --------------------------------------------

        private sealed class MonitorWorker
        {
            private readonly string             _id;
            private readonly RegionMonitorStore _store;
            private readonly EventBroker        _events;
            private readonly CancellationToken  _outerToken;
            private readonly CancellationTokenSource _localCts;
            private Bitmap?[]                   _baselines;     // per-region previous capture
            private Task?                       _task;

            public MonitorWorker(string id, RegionMonitorStore store, EventBroker events, CancellationToken outer)
            {
                _id         = id;
                _store      = store;
                _events     = events;
                _outerToken = outer;
                _localCts   = CancellationTokenSource.CreateLinkedTokenSource(outer);
                int count = store.Get(id)?.Regions.Count ?? 0;
                _baselines = new Bitmap?[count];
            }

            public void Start() => _task = Task.Run(() => RunAsync(_localCts.Token));

            public void Stop()
            {
                try { _localCts.Cancel(); } catch { }
                for (int i = 0; i < _baselines.Length; i++)
                {
                    _baselines[i]?.Dispose();
                    _baselines[i] = null;
                }
            }

            public Bitmap? GetBaseline(int regionIndex) =>
                regionIndex >= 0 && regionIndex < _baselines.Length ? _baselines[regionIndex] : null;

            private async Task RunAsync(CancellationToken token)
            {
                try
                {
                    while (!token.IsCancellationRequested)
                    {
                        var m = _store.Get(_id);
                        if (m == null || !m.Enabled) return;

                        try { TickOnce(m); }
                        catch (Exception ex)
                        {
                            AppLog.Debug($"[RegionMonitor:{m.Name}/{_id}] tick error: {ex.Message}");
                        }

                        int delay = Math.Max(100, m.IntervalMs);
                        try { await Task.Delay(delay, token).ConfigureAwait(false); }
                        catch (TaskCanceledException) { return; }
                    }
                }
                catch (Exception ex)
                {
                    AppLog.Warning($"[RegionMonitor:{_id}] worker crashed: {ex.Message}");
                }
            }

            private void TickOnce(RegionMonitor m)
            {
                for (int i = 0; i < m.Regions.Count; i++)
                {
                    var r = m.Regions[i];
                    Bitmap? shot = CaptureRegion(r);
                    if (shot == null) continue;

                    if (_baselines.Length != m.Regions.Count)
                    {
                        // Region count changed since worker started — resize, preserving prior baselines.
                        var resized = new Bitmap?[m.Regions.Count];
                        Array.Copy(_baselines, resized, Math.Min(_baselines.Length, resized.Length));
                        // Dispose any baselines that fell off the end of the array.
                        for (int j = m.Regions.Count; j < _baselines.Length; j++) _baselines[j]?.Dispose();
                        _baselines = resized;
                    }

                    var prev = _baselines[i];
                    if (prev == null)
                    {
                        // First capture — establish baseline, no fire.
                        _baselines[i] = shot;
                        continue;
                    }

                    double pct = DiffPercent(prev, shot, m.Tolerance);
                    if (pct >= m.ThresholdPct)
                    {
                        FireEvent(m, i, r, pct);
                        // Replace baseline so we don't keep re-firing on the same delta.
                        _baselines[i].Dispose();
                        _baselines[i] = shot;
                        _store.RecordFire(m.Id, i, pct);
                    }
                    else
                    {
                        // No fire — keep the older baseline so a slow drift can still cross
                        // the threshold over multiple ticks. Dispose the new capture.
                        shot.Dispose();
                    }
                }
            }

            private void FireEvent(RegionMonitor m, int idx, MonitorRegion r, double pct)
            {
                var data = new Dictionary<string, object?>
                {
                    ["monitorId"]   = m.Id,
                    ["name"]        = m.Name,
                    ["regionIndex"] = idx,
                    ["label"]       = r.Label,
                    ["x"]           = r.X,
                    ["y"]           = r.Y,
                    ["width"]       = r.Width,
                    ["height"]      = r.Height,
                    ["percentDiff"] = Math.Round(pct, 3),
                    ["threshold"]   = m.ThresholdPct
                };
                _events.Emit("monitor.fired", data);
            }
        }

        // -- Static helpers (also used by CheckOnce) -----------------------

        public static Bitmap? CaptureRegion(MonitorRegion r)
        {
            if (r.Width <= 0 || r.Height <= 0) return null;
            try
            {
                var bmp = new Bitmap(r.Width, r.Height, PixelFormat.Format32bppArgb);
                using var g = Graphics.FromImage(bmp);
                g.CopyFromScreen(new Point(r.X, r.Y), Point.Empty, new Size(r.Width, r.Height));
                return bmp;
            }
            catch (Exception ex)
            {
                AppLog.Debug($"[RegionMonitor] CaptureRegion({r.X},{r.Y},{r.Width}x{r.Height}) failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Returns the percent of pixels (0–100) whose max-channel-difference exceeds
        /// <paramref name="tolerance"/>. The two bitmaps must have identical dimensions.
        /// </summary>
        public static double DiffPercent(Bitmap a, Bitmap b, int tolerance)
        {
            if (a.Width != b.Width || a.Height != b.Height) return 100.0;
            int w = a.Width, h = a.Height;
            var rect = new Rectangle(0, 0, w, h);
            var dataA = a.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            var dataB = b.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                int stride = Math.Abs(dataA.Stride);
                int len = stride * h;
                byte[] arrA = new byte[len];
                byte[] arrB = new byte[len];
                Marshal.Copy(dataA.Scan0, arrA, 0, len);
                Marshal.Copy(dataB.Scan0, arrB, 0, len);

                long changed = 0;
                for (int y = 0; y < h; y++)
                {
                    int row = y * stride;
                    for (int x = 0; x < w; x++)
                    {
                        int i = row + x * 4;
                        int dB = Math.Abs(arrA[i]     - arrB[i]);
                        int dG = Math.Abs(arrA[i + 1] - arrB[i + 1]);
                        int dR = Math.Abs(arrA[i + 2] - arrB[i + 2]);
                        int max = dB > dG ? (dB > dR ? dB : dR) : (dG > dR ? dG : dR);
                        if (max > tolerance) changed++;
                    }
                }
                return (double)changed / (w * h) * 100.0;
            }
            finally
            {
                a.UnlockBits(dataA);
                b.UnlockBits(dataB);
            }
        }
    }
}
