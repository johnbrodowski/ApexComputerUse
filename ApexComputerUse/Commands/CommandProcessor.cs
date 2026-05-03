using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;

namespace ApexComputerUse
{
    // CommandRequest, CommandResponse -> CommandRequest.cs

    public partial class CommandProcessor : IDisposable
    {
        private readonly ApexHelper _helper = new();
        private OcrHelper?   _ocr;
        private MtmdHelper?  _mtmd;

        private readonly ElementIdGenerator _idGen = new() { UseIncrementalIds = false };
        private readonly Dictionary<int, AutomationElement> _elementMap    = new();
        private readonly Dictionary<int, string>            _elementHashes = new();   // parallel to _elementMap - stores each element's hash so a subtree can be re-scanned without re-walking from the window root
        private readonly Dictionary<AutomationElement, int> _elementReverse = new();  // fast-path for /find ID recovery; default equality uses UIA CompareElements via AutomationElement.Equals
        private readonly Queue<int>                         _elementInsertOrder = new();  // FIFO tracker so the 50k cap evicts oldest IDs instead of nuking the whole map
        private readonly Dictionary<int, Window>            _windowMap     = new();
        private IntPtr _mappedWindowHandle = IntPtr.Zero;

        public AutomationElement? CurrentElement { get; private set; }
        public Window?            CurrentWindow  { get; private set; }

        /// <summary>
        /// Sets the current window/element from external callers (e.g. the Form1 GUI tab's
        /// interactive Find flow, where fuzzy matching requires a user confirmation dialog
        /// that doesn't fit cleanly inside <see cref="CmdFind"/>). Takes <c>_stateLock</c> so
        /// the assignment is serialized against concurrent remote commands.
        /// </summary>
        public void SetCurrentTarget(Window? window, AutomationElement? element)
        {
            lock (_stateLock)
            {
                CurrentWindow  = window;
                CurrentElement = element;
                _windowDesc    = window  == null ? "(none)" : (window.Properties.Name.ValueOrDefault ?? "(none)");
                _elementDesc   = element == null ? "(none)" : _helper.Describe(element);
            }
        }

        /// <summary>True when the AI/multimodal model has been loaded and is ready.</summary>
        public bool IsModelLoaded => _mtmd?.IsInitialized == true;

        /// <summary>
        /// True while the model is generating a response (inference in progress).
        /// Written from the inference worker, read from the UI status timer -
        /// marked <c>volatile</c> so cross-thread reads see a fresh value without a lock.
        /// </summary>
        private volatile bool _isProcessing;
        public bool IsProcessing => _isProcessing;

        private string _windowDesc  = "(none)";
        private string _elementDesc = "(none)";
        /// <summary>
        /// Guards all state mutations (CurrentElement, CurrentWindow, element/window maps).
        /// AI inference commands capture a state snapshot and then run <em>outside</em> this
        /// lock so that 30-second inference runs do not block find/execute/status/etc.
        /// </summary>
        private readonly object _stateLock = new();

        /// Fired on every command for display in the form's log.
        public event Action<string>? OnLog;

        /// <summary>Injected by Form1 after construction. Required for scene commands.</summary>
        public SceneStore? SceneStore { get; set; }

        // -- Entry point ---------------------------------------------------

