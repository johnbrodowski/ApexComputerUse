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

        // Captured descriptor (controlType + name + automationId) for each scanned element.
        // Used as a second recovery rung after parent-walk: if we walked >1 hop or landed on a
        // Window, we can scope a re-find under that ancestor by descriptor and prefer the unique
        // match over a generic ancestor handle. Cached at scan time when the element is fresh.
        private readonly Dictionary<int, ElementDescriptor> _elementDescriptors = new();

        private readonly record struct ElementDescriptor(string ControlType, string Name, string AutomationId);

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

        /// <summary>
        /// Injected by Form1 after construction. When non-null, the element-tree scan
        /// will skip elements whose hash is marked Excluded (unless the request explicitly
        /// asks for Unfiltered=true) and attach the persisted Note (if any) to each emitted node.
        /// </summary>
        public ElementAnnotationStore? ElementAnnotations { get; set; }

        /// <summary>Injected by Form1 after construction. Backs the regionmap commands.</summary>
        public RegionMapStore? RegionMaps { get; set; }

        /// <summary>Injected by Form1 after construction. Backs the region-monitor commands.</summary>
        public RegionMonitorStore? RegionMonitors { get; set; }

        /// <summary>Injected by HttpCommandServer after construction; null in service-only mode.</summary>
        public RegionMonitorRunner? MonitorRunner { get; set; }

        /// <summary>
        /// Looks up the cached element hash for a numeric id (only valid for elements
        /// that were emitted by the most recent /elements scan). Used by the annotation
        /// HTTP routes so callers can pass either an id (convenient) or a hash (stable).
        /// Held inside <see cref="_stateLock"/> to coordinate with concurrent scans.
        /// </summary>
        public bool TryResolveHash(int id, out string hash)
        {
            lock (_stateLock)
            {
                if (_elementHashes.TryGetValue(id, out var h) && !string.IsNullOrEmpty(h))
                {
                    hash = h;
                    return true;
                }
                hash = "";
                return false;
            }
        }

        /// <summary>
        /// Snapshot of the cached descriptor for a previously-scanned element id.
        /// Used by the annotation routes to enrich an annotation record with a
        /// human-readable label (controlType / name / automationId) at write time.
        /// </summary>
        public bool TryResolveDescriptor(int id, out string controlType, out string name, out string automationId)
        {
            lock (_stateLock)
            {
                if (_elementDescriptors.TryGetValue(id, out var d))
                {
                    controlType  = d.ControlType;
                    name         = d.Name;
                    automationId = d.AutomationId;
                    return true;
                }
                controlType = name = automationId = "";
                return false;
            }
        }

        /// <summary>
        /// Returns the absolute screen bounds of the currently-mapped CurrentWindow,
        /// or null if no window is selected. Used by /regionmap helpers to build
        /// canvas-sized DrawRequests that match the on-screen overlay.
        /// </summary>
        public (int x, int y, int w, int h)? GetCurrentWindowBounds()
        {
            lock (_stateLock)
            {
                if (CurrentWindow == null) return null;
                try
                {
                    var b = CurrentWindow.BoundingRectangle;
                    return ((int)b.X, (int)b.Y, (int)b.Width, (int)b.Height);
                }
                catch { return null; }
            }
        }

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
                        "annotate"          => CmdAnnotate(req),
                        "unannotate"        => CmdUnannotate(req),
                        "exclude"           => CmdExclude(req),
                        "unexclude"         => CmdUnexclude(req),
                        "annotations"       => CmdListAnnotations(req),
                        "excluded"          => CmdListExcluded(),
                        "regionmap"         => CmdRegionMap(req),
                        "monitor"           => CmdMonitor(req),
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

        // Soft-advisory variant: success path that also flags an in-band warning (graceful
        // fallback used, optional value field ignored, etc.). Surfaces as ApexResult.Data["warning"].
        private static CommandResponse OkWithWarning(string msg, string? data, string warning) =>
            new() { Success = true, Message = msg, Data = data, Warning = warning };

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

        // -- Diagnostics: swallowed-exception telemetry ----------------------
        // Many sites catch and intentionally fall through (stale UIA proxies, mid-enumeration
        // tree mutations, optional pattern probes). Historically those catches were silent;
        // we now bump a counter and emit a Debug log so failures stop being invisible.
        // Counter is exposed via /metrics for runtime diagnosis without requiring log scrape.
        private static long _swallowedExCount;
        public static long  SwallowedExceptions => Volatile.Read(ref _swallowedExCount);

        internal static void LogSwallowed(string site, Exception ex)
        {
            Interlocked.Increment(ref _swallowedExCount);
            AppLog.Debug($"[{site}] swallowed: {ex.GetType().Name}: {ex.Message}");
        }

        // -- Transient retry --------------------------------------------------
        // Wraps an action call so a single transient COM / UIA timeout doesn't immediately
        // surface as a failure. Browser SPAs and apps that rebuild their accessibility tree
        // between events emit these intermittently; one retry usually clears them.
        //
        // Retries ONLY on COMException, ElementNotAvailableException, TimeoutException -
        // i.e. signals of stale / busy proxies, not semantic failures. Semantic exceptions
        // (NoClickablePoint, NotSupportedException, ArgumentException, etc.) propagate
        // immediately so BuildErrorData can attach pattern hints to the response.
        //
        // attempts is 1-based total tries; default 2 = single retry. Cap at 5.
        private static T RetryTransient<T>(Func<T> op, int attempts = 2)
        {
            if (attempts < 1) attempts = 1;
            if (attempts > 5) attempts = 5;
            for (int i = 0; ; i++)
            {
                try { return op(); }
                catch (System.Runtime.InteropServices.COMException ex) when (i < attempts - 1)
                {
                    LogSwallowed("RetryTransient/COMException", ex);
                    Thread.Sleep(50 * (i + 1));
                }
                catch (FlaUI.Core.Exceptions.ElementNotAvailableException ex) when (i < attempts - 1)
                {
                    LogSwallowed("RetryTransient/ElementNotAvailable", ex);
                    Thread.Sleep(50 * (i + 1));
                }
                catch (TimeoutException ex) when (i < attempts - 1)
                {
                    LogSwallowed("RetryTransient/Timeout", ex);
                    Thread.Sleep(50 * (i + 1));
                }
            }
        }

        // Action overload (no return value) - thin shim around the generic helper.
        private static void RetryTransient(Action op, int attempts = 2) =>
            RetryTransient<bool>(() => { op(); return true; }, attempts);

        // -- Polling backoff schedule -----------------------------------------
        // Progressive backoff for /waitfor and /wait-window when the caller didn't pin
        // an explicit Interval. Schedule: 50, 50, 100, 100, 200, 200, 400, 400, 500 ...
        // Saves CPU on long waits (e.g. 30s polling at 50ms = 600 ticks vs ~80 with backoff)
        // while keeping the first few ticks fast for near-immediate transitions.
        // No jitter - this is single-process / single-client so thundering-herd doesn't apply.
        private static int PollBackoffMs(int tick)
        {
            if (tick < 0) tick = 0;
            int step = tick / 2;          // double every 2 ticks
            if (step > 4) step = 4;       // 50<<4 = 800, but cap below to 500
            int delay = 50 << step;       // 50, 100, 200, 400, 800
            return Math.Min(delay, 500);
        }

        /// <summary>
        /// Graceful-fallback recovery for a stale element. Two rungs:
        ///   Rung 1 - parent walk: climb the cached parent chain to the first live ancestor.
        ///   Rung 2 - descriptor re-find (when rung 1 surfaced a generic ancestor): scope a
        ///            FindFirst under that ancestor for an element matching the original
        ///            element's (controlType, name, automationId). Only accepted when the
        ///            descriptor matches uniquely; otherwise we fall back to the rung-1 result.
        ///
        /// Letting actions land on the original-equivalent element (e.g. a re-rendered START
        /// button) preserves their intended semantics; landing on a generic Group ancestor
        /// often does not (canvas keystroke handlers don't bubble through a Pane).
        ///
        /// Returns (null, null, hops) when no live ancestor is cached - caller should then
        /// emit the original stale-element error so the agent re-scans.
        /// </summary>
        private (AutomationElement? element, int? id, int hops) TryRecoverViaParent(int staleId)
        {
            // Snapshot the original descriptor before walking - even if it's evicted later we
            // can still try descriptor-based re-find with what we had.
            _elementDescriptors.TryGetValue(staleId, out var originalDescriptor);

            int hops = 0;
            int currentId = staleId;
            while (_elementParents.TryGetValue(currentId, out int parentId))
            {
                hops++;
                if (_elementMap.TryGetValue(parentId, out var parent) && IsElementValid(parent))
                {
                    // Rung 2: try descriptor re-find under this ancestor when the original
                    // descriptor has at least one disambiguating field. Skip the lookup for
                    // the trivial 1-hop-to-immediate-parent case where descriptor matching
                    // would just rediscover the same dead handle, and skip when the descriptor
                    // is too generic (no name, no automationId) - matching too many siblings
                    // is worse than a clean parent fallback.
                    bool worthDescriptorSearch = hops >= 1
                        && (!string.IsNullOrEmpty(originalDescriptor.Name)
                            || !string.IsNullOrEmpty(originalDescriptor.AutomationId));
                    if (worthDescriptorSearch)
                    {
                        var match = TryDescriptorRefind(parent, originalDescriptor);
                        if (match != null)
                        {
                            // Index the rediscovered element under the same id so subsequent
                            // recoveries find it directly via _elementMap. The id stays stable
                            // because the descriptor + parent are unchanged.
                            _elementMap[staleId]     = match;
                            _elementReverse[match]   = staleId;
                            return (match, staleId, 0); // hops=0 to signal "exact descriptor match"
                        }
                    }
                    return (parent, parentId, hops);
                }
                currentId = parentId;
                if (hops > 20) break; // sanity cap - typical UIA trees are <15 deep; guards against pathological cycles
            }
            return (null, null, hops);
        }

        /// <summary>
        /// Searches under <paramref name="root"/> for a single descendant matching the cached
        /// descriptor. Returns the match if exactly one is found; null if zero or ambiguous
        /// (two or more matches mean the agent originally selected one specific instance and
        /// we'd be guessing which to substitute).
        /// </summary>
        private AutomationElement? TryDescriptorRefind(AutomationElement root, ElementDescriptor desc)
        {
            try
            {
                AutomationElement[] matches;
                try
                {
                    if (!string.IsNullOrEmpty(desc.AutomationId))
                    {
                        matches = root.FindAllDescendants(cf => cf.ByAutomationId(desc.AutomationId));
                    }
                    else
                    {
                        // Name-based re-find. ControlType filter narrows the search to avoid
                        // false matches with same-named elements of different types (a Text
                        // label vs a Button labeled the same).
                        matches = root.FindAllDescendants(cf => cf.ByName(desc.Name));
                    }
                }
                catch (Exception ex)
                {
                    LogSwallowed("TryDescriptorRefind/FindAllDescendants", ex);
                    return null;
                }

                if (matches.Length == 0) return null;

                // Filter by control type when we have it - reduces ambiguity.
                AutomationElement[] filtered = matches;
                if (!string.IsNullOrEmpty(desc.ControlType))
                {
                    var typed = new List<AutomationElement>(matches.Length);
                    foreach (var m in matches)
                    {
                        try
                        {
                            if (string.Equals(m.Properties.ControlType.ValueOrDefault.ToString(),
                                              desc.ControlType, StringComparison.OrdinalIgnoreCase))
                                typed.Add(m);
                        }
                        catch (Exception ex) { LogSwallowed("TryDescriptorRefind/controlTypeFilter", ex); }
                    }
                    if (typed.Count > 0) filtered = typed.ToArray();
                }

                // Accept only on a unique match - two same-descriptor elements means the agent
                // originally picked one and we don't know which.
                return filtered.Length == 1 ? filtered[0] : null;
            }
            catch (Exception ex)
            {
                LogSwallowed("TryDescriptorRefind/outer", ex);
                return null;
            }
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

