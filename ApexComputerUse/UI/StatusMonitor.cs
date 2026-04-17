using System.Diagnostics;
using System.Net.NetworkInformation;

namespace ApexComputerUse
{
    internal sealed class StatusMonitor : IDisposable
    {
        private readonly ToolStripStatusLabel _lblCpu, _lblRam, _lblModel, _lblNet;
        private readonly CommandProcessor _processor;
        private readonly System.Windows.Forms.Timer _timer = new() { Interval = 2000 };
        private PerformanceCounter? _cpuCounter;
        private long _netBytesPrev = -1;

        // Cached set of up-adapters; enumerating all NICs costs ~10ms on machines with many adapters.
        // Refreshed only when the OS reports an address change rather than re-enumerating every tick.
        private NetworkInterface[] _cachedNics = Array.Empty<NetworkInterface>();
        private readonly NetworkAddressChangedEventHandler _nicChangedHandler;

        internal StatusMonitor(ToolStripStatusLabel lblCpu, ToolStripStatusLabel lblRam, ToolStripStatusLabel lblModel, ToolStripStatusLabel lblNet, CommandProcessor processor)
        {
            _lblCpu = lblCpu; _lblRam = lblRam; _lblModel = lblModel; _lblNet = lblNet;
            _processor = processor;

            _nicChangedHandler = (_, _) => RefreshNicCache();
            _timer.Tick += Tick;

            RefreshNicCache();
            NetworkChange.NetworkAddressChanged += _nicChangedHandler;
            _timer.Start();

            Task.Run(() =>
            {
                try { _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total"); }
                catch { /* not available in all environments */ }
            });
        }

        private void Tick(object? sender, EventArgs e)
        {
            // CPU
            try
            {
                if (_cpuCounter != null)
                    _lblCpu.Text = $"CPU: {_cpuCounter.NextValue():0}%";
            }
            catch { _lblCpu.Text = "CPU: --"; }

            // RAM (process working set)
            try
            {
                long ramMb = Process.GetCurrentProcess().WorkingSet64 / 1024 / 1024;
                _lblRam.Text = $"RAM: {ramMb} MB";
            }
            catch { _lblRam.Text = "RAM: --"; }

            // Model state
            if (_processor.IsProcessing)
                _lblModel.Text = "Model: ⚙ Processing";
            else if (_processor.IsModelLoaded)
                _lblModel.Text = "Model: Loaded";
            else
                _lblModel.Text = "Model: --";

            // Network (total bytes/sec across all adapters).
            // Iterates the cached adapter array; refreshed on NetworkAddressChanged.
            try
            {
                long totalBytes = 0;
                foreach (var nic in _cachedNics)
                    if (nic.OperationalStatus == OperationalStatus.Up)
                    {
                        var stats = nic.GetIPv4Statistics();
                        totalBytes += stats.BytesSent + stats.BytesReceived;
                    }

                if (_netBytesPrev >= 0)
                {
                    long delta = totalBytes - _netBytesPrev;
                    double kbps = delta / 1024.0 / (_timer.Interval / 1000.0);
                    _lblNet.Text = kbps >= 1024
                        ? $"Net: {kbps / 1024:0.0} MB/s"
                        : $"Net: {kbps:0} KB/s";
                }
                _netBytesPrev = totalBytes;
            }
            catch { _lblNet.Text = "Net: --"; }
        }

        private void RefreshNicCache()
        {
            try { _cachedNics = NetworkInterface.GetAllNetworkInterfaces(); }
            catch { _cachedNics = Array.Empty<NetworkInterface>(); }
            // Force recalc of the bytes delta — counters on the new adapter set aren't
            // comparable to the old totals, so skip this tick's kbps display.
            _netBytesPrev = -1;
        }

        public void Dispose()
        {
            _timer.Stop();
            _timer.Dispose();
            _cpuCounter?.Dispose();
            NetworkChange.NetworkAddressChanged -= _nicChangedHandler;
        }
    }
}
