using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ApexComputerUse
{
    public partial class HttpCommandServer
    {
        // New route handlers 

        private ApexResult HandleDrawDemo(HttpListenerRequest req)
        {
            bool overlay = string.Equals(req.QueryString["overlay"], "true",
                               StringComparison.OrdinalIgnoreCase);
            int overlayMs = int.TryParse(req.QueryString["ms"], out int ms) ? ms : 6000;

            var scene = AIDrawingCommand.BuildSpaceScene();
            scene.Overlay   = overlay;
            scene.OverlayMs = overlayMs;

            string base64 = AIDrawingCommand.Render(scene);

            if (overlay)
                Application.OpenForms[0]?.BeginInvoke(() => AIDrawingCommand.ShowOverlay(scene));

            string msg = $"Space scene rendered ({scene.Shapes.Count} shapes)." +
                         (overlay ? $" Overlay showing for {overlayMs / 1000.0:0.#}s (Esc to dismiss)." : "");
            return new ApexResult
            {
                Success = true,
                Action  = "draw/demo",
                Data    = new Dictionary<string, string> { ["result"] = base64, ["message"] = msg }
            };
        }

        // /health

        private ApexResult HandleHealth()
        {
            var up = DateTime.UtcNow - _startTime;
            return new ApexResult
            {
                Success = true,
                Action  = "health",
                Data    = new Dictionary<string, string>
                {
                    ["status"]           = "ok",
                    ["uptime"]           = $"{(int)up.TotalHours:D2}:{up.Minutes:D2}:{up.Seconds:D2}",
                    ["model_loaded"]     = _processor.IsModelLoaded.ToString(),
                    ["model_processing"] = _processor.IsProcessing.ToString(),
                    ["active_requests"]  = Volatile.Read(ref _activeRequests).ToString(),
                    ["total_requests"]   = Volatile.Read(ref _totalRequests).ToString(),
                    ["error_requests"]   = Volatile.Read(ref _errorRequests).ToString(),
                }
            };
        }

        // /metrics

        private ApexResult HandleMetrics()
        {
            var routes = _routeCounts.Keys.OrderByDescending(k => _routeCounts[k])
                .ToDictionary(
                    k => k,
                    k => new
                    {
                        count      = _routeCounts[k],
                        last_ms    = _routeLastLatencyMs.TryGetValue(k, out double ms) ? Math.Round(ms, 1) : 0.0
                    });

            return new ApexResult
            {
                Success = true,
                Action  = "metrics",
                Data    = new Dictionary<string, string>
                {
                    ["total_requests"]   = Volatile.Read(ref _totalRequests).ToString(),
                    ["error_requests"]   = Volatile.Read(ref _errorRequests).ToString(),
                    ["active_requests"]  = Volatile.Read(ref _activeRequests).ToString(),
                    ["routes"]           = JsonSerializer.Serialize(routes, FormatAdapter.s_compact)
                }
            };
        }

        private static ApexResult HandlePing() => new()
        {
            Success = true,
            Action  = "ping",
            Data    = new Dictionary<string, string>
            {
                ["status"]    = "ok",
                ["timestamp"] = DateTimeOffset.UtcNow.ToString("O")
            }
        };

        private static ApexResult HandleSysinfo() => new()
        {
            Success = true,
            Action  = "sysinfo",
            Data    = new Dictionary<string, string>
            {
                ["os"]        = Environment.OSVersion.ToString(),
                ["machine"]   = Environment.MachineName,
                ["user"]      = Environment.UserName,
                ["domain"]    = Environment.UserDomainName,
                ["cpu_count"] = Environment.ProcessorCount.ToString(),
                ["clr"]       = Environment.Version.ToString(),
                ["is64bit"]   = Environment.Is64BitOperatingSystem.ToString(),
                ["cwd"]       = Environment.CurrentDirectory
            }
        };

        private static ApexResult HandleEnv()
        {
            var data = new Dictionary<string, string>();
            foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
            {
                string key = entry.Key?.ToString() ?? "";
                if (!key.StartsWith("APEX_", StringComparison.OrdinalIgnoreCase)) continue;
                string value = entry.Value?.ToString() ?? "";
                data[key] = IsSensitiveEnvKey(key) ? "(redacted)" : value;
            }
            return new ApexResult { Success = true, Action = "env", Data = data };
        }

        private static ApexResult HandleLs(string? requestedPath)
        {
            string dir;
            try
            {
                dir = string.IsNullOrWhiteSpace(requestedPath)
                    ? Environment.CurrentDirectory
                    : Path.GetFullPath(requestedPath);   // canonicalize to prevent traversal
            }
            catch
            {
                return new ApexResult { Success = false, Action = "ls", Error = "Invalid path." };
            }

            if (!Directory.Exists(dir))
                return new ApexResult { Success = false, Action = "ls",
                    Error = $"Directory not found: {dir}" };

            var entries = new List<string>();
            foreach (string d in Directory.EnumerateDirectories(dir))
                entries.Add(Path.GetFileName(d) + "/");
            foreach (string f in Directory.EnumerateFiles(dir))
                entries.Add(Path.GetFileName(f));

            return new ApexResult
            {
                Success = true,
                Action  = "ls",
                Data    = new Dictionary<string, string>
                {
                    ["path"]    = dir,
                    ["entries"] = string.Join("\n", entries)
                }
            };
        }

        private static async Task<ApexResult> HandleRunAsync(string? cmd)
        {
            if (string.IsNullOrWhiteSpace(cmd))
                return new ApexResult { Success = false, Action = "run",
                    Error = "cmd parameter is required" };

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var psi = new ProcessStartInfo
            {
                FileName               = "cmd.exe",
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute        = false,
                CreateNoWindow         = true
            };
            // Pass via ArgumentList so cmd is quoted as a single token, preventing
            // cmd.exe from parsing outer shell metacharacters injected by callers.
            psi.ArgumentList.Add("/c");
            psi.ArgumentList.Add(cmd!);

            using var proc = new Process { StartInfo = psi };
            proc.Start();

            // Read both streams concurrently to avoid deadlock when one fills its OS pipe buffer.
            Task<string> stdoutTask = proc.StandardOutput.ReadToEndAsync();
            Task<string> stderrTask = proc.StandardError.ReadToEndAsync();
            try
            {
                await proc.WaitForExitAsync(cts.Token);
                await Task.WhenAll(stdoutTask, stderrTask);
            }
            catch (OperationCanceledException)
            {
                try { if (!proc.HasExited) proc.Kill(entireProcessTree: true); } catch { }
                return new ApexResult { Success = false, Action = "run",
                    Error = "Process timed out after 30 seconds" };
            }

            string stdout = await stdoutTask;
            string stderr = await stderrTask;
            int exit = proc.ExitCode;
            return new ApexResult
            {
                Success = exit == 0,
                Action  = "run",
                Data    = new Dictionary<string, string>
                {
                    ["cmd"]       = cmd!,
                    ["stdout"]    = stdout,
                    ["stderr"]    = stderr,
                    ["exit_code"] = exit.ToString()
                },
                Error = exit == 0 ? null : $"Process exited with code {exit}"
            };
        }

        private static bool IsSensitiveEnvKey(string key)
        {
            return key.Contains("KEY", StringComparison.OrdinalIgnoreCase)
                || key.Contains("TOKEN", StringComparison.OrdinalIgnoreCase)
                || key.Contains("SECRET", StringComparison.OrdinalIgnoreCase)
                || key.Contains("PASSWORD", StringComparison.OrdinalIgnoreCase);
        }

        private sealed class PayloadTooLargeException : Exception
        {
            public PayloadTooLargeException(string message) : base(message) { }
        }

        private static async Task<string> ReadBodyWithLimitAsync(HttpListenerRequest req, int maxBytes)
        {
            using var ms = new MemoryStream();
            var buffer = new byte[8192];
            while (true)
            {
                int read = await req.InputStream.ReadAsync(buffer.AsMemory(0, buffer.Length));
                if (read <= 0) break;
                if (ms.Length + read > maxBytes)
                    throw new PayloadTooLargeException($"Request body too large. Limit is {maxBytes} bytes.");
                ms.Write(buffer, 0, read);
            }
            var encoding = req.ContentEncoding ?? Encoding.UTF8;
            return encoding.GetString(ms.ToArray());
        }

    }
}
