using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace ApexComputerUse
{
    /// <summary>
    /// Owns all file-download state for the Model tab (setup bundle + ad-hoc URL download).
    /// Form1 subscribes to <see cref="Status"/>/<see cref="Progress"/> to drive the progress bar and
    /// status label; the manager handles cancellation, partial-file cleanup, and the setup file list.
    /// </summary>
    public class DownloadManager
    {
        public readonly struct SetupFile
        {
            public readonly string Url;
            public readonly string RelPath;
            public readonly string Label;
            public SetupFile(string url, string relPath, string label)
            {
                Url = url; RelPath = relPath; Label = label;
            }
        }

        private static readonly SetupFile[] _setupFiles =
        [
            new("https://huggingface.co/LiquidAI/LFM2.5-VL-450M-GGUF/resolve/main/LFM2.5-VL-450M-Q4_0.gguf",
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "models", "LFM2.5-VL-450M-Q4_0.gguf"),
                "LFM2.5-VL model"),
            new("https://huggingface.co/LiquidAI/LFM2.5-VL-450M-GGUF/resolve/main/mmproj-LFM2.5-VL-450m-F16.gguf",
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "models", "mmproj-LFM2.5-VL-450m-F16.gguf"),
                "projector"),
            new("https://github.com/tesseract-ocr/tessdata/raw/refs/heads/main/eng.traineddata",
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tessdata", "eng.traineddata"),
                "eng.traineddata"),
        ];

        public static IReadOnlyList<SetupFile> SetupFiles => _setupFiles;

        private CancellationTokenSource? _cts;
        public bool IsRunning => _cts != null;

        /// <summary>(message, color) - emitted on the caller's thread for the ad-hoc stream callback,
        /// or on the UI sync context the caller installed. Form1 routes this into lblDownloadStatus.</summary>
        public event Action<string, Color>? Status;

        /// <summary>0..100</summary>
        public event Action<int>? Progress;

        public void Cancel() => _cts?.Cancel();

        /// <summary>Fires the setup-bundle run: iterates <see cref="SetupFiles"/>, skipping files
        /// already on disk. Returns true when every file ends up present, false on cancel/error.</summary>
        public async Task<bool> RunSetupAsync()
        {
            if (_cts != null) return false;
            _cts = new CancellationTokenSource();
            var ct = _cts.Token;
            try
            {
                for (int i = 0; i < _setupFiles.Length; i++)
                {
                    var f = _setupFiles[i];
                    string prefix = $"[{i + 1}/{_setupFiles.Length}]";

                    if (File.Exists(f.RelPath))
                    {
                        Status?.Invoke($"{prefix} {f.Label} already exists - skipping.", Color.Gray);
                        await Task.Delay(400, ct);
                        continue;
                    }

                    Progress?.Invoke(0);
                    Status?.Invoke($"{prefix} Downloading {f.Label}...", Color.DarkBlue);

                    await DownloadStreamAsync(
                        f.Url, f.RelPath,
                        (msg, col) => Status?.Invoke($"{prefix} {f.Label}: {msg}", col),
                        ct);
                }

                Progress?.Invoke(100);
                Status?.Invoke("All files downloaded.", Color.Green);
                return true;
            }
            catch (OperationCanceledException)
            {
                Status?.Invoke("Cancelled.", Color.Gray);
                CleanupPartialSetupFiles();
                return false;
            }
            catch (Exception ex)
            {
                Status?.Invoke($"Error: {ex.Message}", Color.Red);
                return false;
            }
            finally
            {
                _cts?.Dispose();
                _cts = null;
            }
        }

        /// <summary>Downloads a single URL to the specified path. Partial file is deleted on cancel.</summary>
        public async Task<bool> DownloadAsync(string url, string destPath)
        {
            if (_cts != null) return false;
            _cts = new CancellationTokenSource();
            var ct = _cts.Token;
            try
            {
                Progress?.Invoke(0);
                Status?.Invoke("Starting download...", Color.DarkBlue);

                await DownloadStreamAsync(url, destPath,
                    (msg, col) => Status?.Invoke(msg, col), ct);

                Progress?.Invoke(100);
                Status?.Invoke($"Done - {destPath}", Color.Green);
                return true;
            }
            catch (OperationCanceledException)
            {
                Status?.Invoke("Cancelled.", Color.Gray);
                if (File.Exists(destPath)) try { File.Delete(destPath); } catch { }
                return false;
            }
            catch (Exception ex)
            {
                Status?.Invoke($"Error: {ex.Message}", Color.Red);
                return false;
            }
            finally
            {
                _cts?.Dispose();
                _cts = null;
            }
        }

        private async Task DownloadStreamAsync(
            string url, string destPath,
            Action<string, Color> report,
            CancellationToken ct)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
            using var client = new HttpClient();
            using var resp = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            resp.EnsureSuccessStatusCode();

            long total = resp.Content.Headers.ContentLength ?? -1;
            using var src = await resp.Content.ReadAsStreamAsync(ct);
            using var dest = File.Create(destPath);

            var buffer = new byte[81920];
            long downloaded = 0;
            int read;
            while ((read = await src.ReadAsync(buffer, ct)) > 0)
            {
                await dest.WriteAsync(buffer.AsMemory(0, read), ct);
                downloaded += read;
                if (total > 0)
                {
                    int pct = (int)(downloaded * 100 / total);
                    Progress?.Invoke(pct);
                    report($"{downloaded / 1024 / 1024} MB / {total / 1024 / 1024} MB  ({pct}%)", Color.DarkBlue);
                }
            }
        }

        // A previous cancel may have left a tiny (headers-only) file behind. Remove anything
        // obviously incomplete so the next run-setup re-downloads it rather than skipping.
        private static void CleanupPartialSetupFiles()
        {
            foreach (var f in _setupFiles)
            {
                if (!File.Exists(f.RelPath)) continue;
                try { if (new FileInfo(f.RelPath).Length < 1024) File.Delete(f.RelPath); }
                catch { }
            }
        }
    }
}

