namespace ApexUIBridge.TestRunner;

using System.Diagnostics;

/// <summary>Runs <c>dotnet build</c> without blocking the calling thread.</summary>
public sealed class BuildRunner
{
    private readonly string _solutionPath;
    private readonly string _configuration;

    public BuildRunner(string solutionPath, string configuration = "Debug")
    {
        _solutionPath  = solutionPath;
        _configuration = configuration;
    }

    public async Task<BuildResult> BuildAsync(CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName               = "dotnet",
            Arguments              = $"build \"{_solutionPath}\" -c {_configuration} --no-restore -v minimal",
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
            CreateNoWindow         = true,
        };

        using var proc = Process.Start(psi)
            ?? throw new InvalidOperationException("dotnet build process failed to start.");

        // Read stdout/stderr concurrently so we don't deadlock on full buffers
        var stdoutTask = proc.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = proc.StandardError.ReadToEndAsync(ct);

        await proc.WaitForExitAsync(ct).ConfigureAwait(false);

        var output = await stdoutTask + await stderrTask;
        return new BuildResult(proc.ExitCode == 0, output, proc.ExitCode);
    }
}

public sealed record BuildResult(bool Success, string Output, int ExitCode);
