namespace ApexUIBridge.TestRunner;

using System.Diagnostics;

/// <summary>
/// Starts and stops a single external process without blocking.
/// GUI apps (WinForms, WPF) need UseShellExecute = false but CreateNoWindow = false
/// so their windows appear on the desktop for UIA scanning.
/// </summary>
public sealed class ProcessManager : IAsyncDisposable
{
    private Process? _process;
    private readonly string _name;
    private readonly string _exe;
    private readonly bool   _isGui;

    public ProcessManager(string name, string exePath, bool isGui = true)
    {
        _name  = name;
        _exe   = exePath;
        _isGui = isGui;
    }

    public Task StartAsync(CancellationToken ct = default,
        IReadOnlyDictionary<string, string>? env = null)
    {
        if (!File.Exists(_exe))
            throw new FileNotFoundException($"Executable not found: {_exe}", _exe);

        var psi = new ProcessStartInfo
        {
            FileName               = _exe,
            UseShellExecute        = false,
            CreateNoWindow         = !_isGui,
            RedirectStandardOutput = !_isGui,
            RedirectStandardError  = !_isGui,
        };
        if (env != null)
            foreach (var (k, v) in env) psi.Environment[k] = v;

        _process = Process.Start(psi)
            ?? throw new InvalidOperationException($"Failed to start {_name}");

        Console.WriteLine($"[{_name}] Started (PID {_process.Id})");
        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        if (_process is null || _process.HasExited) return;
        try
        {
            _process.Kill(entireProcessTree: true);
            await _process.WaitForExitAsync().ConfigureAwait(false);
            Console.WriteLine($"[{_name}] Stopped.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{_name}] Stop error: {ex.Message}");
        }
        finally
        {
            _process.Dispose();
            _process = null;
        }
    }

    public bool IsRunning => _process is { HasExited: false };

    public async ValueTask DisposeAsync() => await StopAsync();
}