        public CommandResponse Process(CommandRequest req)
        {
            string cmd = req.Command.ToLowerInvariant();

            // AI inference (describe, ask, file, init) runs OUTSIDE _stateLock.
            // Inference can take 30+ seconds; holding the lock that long would block
            // every other command (find, execute, status, ...).
            // Serialisation of the model itself is handled by MtmdHelper's SemaphoreSlim.
            if (cmd == "ai")
            {
                string action = (req.Action ?? "").ToLowerInvariant();
                if (action is "describe" or "ask" or "file" or "init")
                {
                    // Snapshot mutable state under a brief lock acquisition.
                    AutomationElement? elementSnapshot;
                    lock (_stateLock) { elementSnapshot = CurrentElement; }

                    try
                    {
                        CommandResponse r = action switch
                        {
                            "describe" => CmdAiDescribe(req, elementSnapshot),
                            "ask"      => CmdAiAsk(req, elementSnapshot),
                            "file"     => CmdAiFile(req),
                            "init"     => CmdAiInit(req),
                            _          => Fail("unreachable")
                        };
                        OnLog?.Invoke($"[Remote] ai/{action}: {r.Message}");
                        return r;
                    }
                    catch (Exception ex)
                    {
                        OnLog?.Invoke($"[Remote Error] {ex.Message}");
                        return Fail(ex.Message);
                    }
                }
            }

            lock (_stateLock)
            {
                try
                {
                    var response = cmd switch
                    {
                        "find"              => CmdFind(req),
                        "execute" or "exec" => CmdExecute(req),
                        "ocr"               => CmdOcr(req),
                        "ai"                => CmdAi(req),       // only reaches here for "status"
                        "status"            => CmdStatus(),
                        "windows"           => CmdListWindows(),
                        "elements"          => CmdListElements(req),
                        "uimap"             => CmdRenderMap(),
                        "draw"              => CmdDraw(req),
                        "scene"             => CmdScene(req),
                        "help"              => CmdHelp(),
                        "capture"           => CmdCapture(req),
                        _ => Fail($"Unknown command '{req.Command}'. Try 'help'.")
                    };
                    OnLog?.Invoke($"[Remote] {req.Command}: {response.Message}");
                    return response;
                }
                catch (Exception ex)
                {
                    OnLog?.Invoke($"[Remote Error] {ex.Message}");
                    return Fail(ex.Message);
                }
            }
        }



        private static ControlType? ResolveControlType(string? name)
        {
            if (string.IsNullOrWhiteSpace(name) || name == "All") return null;
            return Enum.TryParse<ControlType>(name, true, out var ct) ? ct : null;
        }

        private static CommandResponse Ok(string msg, string? data = null) =>
            new() { Success = true,  Message = msg, Data = data };

        // Variant that attaches structured extras (merged into the final response Data dict
        // by ApexResult.From). Used for actions like gettext that want to expose the source
        // UIA pattern alongside the bare text.
        private static CommandResponse OkWithExtras(string msg, string? data, Dictionary<string, string> extras) =>
            new() { Success = true, Message = msg, Data = data, Extras = extras };

        // Failure variant that attaches structured error data (merged into ApexResult.ErrorData
        // and surfaced as the response field "error_data"). Lets pattern-using actions report
        // supported_patterns / element_state without losing the human-readable error string.
        private static CommandResponse FailWithData(string msg, Dictionary<string, object> errorData) =>
            new() { Success = false, Message = msg, ErrorData = errorData };

        /// <summary>
        /// Returns false if the cached AutomationElement is no longer valid (e.g. the
        /// target app closed or navigated away).  Uses a lightweight property read so the
        /// COM call fails fast on stale references rather than hanging.
        /// </summary>
        public static bool IsElementValid(AutomationElement el)
        {
            try { return el.Properties.ProcessId.ValueOrDefault > 0; }
            catch { return false; }
        }

        private static CommandResponse Fail(string msg) =>
            new() { Success = false, Message = msg };

        private static string Do(Action action) { action(); return ""; }

        private static int ParseIntOr(string s, int fallback) =>
            int.TryParse(s, out int n) ? n : fallback;

        private static double ParseDoubleOr(string s, double fallback) =>
            double.TryParse(s, out double d) ? d : fallback;

        /// <summary>Parses "a,b" into two ints (for offset / grid coordinates).</summary>
        private static (int a, int b) ParsePair(string s)
        {
            var parts = s.Split(',');
            if (parts.Length < 2) return (ParseIntOr(s, 0), 0);
            return (ParseIntOr(parts[0].Trim(), 0), ParseIntOr(parts[1].Trim(), 0));
        }

        /// <summary>Parses "a,b" into two doubles (for scroll percent).</summary>
        private static (double a, double b) ParsePairD(string s)
        {
            var parts = s.Split(',');
            if (parts.Length < 2) return (ParseDoubleOr(s, 0), 0);
            return (ParseDoubleOr(parts[0].Trim(), 0), ParseDoubleOr(parts[1].Trim(), 0));
        }

        private static string CaptureFolder() =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Apex_Captures");

        public void Dispose()
        {
            _helper.Dispose();
            _ocr?.Dispose();
            _mtmd?.Dispose();
        }
    }
}

