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
        private readonly Dictionary<int, int>               _elementParents = new();  // child id -> parent id; used to walk up to a live ancestor when an element goes stale (graceful-fallback recovery)
        private readonly Queue<int>                         _elementInsertOrder = new();  // FIFO tracker so the 50k cap evicts oldest IDs instead of nuking the whole map
        private readonly Dictionary<int, Window>            _windowMap     = new();
        private IntPtr _mappedWindowHandle = IntPtr.Zero;

        private AutomationElement? _currentElement;
        // Captured at the moment CurrentElement is assigned, while the element is still fresh
        // and reverse-mappable. Stale UIA proxies fail equality/hash lookups, so we can't rely
        // on _elementReverse to recover the id later - we have to remember it up front.
        private int?               _currentElementId;
        public AutomationElement? CurrentElement
        {
            get => _currentElement;
            private set
            {
                _currentElement = value;
                _currentElementId = (value != null && _elementReverse.TryGetValue(value, out int id))
                    ? id
                    : (int?)null;
            }
        }
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

        /// <summary>
        /// Graceful-fallback recovery for a stale element. Walks up the cached parent chain
        /// (populated during /elements scans) and returns the first ancestor that is still
        /// valid. Lets callers complete actions like 'keys' or 'focus' against a live ancestor
        /// instead of getting an outright failure when the target app refreshes the tree
        /// (a common case for browser canvases and SPA route changes).
        ///
        /// Returns (null, null, hops) when no live ancestor is cached - caller should then
        /// emit the original stale-element error so the agent re-scans.
        /// </summary>
        private (AutomationElement? element, int? id, int hops) TryRecoverViaParent(int staleId)
        {
            int hops = 0;
            int currentId = staleId;
            while (_elementParents.TryGetValue(currentId, out int parentId))
            {
                hops++;
                if (_elementMap.TryGetValue(parentId, out var parent) && IsElementValid(parent))
                    return (parent, parentId, hops);
                currentId = parentId;
                if (hops > 20) break; // sanity cap - typical UIA trees are <15 deep; guards against pathological cycles
            }
            return (null, null, hops);
        }

        /// <summary>
        /// Lookup variant for cases where the caller has the AutomationElement reference but
        /// not its cached id (e.g. the /exec stale-check site, where target may be CurrentElement).
        /// Wrapped in try/catch because UIA equality on a stale COM proxy can throw.
        /// </summary>
        private (AutomationElement? element, int? id, int hops) TryRecoverViaParent(AutomationElement stale)
        {
            int staleId;
            try
            {
                if (!_elementReverse.TryGetValue(stale, out staleId)) return (null, null, 0);
            }
            catch { return (null, null, 0); }
            return TryRecoverViaParent(staleId);
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

