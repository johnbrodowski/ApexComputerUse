using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;

namespace ApexComputerUse
{
    public partial class CommandProcessor
    {
        // -- AI (Multimodal) commands --------------------------------------

        // NOTE: describe/ask/file/init are intercepted in Process() and dispatched outside
        // _stateLock. Only "status" reaches this method through the locked code path.
        private CommandResponse CmdAi(CommandRequest req)
        {
            return (req.Action?.ToLowerInvariant() ?? "status") switch
            {
                "status" => CmdAiStatus(),
                _ => Fail($"Unknown ai action '{req.Action}'. Try: init, status, describe, file, ask")
            };
        }

        /// <summary>
        /// Loads the AI model asynchronously. Bypasses the command lock so the
        /// caller can properly await without deadlocking. Used by the Model tab UI.
        /// </summary>
        public async Task<CommandResponse> InitModelAsync(string modelPath, string projPath)
        {
            OnLog?.Invoke($"[AI Init] Loading model: {modelPath}");
            OnLog?.Invoke($"[AI Init] Loading projector: {projPath}");

            if (!File.Exists(modelPath))
                return Fail($"Model file not found: {modelPath}");
            if (!File.Exists(projPath))
                return Fail($"Projector file not found: {projPath}");

            _mtmd?.Dispose();
            _mtmd = null;

            try
            {
                var helper = new MtmdHelper(modelPath, projPath);
                await helper.InitializeAsync();
                _mtmd = helper;
                OnLog?.Invoke($"[AI Init] OK - Vision={_mtmd.SupportsVision} Audio={_mtmd.SupportsAudio}");
                return Ok($"AI ready. Vision={_mtmd.SupportsVision} Audio={_mtmd.SupportsAudio}");
            }
            catch (Exception ex)
            {
                _mtmd = null;
                string detail = BuildExceptionDetail(ex);
                OnLog?.Invoke($"[AI Init] FAILED: {detail}");
                return Fail($"Init failed: {detail}");
            }
        }

        private CommandResponse CmdAiInit(CommandRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.ModelPath))  return Fail("model= path required.");
            if (string.IsNullOrWhiteSpace(req.MmProjPath)) return Fail("proj= path required.");
            // Task.Run avoids a SynchronizationContext deadlock when called from the WinForms thread.
            return Task.Run(async () => await InitModelAsync(req.ModelPath!, req.MmProjPath!))
                        .GetAwaiter().GetResult();
        }

        /// Builds a readable exception message including all inner exceptions.
        private static string BuildExceptionDetail(Exception ex)
        {
            var sb = new System.Text.StringBuilder();
            var current = ex;
            int depth = 0;
            while (current != null && depth < 5)
            {
                if (depth > 0) sb.Append(" --> ");
                sb.Append($"[{current.GetType().Name}] {current.Message}");
                current = current.InnerException;
                depth++;
            }
            return sb.ToString();
        }

        private CommandResponse CmdAiStatus()
        {
            if (_mtmd == null || !_mtmd.IsInitialized)
                return Ok("AI not initialized. Use: ai action=init model=<path> proj=<path>");
            return Ok($"AI ready. Vision={_mtmd.SupportsVision} Audio={_mtmd.SupportsAudio}");
        }

        private CommandResponse CmdAiDescribe(CommandRequest req, AutomationElement? element)
        {
            if (_mtmd == null || !_mtmd.IsInitialized) return Fail("AI not initialized. Use ai action=init first.");
            if (element == null)                        return Fail("No element selected. Use 'find' first.");
            string prompt = req.Prompt ?? req.Value ?? "Describe what you see in this UI element.";
            _isProcessing = true;
            try
            {
                // Task.Run avoids a SynchronizationContext deadlock; safe on any calling thread.
                string result = Task.Run(async () => await _mtmd.DescribeElementAsync(element, prompt))
                                    .GetAwaiter().GetResult();
                return Ok("AI description", result);
            }
            finally { _isProcessing = false; }
        }

        private CommandResponse CmdAiFile(CommandRequest req)
        {
            if (_mtmd == null || !_mtmd.IsInitialized) return Fail("AI not initialized. Use ai action=init first.");
            string? path = req.Value;
            if (string.IsNullOrWhiteSpace(path)) return Fail("value=<file path> required.");

            // Resolve to a canonical absolute path to prevent directory traversal attacks.
            try   { path = Path.GetFullPath(path!); }
            catch { return Fail("Invalid file path."); }

            if (!File.Exists(path))
                return Fail($"File not found: {Path.GetFileName(path)}");

            string prompt = req.Prompt ?? "Describe this media.";
            _isProcessing = true;
            try
            {
                // Task.Run avoids a SynchronizationContext deadlock; safe on any calling thread.
                string result = Task.Run(async () => await _mtmd.DescribeImageAsync(path, prompt))
                                    .GetAwaiter().GetResult();
                return Ok($"AI file description ({Path.GetFileName(path)})", result);
            }
            finally { _isProcessing = false; }
        }

        private CommandResponse CmdAiAsk(CommandRequest req, AutomationElement? element)
        {
            if (_mtmd == null || !_mtmd.IsInitialized) return Fail("AI not initialized. Use ai action=init first.");
            if (element == null)                        return Fail("No element selected. Use 'find' first.");
            string prompt = req.Prompt ?? req.Value ?? "";
            if (string.IsNullOrWhiteSpace(prompt)) return Fail("prompt= required.");
            _isProcessing = true;
            try
            {
                // Task.Run avoids a SynchronizationContext deadlock; safe on any calling thread.
                string result = Task.Run(async () => await _mtmd.DescribeElementAsync(element, prompt))
                                    .GetAwaiter().GetResult();
                return Ok("AI answer", result);
            }
            finally { _isProcessing = false; }
        }

    }
}

