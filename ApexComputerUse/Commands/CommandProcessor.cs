using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;

namespace ApexComputerUse
{
    public class CommandRequest
    {
        public string  Command      { get; set; } = "";
        public string? Window       { get; set; }
        public string? AutomationId { get; set; }
        public string? ElementName  { get; set; }
        public string? SearchType   { get; set; }   // "All" or a ControlType name
        public bool    OnscreenOnly { get; set; }   // true = exclude IsOffscreen elements
        public string? Action       { get; set; }
        public string? Value        { get; set; }
        public string? ModelPath    { get; set; }   // ai init — LLM model .gguf path
        public string? MmProjPath   { get; set; }   // ai init — mmproj .gguf path
        public string? Prompt       { get; set; }   // ai describe/ask — question text
        public int?    Depth        { get; set; }   // elements — max tree depth (null = unlimited)

        // ── Browser-friendly tree shaping (opt-in; all default to inert) ──
        /// <summary>elements — case-insensitive substring search applied to Name/AutomationId/Value.</summary>
        public string? Match          { get; set; }
        /// <summary>elements — collapse chains of identity-less single-child wrapper nodes.</summary>
        public bool    CollapseChains { get; set; }
        /// <summary>elements/find — include an ancestor breadcrumb `path` on every emitted node.</summary>
        public bool    IncludePath    { get; set; }
        /// <summary>elements/find — extra per-element properties. "extra" adds `value` (Value pattern) and `helpText`.</summary>
        public string? Properties     { get; set; }
        /// <summary>elements — if set, the scan still runs but a full payload is only serialized
        /// when the tree hash differs. A match returns a tiny <c>{"treeHash":"...","notModified":true}</c>
        /// response so polling clients avoid round-tripping a megabyte of unchanged JSON.</summary>
        public string? ChangedSince   { get; set; }
    }

    public class CommandResponse
    {
        public bool    Success { get; set; }
        public string  Message { get; set; } = "";
        public string? Data    { get; set; }

        public string ToText() =>
            Data != null
                ? $"{(Success ? "OK" : "ERR")} {Message}\n{Data}"
                : $"{(Success ? "OK" : "ERR")} {Message}";

        public string ToJson()
        {
            var obj = new { success = Success, message = Message, data = Data };
            return System.Text.Json.JsonSerializer.Serialize(obj,
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        }
    }

    /// <summary>
    /// Shared command processor used by both the HTTP server and Telegram bot.
    /// Fuzzy matches are auto-accepted (no UI prompts).
    /// </summary>
    public class CommandProcessor : IDisposable
    {
        private readonly ApexHelper _helper = new();
        private OcrHelper?   _ocr;
        private MtmdHelper?  _mtmd;

        private readonly ElementIdGenerator _idGen = new() { UseIncrementalIds = false };
        private readonly Dictionary<int, AutomationElement> _elementMap    = new();
        private readonly Dictionary<int, string>            _elementHashes = new();   // parallel to _elementMap — stores each element's hash so a subtree can be re-scanned without re-walking from the window root
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
        /// Written from the inference worker, read from the UI status timer —
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

        // ── Entry point ───────────────────────────────────────────────────

        public CommandResponse Process(CommandRequest req)
        {
            string cmd = req.Command.ToLowerInvariant();

            // AI inference (describe, ask, file, init) runs OUTSIDE _stateLock.
            // Inference can take 30+ seconds; holding the lock that long would block
            // every other command (find, execute, status, …).
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

        // ── Commands ──────────────────────────────────────────────────────

        private CommandResponse CmdFind(CommandRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Window))
                return Fail("'window' is required.  e.g. find window=Notepad");

            // If window search value is a numeric mapped ID, resolve directly from window map
            Window? window;
            string wTitle;
            bool wExact;
            if (int.TryParse(req.Window, out int windowMapId) && _windowMap.TryGetValue(windowMapId, out var mappedWindow))
            {
                window = mappedWindow;
                wTitle = window.Properties.Name.ValueOrDefault ?? req.Window;
                wExact = true;
            }
            else
            {
                window = _helper.FindWindowFuzzy(req.Window, out wTitle, out wExact);
            }
            if (window == null) return Fail($"No window found for '{req.Window}'.");

            CurrentWindow = window;
            _windowDesc   = wTitle;
            try { window.Focus(); } catch { /* window may not support focus */ }
            string wNote  = wExact ? "" : $" (fuzzy for '{req.Window}')";

            bool includeExtra = string.Equals(req.Properties, "extra", StringComparison.OrdinalIgnoreCase);

            // No element search — target the window itself, unless a type filter was
            // given, in which case find the first descendant of that ControlType.
            if (string.IsNullOrWhiteSpace(req.AutomationId) && string.IsNullOrWhiteSpace(req.ElementName))
            {
                ControlType? typeOnly = ResolveControlType(req.SearchType);
                if (typeOnly.HasValue)
                {
                    AutomationElement? byType = null;
                    try
                    {
                        var t = Task.Run(() => window.FindFirstDescendant(cf => cf.ByControlType(typeOnly.Value)));
                        byType = t.Wait(5000) ? t.Result : null;
                    }
                    catch { /* stale window — fall through to null check */ }

                    if (byType == null)
                        return Fail($"No element of type '{req.SearchType}' found in '{wTitle}'.");

                    CurrentElement = byType;
                    _elementDesc   = _helper.Describe(byType);
                    return Ok($"Window: {wTitle}{wNote} | Element (first {req.SearchType})",
                              BuildFindElementJson(byType, includeExtra));
                }

                CurrentElement = window;
                _elementDesc   = _helper.Describe(window);
                return Ok($"Window: {wTitle}{wNote}",
                          BuildFindElementJson(window, includeExtra));
            }

            ControlType? filter = ResolveControlType(req.SearchType);
            bool   byId      = !string.IsNullOrWhiteSpace(req.AutomationId);
            string searchVal = byId ? req.AutomationId! : req.ElementName!;

            // Map lookup: if the search value is a numeric mapped ID, resolve directly
            if (byId && int.TryParse(searchVal, out int mappedId) && _elementMap.TryGetValue(mappedId, out var mappedEl))
            {
                if (!IsElementValid(mappedEl))
                    return Fail($"Element [map:{mappedId}] is stale — the target app has changed state. Run 'find' again.");
                try
                {
                    CurrentElement = mappedEl;
                    _elementDesc   = _helper.Describe(mappedEl);
                    return Ok($"Window: {wTitle}{wNote} | Element [map:{mappedId}]",
                              BuildFindElementJson(mappedEl, includeExtra, mappedId));
                }
                catch { return Fail($"Element [map:{mappedId}] became stale during access. Run 'find' again."); }
            }

            var el = _helper.FindElementFuzzy(window, searchVal, filter, byId,
                         out string eValue, out bool eExact);

            if (el == null) return Fail($"No element found for '{searchVal}'.");

            CurrentElement = el;
            _elementDesc   = _helper.Describe(el);
            string eNote   = eExact ? "" : $" (fuzzy '{searchVal}' → '{eValue}')";

            return Ok($"Window: {wTitle}{wNote} | Element{eNote}",
                      BuildFindElementJson(el, includeExtra));
        }

        /// <summary>
        /// Builds the structured JSON document returned in <c>data.result</c> for <c>/find</c>.
        /// Replaces the legacy unstructured <see cref="ApexHelper.Describe"/> string so callers
        /// can programmatically consume <c>id</c>, <c>controlType</c>, <c>name</c>, etc. without
        /// regex-parsing a description. The Describe text remains available in <c>message</c>.
        /// </summary>
        /// <param name="preferredId">
        /// When the element was resolved via a numeric map-ID lookup, pass that ID so the
        /// response mirrors the caller's own reference. Otherwise we try to recover the ID
        /// from <c>_elementMap</c> via reference equality (present only when /elements was
        /// called previously on the same window); we omit <c>id</c> if we have no match.
        /// </param>
        private string BuildFindElementJson(AutomationElement el, bool includeExtra, int? preferredId = null)
        {
            // Recover the stable numeric ID if the caller previously ran /elements — reference
            // equality against the live AutomationElement handles the common "scan then find"
            // workflow. First-time /find without a prior scan simply omits `id`.
            int? id = preferredId;
            if (id == null)
            {
                foreach (var kv in _elementMap)
                {
                    if (kv.Value.Equals(el)) { id = kv.Key; break; }
                }
            }

            BoundingRect? rect = null;
            try
            {
                var b = el.BoundingRectangle;
                if (b.Width > 0 || b.Height > 0)
                    rect = new BoundingRect { X = (int)b.X, Y = (int)b.Y, Width = (int)b.Width, Height = (int)b.Height };
            }
            catch { /* stale — leave rect null */ }

            string ct       = "Unknown";
            string name     = "";
            string autoId   = "";
            string className = "";
            string frameworkId = "";
            bool   isEnabled   = false;
            bool   isOffscreen = false;
            try { ct          = el.Properties.ControlType.ValueOrDefault.ToString(); } catch { }
            try { name        = el.Properties.Name.ValueOrDefault         ?? ""; }     catch { }
            try { autoId      = el.Properties.AutomationId.ValueOrDefault ?? ""; }     catch { }
            try { className   = el.Properties.ClassName.ValueOrDefault    ?? ""; }     catch { }
            try { frameworkId = el.Properties.FrameworkId.ValueOrDefault  ?? ""; }     catch { }
            try { isEnabled   = el.Properties.IsEnabled.ValueOrDefault;          }     catch { }
            try { isOffscreen = el.Properties.IsOffscreen.ValueOrDefault;        }     catch { }

            var payload = new
            {
                id,
                controlType       = ct,
                name,
                automationId      = autoId,
                className,
                frameworkId,
                isEnabled,
                isOffscreen,
                boundingRectangle = rect,
                value             = includeExtra ? _helper.ReadValuePattern(el) : null,
                helpText          = includeExtra ? _helper.ReadHelpText(el)     : null
            };

            return System.Text.Json.JsonSerializer.Serialize(payload,
                new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented          = true,
                    PropertyNamingPolicy   = System.Text.Json.JsonNamingPolicy.CamelCase,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                });
        }

        private CommandResponse CmdExecute(CommandRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Action)) return Fail("'action' is required.");

            // If a numeric element ID was provided directly (e.g. element=123 or id=123),
            // look it up in the map so callers don't need a separate /find round-trip.
            AutomationElement? target = CurrentElement;
            if (!string.IsNullOrWhiteSpace(req.AutomationId) &&
                int.TryParse(req.AutomationId, out int elemId) &&
                _elementMap.TryGetValue(elemId, out var mappedEl))
            {
                target = mappedEl;
            }

            if (target == null) return Fail("No element selected. Use 'find' first or pass element=<id>.");
            if (!IsElementValid(target)) return Fail("The selected element is stale — the target app has changed state. Run 'find' again.");

            string result = RunAction(target, CurrentWindow, req.Action!, req.Value ?? "");
            return string.IsNullOrEmpty(result)
                ? Ok($"'{req.Action}' executed.")
                : Ok($"'{req.Action}' executed.", result);
        }

        private CommandResponse CmdOcr(CommandRequest req)
        {
            if (CurrentElement == null) return Fail("No element selected. Use 'find' first.");
            _ocr ??= new OcrHelper();

            if (!string.IsNullOrWhiteSpace(req.Value))
            {
                var parts = req.Value!.Split(',');
                if (parts.Length == 4)
                {
                    if (!int.TryParse(parts[0].Trim(), out int rx) ||
                        !int.TryParse(parts[1].Trim(), out int ry) ||
                        !int.TryParse(parts[2].Trim(), out int rw) ||
                        !int.TryParse(parts[3].Trim(), out int rh) ||
                        rw <= 0 || rh <= 0)
                        return Fail("OCR region must be four integers: x,y,width,height with width and height > 0.");
                    var region = new System.Drawing.Rectangle(rx, ry, rw, rh);
                    var r = _ocr.OcrElementRegion(CurrentElement, region);
                    return Ok($"OCR region (confidence {r.Confidence:P1})", r.Text);
                }
            }

            var result = _ocr.OcrElement(CurrentElement);
            return Ok($"OCR (confidence {result.Confidence:P1})", result.Text);
        }

        private CommandResponse CmdCapture(CommandRequest req)
        {
            var target = (req.Action ?? "element").ToLowerInvariant();

            switch (target)
            {
                case "screen":
                    return Ok("Captured screen", _helper.CaptureScreenToBase64());

                case "window":
                    if (CurrentWindow == null) return Fail("No window selected. Use 'find' first.");
                    return Ok($"Captured window: {CurrentWindow.Title}",
                              _helper.CaptureElementToBase64(CurrentWindow));

                case "elements":
                    if (string.IsNullOrWhiteSpace(req.Value))
                        return Fail("Provide element IDs in value= (comma-separated numeric IDs from /elements).");
                    var elems = new List<AutomationElement>();
                    foreach (var part in req.Value!.Split(',', StringSplitOptions.RemoveEmptyEntries))
                        if (int.TryParse(part.Trim(), out int id) && _elementMap.TryGetValue(id, out var el))
                            elems.Add(el);
                    if (elems.Count == 0) return Fail("No valid element IDs found in map. Run 'elements' first.");
                    return Ok($"Captured {elems.Count} element(s)", _helper.StitchElementsToBase64(elems));

                default: // "element"
                    if (CurrentElement == null) return Fail("No element selected. Use 'find' first.");
                    return Ok("Captured element", _helper.CaptureElementToBase64(CurrentElement));
            }
        }

        private CommandResponse CmdDraw(CommandRequest req)
        {
            string json = !string.IsNullOrWhiteSpace(req.Value)  ? req.Value!
                        : !string.IsNullOrWhiteSpace(req.Prompt) ? req.Prompt!
                        : "";

            if (string.IsNullOrWhiteSpace(json))
                return Fail("'draw' requires a JSON DrawRequest in the value field. " +
                            "Example: {\"canvas\":\"blank\",\"width\":800,\"height\":600," +
                            "\"shapes\":[{\"type\":\"circle\",\"x\":400,\"y\":300,\"r\":80,\"color\":\"royalblue\",\"fill\":true}]}");

            try
            {
                var drawReq = AIDrawingCommand.ParseRequest(json);

                // Resolve canvas sources that need the UI automation helper
                if (string.Equals(drawReq.Canvas, "window", StringComparison.OrdinalIgnoreCase))
                {
                    if (CurrentWindow == null) return Fail("No window selected. Use 'find' first.");
                    drawReq.Canvas = _helper.CaptureElementToBase64(CurrentWindow);
                }
                else if (string.Equals(drawReq.Canvas, "element", StringComparison.OrdinalIgnoreCase))
                {
                    if (CurrentElement == null) return Fail("No element selected. Use 'find' first.");
                    drawReq.Canvas = _helper.CaptureElementToBase64(CurrentElement);
                }

                string base64 = AIDrawingCommand.Render(drawReq);

                if (drawReq.Overlay)
                {
                    // ShowOverlay must run on the UI thread
                    System.Windows.Forms.Application.OpenForms[0]?.BeginInvoke(
                        () => AIDrawingCommand.ShowOverlay(drawReq));
                }

                int ms = drawReq.OverlayMs;
                string overlayNote = drawReq.Overlay
                    ? (ms > 0 ? $" Overlay showing for {ms / 1000.0:0.#}s (Esc to dismiss)."
                               : " Overlay showing — press Esc to dismiss.")
                    : "";
                return Ok($"Drawing rendered ({drawReq.Shapes.Count} shape(s)).{overlayNote}", base64);
            }
            catch (Exception ex)
            {
                return Fail($"Draw error: {ex.Message}");
            }
        }

        private CommandResponse CmdRenderMap()
        {
            if (CurrentWindow == null) return Fail("No window selected. Use 'find window=X' first.");

            var elemResponse = CmdListElements(new CommandRequest { Command = "elements" });
            if (!elemResponse.Success || string.IsNullOrWhiteSpace(elemResponse.Data))
                return Fail("Could not scan elements: " + elemResponse.Message);

            string json = elemResponse.Data!;

            var renderer = new UiMapRenderer(new[]
            {
                "Button", "Document", "Text", "Window", "Pane", "MenuItem", "TitleBar",
                "CheckBox", "ComboBox", "DataGrid", "Edit", "Group", "Hyperlink", "List",
                "ListItem", "Menu", "MenuBar", "Slider", "Spinner", "StatusBar", "ScrollBar",
                "Tab", "ToolTip", "ToolBar", "TabItem", "Image", "AppBar", "Calendar",
                "Custom", "DataItem", "Header", "HeaderItem", "ProgressBar", "RadioButton",
                "SemanticZoom", "Separator", "SplitButton", "Table", "Thumb", "Tree",
                "TreeItem", "Unknown"
            });

            var screen = System.Windows.Forms.Screen.PrimaryScreen?.Bounds
                         ?? new System.Drawing.Rectangle(0, 0, 1920, 1080);

            string tmp = Path.GetTempFileName();
            try
            {
                renderer.Render(json, tmp, screen.Width, screen.Height);
                string b64 = Convert.ToBase64String(File.ReadAllBytes(tmp));
                return Ok($"UI map: {_elementMap.Count} element(s)", b64);
            }
            finally
            {
                try { File.Delete(tmp); } catch { /* best effort */ }
            }
        }

        private CommandResponse CmdStatus() =>
            Ok("Current state",
               $"Window : {_windowDesc}\nElement: {_elementDesc}");

        private CommandResponse CmdListWindows()
        {
            _windowMap.Clear();
            var windows = _helper.GetDesktopWindows();
            var entries = windows.Select(w =>
            {
                string hash = _idGen.GenerateElementHash(w, null, null, excludeName: true);
                int id = _idGen.GenerateIdFromHash(hash);
                _windowMap[id] = w.AsWindow();
                return new { id, title = w.Properties.Name.ValueOrDefault ?? "" };
            }).ToList();

            string json = System.Text.Json.JsonSerializer.Serialize(entries,
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true });

            return Ok($"{entries.Count} open window(s)", json);
        }

        private CommandResponse CmdListElements(CommandRequest req)
        {
            if (CurrentWindow == null) return Fail("No window selected. Use 'find window=X' first.");

            var hwnd = CurrentWindow.Properties.NativeWindowHandle.ValueOrDefault;

            // Optional: start the scan at a previously-mapped element instead of the window root.
            // Lets callers progressively drill into a subtree without re-scanning the whole window.
            // When provided, the map is preserved (IDs remain stable across calls).
            int? startId = null;
            if (!string.IsNullOrWhiteSpace(req.AutomationId)
                && int.TryParse(req.AutomationId, out int parsedId))
            {
                startId = parsedId;
            }

            AutomationElement scanRoot;
            string? rootHashOverride = null;
            int?    rootIdOverride   = null;

            if (startId.HasValue)
            {
                if (hwnd != _mappedWindowHandle)
                    return Fail($"Element ID {startId} is stale (current window differs from the one that was scanned). Run /elements first.");
                if (!_elementMap.TryGetValue(startId.Value, out var startEl))
                    return Fail($"Element ID {startId} not in map. Run /elements first.");
                if (!_elementHashes.TryGetValue(startId.Value, out var startHash))
                    return Fail($"Element hash for ID {startId} missing. Run /elements first.");

                scanRoot         = startEl;
                rootHashOverride = startHash;
                rootIdOverride   = startId.Value;
                // Do NOT clear the map — we want to preserve existing IDs so callers can keep referencing them.
            }
            else
            {
                // Full-tree scan from the window root — clear the map and start fresh.
                if (hwnd != _mappedWindowHandle)
                {
                    _elementMap.Clear();
                    _elementHashes.Clear();
                    _idGen.Reset();
                    _mappedWindowHandle = hwnd;
                }
                else
                {
                    _elementMap.Clear();
                    _elementHashes.Clear();
                }
                scanRoot = CurrentWindow;
            }

            int? maxDepth = (req.Depth.HasValue && req.Depth.Value >= 0) ? req.Depth : null;

            var options = new ScanOptions
            {
                OnscreenOnly = req.OnscreenOnly,
                MaxDepth     = maxDepth,
                IncludePath  = req.IncludePath,
                IncludeExtra = string.Equals(req.Properties, "extra", StringComparison.OrdinalIgnoreCase)
            };

            var root = ScanElementsIntoMap(
                scanRoot, null, null,
                options,
                overrideHash: rootHashOverride,
                overrideId:   rootIdOverride);

            // Apply ControlType filter: prune tree to matching nodes (plus structural ancestors).
            ControlType? typeFilter = ResolveControlType(req.SearchType);
            if (typeFilter.HasValue && root != null)
                root = FilterTreeByType(root, typeFilter.Value, isRoot: true);

            // Text-search filter: prune to branches containing matches on Name/AutomationId/Value.
            if (!string.IsNullOrWhiteSpace(req.Match) && root != null)
                root = FilterTreeByMatch(root, req.Match!.Trim(), isRoot: true);

            // Single-child wrapper collapse — run last so IDs, paths, and descendant counts are
            // already set before we start hoisting children up through the tree.
            if (req.CollapseChains && root != null)
                root = CollapseSingleChildChains(root);

            int count = CountNodes(root);
            string treeHash = ComputeTreeHash(root);

            // Short-circuit for pollers that passed their last-seen hash: skip the expensive
            // JSON serialization entirely when the tree hasn't changed structurally.
            if (!string.IsNullOrEmpty(req.ChangedSince) &&
                string.Equals(req.ChangedSince, treeHash, StringComparison.Ordinal))
            {
                string shortJson = System.Text.Json.JsonSerializer.Serialize(
                    new { treeHash, notModified = true },
                    new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                return Ok($"{count} element(s) (unchanged)", shortJson);
            }

            string json = System.Text.Json.JsonSerializer.Serialize(
                new { treeHash, root },
                new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented          = true,
                    PropertyNamingPolicy   = System.Text.Json.JsonNamingPolicy.CamelCase,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                });

            return Ok($"{count} element(s)", json);
        }

        /// <summary>
        /// Deterministic structural hash of the emitted tree. Only identity fields
        /// (id, controlType, automationId, name, rectangle, descendant counts) participate —
        /// the caller can safely pass this back as <see cref="CommandRequest.ChangedSince"/> to
        /// short-circuit unchanged polls. Uses SHA-256 so collisions aren't a concern for
        /// polling-level change detection.
        /// </summary>
        private static string ComputeTreeHash(ElementNode? root)
        {
            using var sha = System.Security.Cryptography.SHA256.Create();
            using var ms  = new System.IO.MemoryStream();
            using (var writer = new System.IO.BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true))
                WriteNodeForHash(writer, root);
            ms.Position = 0;
            var bytes = sha.ComputeHash(ms);
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }

        private static void WriteNodeForHash(System.IO.BinaryWriter w, ElementNode? node)
        {
            if (node == null) { w.Write((byte)0); return; }
            w.Write((byte)1);
            w.Write(node.Id);
            w.Write(node.ControlType ?? "");
            w.Write(node.AutomationId ?? "");
            w.Write(node.Name ?? "");
            if (node.BoundingRectangle is { } r)
            {
                w.Write((byte)1);
                w.Write(r.X); w.Write(r.Y); w.Write(r.Width); w.Write(r.Height);
            }
            else w.Write((byte)0);
            w.Write(node.ChildCount ?? -1);
            w.Write(node.DescendantCount ?? -1);
            int childCount = node.Children?.Count ?? 0;
            w.Write(childCount);
            if (node.Children != null)
                foreach (var c in node.Children) WriteNodeForHash(w, c);
        }

        // ── AI (Multimodal) commands ──────────────────────────────────────

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
                OnLog?.Invoke($"[AI Init] OK — Vision={_mtmd.SupportsVision} Audio={_mtmd.SupportsAudio}");
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
                string result = Task.Run(async () => await _mtmd.DescribeElementAsync(element, prompt))
                                    .GetAwaiter().GetResult();
                return Ok("AI answer", result);
            }
            finally { _isProcessing = false; }
        }

        private CommandResponse CmdScene(CommandRequest req)
        {
            if (SceneStore == null) return Fail("SceneStore not initialised.");

            var opts = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            string action = (req.Action ?? "").ToLowerInvariant();

            try
            {
                switch (action)
                {
                    case "list":
                    {
                        var scenes = SceneStore.ListScenes()
                            .Select(s => new { s.Id, s.Name, s.Width, s.Height,
                                               shapes = s.Layers.Sum(l => l.Shapes.Count) });
                        return Ok("Scenes", System.Text.Json.JsonSerializer.Serialize(scenes, opts));
                    }

                    case "create":
                    {
                        var p = System.Text.Json.JsonSerializer
                                    .Deserialize<SceneCreateParams>(req.Value ?? "{}", opts)
                                ?? new SceneCreateParams();
                        var scene = SceneStore.CreateScene(p.Name ?? "Untitled",
                                        p.Width ?? 800, p.Height ?? 600, p.Background ?? "white");
                        return Ok($"Scene created: {scene.Id}",
                                  System.Text.Json.JsonSerializer.Serialize(scene, opts));
                    }

                    case "get":
                    {
                        var scene = SceneStore.GetScene(req.Value?.Trim() ?? "")
                                    ?? throw new KeyNotFoundException($"Scene '{req.Value}' not found.");
                        return Ok(scene.Name, System.Text.Json.JsonSerializer.Serialize(scene, opts));
                    }

                    case "render":
                    {
                        string b64 = SceneStore.RenderScene(req.Value?.Trim() ?? "");
                        return Ok("Scene rendered", b64);
                    }

                    case "add-layer":
                    {
                        var p = System.Text.Json.JsonSerializer
                                    .Deserialize<LayerParams>(req.Value ?? "{}", opts)
                                ?? new LayerParams();
                        var layer = SceneStore.AddLayer(p.SceneId ?? "", p.Name ?? "Layer");
                        return Ok($"Layer added: {layer.Id}",
                                  System.Text.Json.JsonSerializer.Serialize(layer, opts));
                    }

                    case "add-shape":
                    {
                        var p = System.Text.Json.JsonSerializer
                                    .Deserialize<ShapeParams>(req.Value ?? "{}", opts)
                                ?? new ShapeParams();
                        var ss = SceneStore.AddShape(p.SceneId ?? "", p.LayerId ?? "",
                                     p.Shape ?? new AIDrawingCommand.ShapeCommand(), p.Name);
                        return Ok($"Shape added: {ss.Id}",
                                  System.Text.Json.JsonSerializer.Serialize(ss, opts));
                    }

                    case "update-shape":
                    {
                        var p = System.Text.Json.JsonSerializer
                                    .Deserialize<ShapeParams>(req.Value ?? "{}", opts)
                                ?? new ShapeParams();
                        var ss = SceneStore.UpdateShape(p.SceneId ?? "", p.LayerId ?? "",
                                     p.ShapeId ?? "", p.Shape ?? new AIDrawingCommand.ShapeCommand(),
                                     p.Name);
                        return Ok($"Shape updated: {ss.Id}",
                                  System.Text.Json.JsonSerializer.Serialize(ss, opts));
                    }

                    case "delete-shape":
                    {
                        var p = System.Text.Json.JsonSerializer
                                    .Deserialize<ShapeParams>(req.Value ?? "{}", opts)
                                ?? new ShapeParams();
                        SceneStore.DeleteShape(p.SceneId ?? "", p.LayerId ?? "", p.ShapeId ?? "");
                        return Ok($"Shape deleted: {p.ShapeId}");
                    }

                    default:
                        return Fail($"Unknown scene action '{action}'. " +
                                    "Try: list, create, get, render, add-layer, add-shape, update-shape, delete-shape");
                }
            }
            catch (KeyNotFoundException ex) { return Fail(ex.Message); }
            catch (Exception ex)            { return Fail($"Scene error: {ex.Message}"); }
        }

        // ── Scene command parameter POCOs ─────────────────────────────────

        private class SceneCreateParams
        {
            [System.Text.Json.Serialization.JsonPropertyName("name")]       public string? Name       { get; set; }
            [System.Text.Json.Serialization.JsonPropertyName("width")]      public int?    Width      { get; set; }
            [System.Text.Json.Serialization.JsonPropertyName("height")]     public int?    Height     { get; set; }
            [System.Text.Json.Serialization.JsonPropertyName("background")] public string? Background { get; set; }
        }

        private class LayerParams
        {
            [System.Text.Json.Serialization.JsonPropertyName("scene_id")]   public string? SceneId { get; set; }
            [System.Text.Json.Serialization.JsonPropertyName("name")]       public string? Name    { get; set; }
        }

        private class ShapeParams
        {
            [System.Text.Json.Serialization.JsonPropertyName("scene_id")]   public string? SceneId  { get; set; }
            [System.Text.Json.Serialization.JsonPropertyName("layer_id")]   public string? LayerId  { get; set; }
            [System.Text.Json.Serialization.JsonPropertyName("shape_id")]   public string? ShapeId  { get; set; }
            [System.Text.Json.Serialization.JsonPropertyName("name")]       public string? Name     { get; set; }
            [System.Text.Json.Serialization.JsonPropertyName("shape")]      public AIDrawingCommand.ShapeCommand? Shape { get; set; }
        }

        private static CommandResponse CmdHelp() => Ok("Commands", """
            find     window=<title|id> [id=<automationId|mapId>] [name=<name>] [type=<ControlType>]
            exec     action=<action> [value=<input>]
            ocr      [value=x,y,w,h]
            capture  [action=screen|window|element|elements] [value=id1,id2,...]
            draw     value=<JSON DrawRequest>
              canvas      blank|white|black|screen|window|element|<base64-png>
              overlay     true = also show on screen as a transparent overlay
              overlay_ms  ms before overlay auto-closes (default 5000; 0 = Esc to dismiss)
              width    canvas width  (default 800, ignored when canvas is an image)
              height   canvas height (default 600, ignored when canvas is an image)
              shapes   array of shape objects:
                type         rect|ellipse|circle|line|arrow|text|polygon
                x,y          position (circle: centre)
                w,h          size (rect/ellipse)
                r            radius (circle)
                x2,y2        end point (line/arrow)
                points       [x1,y1,x2,y2,…] (polygon)
                color        name or #RRGGBB  (default "red")
                fill         true/false (default false)
                stroke_width pen width (default 2)
                opacity      0.0–1.0  (default 1)
                corner_radius rounded corners for rect (default 0)
                dashed       true/false dashed stroke (default false)
                text         label string (type=text)
                font_size    pixels (default 14)
                font_bold    true/false
                background   label background colour (type=text)
                align        left|center|right (type=text)
            ai       action=<sub> ...
              init     model=<path> proj=<path>
              status
              describe [prompt=<text>]
              file     value=<path> [prompt=<text>]
              ask      prompt=<text>
            status
            windows
            elements [type=<ControlType>]
            uimap
            help

            Actions (for exec):
              --- Click / Mouse ---
              click                    smart click (Invoke→Toggle→SelectionItem→mouse)
              mouse-click              force mouse left click
              right-click
              double-click
              middle-click
              click-at   value=x,y     click at offset from element top-left
              hover
              drag       value=x,y     drag element to screen coordinates
              highlight                draw orange highlight for 1 second

              --- Keyboard ---
              type / enter  value=<text>
              insert        value=<text>   insert at caret
              keys          value=<keys>   {CTRL}/{ALT}/{SHIFT}/{KEY}, Ctrl+A, Enter, Tab, ...
              selectall, copy, cut, paste, undo, clear

              --- Focus / State ---
              focus
              isenabled                returns true/false
              isvisible                returns true/false
              describe                 full element property dump
              patterns                 list supported UIA patterns
              bounds                   bounding rectangle

              --- Text / Value ---
              gettext                  smart: Text pattern→Value→Name
              getvalue                 smart: Value→Text→LegacyIAccessible→Name
              setvalue   value=<text>  smart: Value→RangeValue→keyboard
              clearvalue               set value to empty
              appendvalue value=<text> append to current value
              getselectedtext          selected text via Text pattern

              --- Range / Slider ---
              setrange  value=<num>    set RangeValue pattern value
              getrange                 get current range value
              rangeinfo                min/max/step/largechange

              --- Toggle ---
              toggle                   toggle current state
              toggle-on                set to On
              toggle-off               set to Off
              gettoggle                get current toggle state (On/Off/Indeterminate)

              --- ExpandCollapse ---
              expand
              collapse
              expandstate              get current expand/collapse state

              --- Selection (SelectionItem) ---
              select-item              select via SelectionItem pattern
              addselect                add to multi-selection
              removeselect             remove from selection
              isselected               check if selected
              getselection             get selected items from container

              --- ComboBox / ListBox ---
              select     value=<text>  select item by text (multi-strategy)
              select-index value=<n>   select by zero-based index
              getitems                 list all items
              getselecteditem          get currently selected item text

              --- Window ---
              minimize
              maximize
              restore
              windowstate              Normal / Maximized / Minimized

              --- Transform ---
              move       value=x,y     move element via Transform pattern
              resize     value=w,h     resize element via Transform pattern

              --- Scroll ---
              scroll-up   value=<n>    mouse wheel up (default 3)
              scroll-down value=<n>    mouse wheel down
              scroll-left value=<n>    mouse horizontal scroll left
              scroll-right value=<n>   mouse horizontal scroll right
              scrollinto               scroll element into view (ScrollItem pattern)
              scrollpercent value=h,v  scroll to h/v percent (0–100)
              getscrollinfo            scroll position and range

              --- Grid / Table ---
              griditem  value=row,col  get item description at grid row,col
              gridinfo                 row and column counts
              griditeminfo             row/col/span for a grid item

              --- Screenshot / OCR ---
              screenshot / capture

              --- Wait ---
              wait  value=<automationId>
            """);

        // ── Action runner (remote) ────────────────────────────────────────

        private string RunAction(AutomationElement el, Window? win, string action, string input)
        {
            return action.ToLowerInvariant() switch
            {
                // ── Click / Mouse ─────────────────────────────────────────
                "click"                              => Do(() => _helper.ClickElement(el)),
                "mouse-click" or "mouseclick"        => Do(() => _helper.MouseClickElement(el)),
                "invoke"                             => Do(() => _helper.InvokeButton(el)),
                "right-click" or "rightclick"        => Do(() => _helper.RightClickElement(el)),
                "double-click" or "doubleclick"      => Do(() => _helper.DoubleClickElement(el)),
                "middle-click" or "middleclick"      => Do(() => _helper.MiddleClickElement(el)),
                "click-at"    or "clickat"           => Do(() => { var p = ParsePair(input); _helper.ClickAtOffset(el, p.a, p.b); }),
                "hover"                              => Do(() => _helper.HoverElement(el)),
                "drag"                               => Do(() => { var p = ParsePair(input); _helper.DragAndDropToPoint(el, p.a, p.b); }),
                "highlight"                          => Do(() => _helper.HighlightElement(el)),

                // ── Focus / State ─────────────────────────────────────────
                "focus"                              => Do(() => _helper.SetFocus(el)),
                "isenabled"                          => _helper.IsElementEnabled(el),
                "isvisible"                          => _helper.IsElementVisible(el),
                "describe"                           => _helper.Describe(el),
                "patterns"                           => _helper.GetSupportedPatterns(el),
                "bounds"                             => _helper.GetBoundingRect(el),

                // ── Text / Value ──────────────────────────────────────────
                "gettext"  or "text"                 => _helper.GetText(el),
                "getvalue" or "value"                => _helper.GetValue(el),
                "getselectedtext"                    => _helper.GetSelectedText(el),
                "type"     or "enter"                => Do(() => _helper.EnterText(el, input)),
                "insert"                             => Do(() => _helper.InsertTextAtCaret(el, input)),
                "setvalue"                           => Do(() => _helper.SetValue(el, input)),
                "clearvalue"                         => Do(() => _helper.ClearValue(el)),
                "appendvalue"                        => Do(() => _helper.AppendValue(el, input)),
                "selectall"                          => Do(() => _helper.SelectAllText(el)),
                "copy"                               => Do(() => _helper.CopyText(el)),
                "cut"                                => Do(() => _helper.CutText(el)),
                "paste"                              => Do(() => _helper.PasteText(el)),
                "undo"                               => Do(() => _helper.UndoText(el)),
                "clear"                              => Do(() => _helper.ClearText(el)),

                // ── Keyboard ──────────────────────────────────────────────
                "keys"                               => Do(() => _helper.SendKeysEnhanced(el, input)),

                // ── Range / Slider ────────────────────────────────────────
                "setrange"                           => Do(() => _helper.SetRangeValue(el, ParseDoubleOr(input, 0))),
                "getrange"                           => _helper.GetRangeValue(el),
                "rangeinfo"                          => _helper.GetRangeInfo(el),

                // ── Toggle ────────────────────────────────────────────────
                "toggle"                             => Do(() => _helper.ToggleCheckBox(el)),
                "toggle-on"  or "toggleon"           => Do(() => _helper.SetToggleState(el, true)),
                "toggle-off" or "toggleoff"          => Do(() => _helper.SetToggleState(el, false)),
                "gettoggle"                          => _helper.GetToggleState(el),

                // ── ExpandCollapse ────────────────────────────────────────
                "expand"                             => Do(() => el.Patterns.ExpandCollapse.Pattern.Expand()),
                "collapse"                           => Do(() => el.Patterns.ExpandCollapse.Pattern.Collapse()),
                "expandstate"                        => _helper.GetExpandCollapseState(el),

                // ── Selection (SelectionItem) ─────────────────────────────
                "select-item" or "selectitem"        => Do(() => _helper.SelectItem(el)),
                "addselect"                          => Do(() => _helper.AddToSelection(el)),
                "removeselect"                       => Do(() => _helper.RemoveFromSelection(el)),
                "isselected"                         => _helper.IsSelected(el),
                "getselection"                       => _helper.GetSelectionInfo(el),

                // ── ComboBox / ListBox ────────────────────────────────────
                "select"                             => Do(() => _helper.SelectComboBoxItem(el, input)),
                "select-index" or "selectindex"      => Do(() => _helper.SelectByIndex(el, ParseIntOr(input, 0))),
                "getitems"                           => string.Join("\n", _helper.GetComboBoxItems(el)),
                "getselecteditem"                    => _helper.GetComboBoxSelected(el),

                // ── Window ────────────────────────────────────────────────
                "minimize"   => Do(() => { if (win != null) _helper.MinimizeWindow(win); }),
                "maximize"   => Do(() => { if (win != null) _helper.MaximizeWindow(win); }),
                "restore"    => Do(() => { if (win != null) _helper.RestoreWindow(win); }),
                "windowstate"                        => _helper.GetWindowState(el),

                // ── Transform ────────────────────────────────────────────
                "move"                               => Do(() => { var p = ParsePair(input); _helper.MoveElement(el, p.a, p.b); }),
                "resize"                             => Do(() => { var p = ParsePair(input); _helper.ResizeElement(el, p.a, p.b); }),

                // ── Scroll ────────────────────────────────────────────────
                "scroll-up"   or "scrollup"          => Do(() => _helper.ScrollUp(ParseIntOr(input, 3))),
                "scroll-down" or "scrolldown"        => Do(() => _helper.ScrollDown(ParseIntOr(input, 3))),
                "scroll-left" or "scrollleft"        => Do(() => _helper.HorizontalScroll(-ParseIntOr(input, 3))),
                "scroll-right" or "scrollright"      => Do(() => _helper.HorizontalScroll(ParseIntOr(input, 3))),
                "scrollinto"  or "scrollintoview"    => Do(() => _helper.ScrollIntoView(el)),
                "scrollpercent"                      => Do(() => { var p = ParsePairD(input); _helper.ScrollByPercent(el, p.a, p.b); }),
                "getscrollinfo"                      => _helper.GetScrollInfo(el),

                // ── Grid / Table ──────────────────────────────────────────
                "griditem"                           => _helper.GetGridItem(el, ParsePair(input).a, ParsePair(input).b),
                "gridinfo"                           => _helper.GetGridInfo(el),
                "griditeminfo"                       => _helper.GetGridItemInfo(el),

                // ── Screenshot ────────────────────────────────────────────
                "screenshot" or "capture"            => _helper.CaptureElement(el, CaptureFolder()),

                // ── Wait ──────────────────────────────────────────────────
                "wait"                               => WaitFor(input),

                _ => $"Unknown action '{action}'. Send 'help' for a list."
            };
        }

        private string WaitFor(string automationId)
        {
            if (CurrentWindow == null) return "No window selected.";
            var el = _helper.WaitForElement(CurrentWindow, automationId);
            if (el == null) return $"'{automationId}' did not appear within timeout.";
            CurrentElement = el;
            _elementDesc   = _helper.Describe(el);
            return _elementDesc;
        }

        private const int ScanMaxDepth    = 25;
        private const int ScanChildTimeout = 2000; // ms per FindAllChildren call

        /// <summary>
        /// Bundle of options threaded through <see cref="ScanElementsIntoMap"/>. Kept as a
        /// struct so the recursion signature stays readable as more opt-in features land.
        /// </summary>
        private readonly struct ScanOptions
        {
            public bool    OnscreenOnly    { get; init; }
            public int?    MaxDepth        { get; init; }
            public bool    IncludePath     { get; init; }
            public bool    IncludeExtra    { get; init; }  // properties=extra → value + helpText
        }

        private ElementNode? ScanElementsIntoMap(
            AutomationElement el, string? parentHash, int? parentId,
            ScanOptions options,
            int siblingIndex = 0, int depth = 0,
            string? parentPath = null,
            string? overrideHash = null, int? overrideId = null)
        {
            if (depth > ScanMaxDepth) return null;

            // Onscreen filter — skip element and its entire subtree if off-viewport.
            // depth == 0 is always the scan root (window or expansion target); never filter it out.
            if (options.OnscreenOnly && depth > 0 && el.Properties.IsOffscreen.ValueOrDefault)
                return null;

            try
            {
                var ct = el.Properties.ControlType.ValueOrDefault;
                bool isWindowOrPane = ct == ControlType.Window || ct == ControlType.Pane;

                // If the caller supplied an override (lazy-expansion of a previously-mapped node),
                // reuse its stored hash/id so descendants hash identically to the original scan.
                string hash;
                int    id;
                if (overrideHash != null && overrideId.HasValue)
                {
                    hash = overrideHash;
                    id   = overrideId.Value;
                }
                else
                {
                    hash = _idGen.GenerateElementHash(el, parentId, parentHash,
                              excludeName: isWindowOrPane, siblingIndex: siblingIndex);
                    id = _idGen.GenerateIdFromHash(hash);
                }
                if (_elementMap.Count >= 50_000)
                {
                    _elementMap.Clear();
                    _elementHashes.Clear();
                    OnLog?.Invoke("[Warn] Element map cleared (50k cap reached).");
                }
                _elementMap[id]    = el;
                _elementHashes[id] = hash;

                bool truncate = options.MaxDepth.HasValue && depth >= options.MaxDepth.Value;

                string nameProp = el.Properties.Name.ValueOrDefault ?? "";

                // Build the ancestor breadcrumb — each level is "ControlType" if Name is empty,
                // otherwise "Name". Root is just the root's label. Only materialised when the
                // caller asked for IncludePath, so we don't burn string allocations otherwise.
                string? path = null;
                if (options.IncludePath)
                {
                    string segment = string.IsNullOrEmpty(nameProp) ? ct.ToString() : nameProp;
                    path = string.IsNullOrEmpty(parentPath) ? segment : $"{parentPath} > {segment}";
                }

                // Fetch children on a background thread with a timeout to avoid
                // hanging on UWP-hosted elements that block UIA traversal indefinitely.
                // We fetch even at the truncation boundary so we can report childCount.
                AutomationElement[]? children = null;
                try
                {
                    var fetchTask = Task.Run(() => el.FindAllChildren());
                    children = fetchTask.Wait(ScanChildTimeout) ? fetchTask.Result : null;
                }
                catch (Exception ex) { AppLog.Debug($"[Scan] FindAllChildren failed — {ex.Message}"); children = null; }

                List<ElementNode>? childNodes        = null;
                int?               childCountOut     = null;
                int?               descendantCountOut = null;

                if (truncate)
                {
                    // Depth limit hit — omit children, report their counts so callers know to drill in.
                    if (children != null && children.Length > 0)
                    {
                        childCountOut      = children.Length;
                        // Count all descendants below the cutoff so the caller can decide whether
                        // drilling in is cheap or expensive. Respects the onscreen filter so the
                        // count matches what a follow-up /elements?id=<id>&onscreen=true would emit.
                        descendantCountOut = CountDescendantsBelow(el, options.OnscreenOnly);
                    }
                }
                else if (children != null)
                {
                    for (int i = 0; i < children.Length; i++)
                    {
                        var child = ScanElementsIntoMap(
                            children[i], hash, id, options,
                            siblingIndex: i, depth: depth + 1,
                            parentPath: path);
                        if (child != null)
                        {
                            childNodes ??= new List<ElementNode>();
                            childNodes.Add(child);
                        }
                    }
                }

                var bounds = el.BoundingRectangle;
                var boundingRect = (bounds.Width > 0 || bounds.Height > 0)
                    ? new BoundingRect
                    {
                        X      = (int)bounds.X,
                        Y      = (int)bounds.Y,
                        Width  = (int)bounds.Width,
                        Height = (int)bounds.Height
                    }
                    : null;

                // Opt-in properties — read only when the caller asked for them so default scans
                // keep their existing 4-property budget.
                string? valueOut    = options.IncludeExtra ? _helper.ReadValuePattern(el) : null;
                string? helpTextOut = options.IncludeExtra ? _helper.ReadHelpText(el)     : null;
                if (string.IsNullOrEmpty(valueOut))    valueOut    = null;
                if (string.IsNullOrEmpty(helpTextOut)) helpTextOut = null;

                return new ElementNode
                {
                    Id                = id,
                    ControlType       = ct.ToString(),
                    Name              = nameProp,
                    AutomationId      = el.Properties.AutomationId.ValueOrDefault ?? "",
                    BoundingRectangle = boundingRect,
                    Children          = childNodes,
                    ChildCount        = childCountOut,
                    DescendantCount   = descendantCountOut,
                    Path              = path,
                    Value             = valueOut,
                    HelpText          = helpTextOut
                };
            }
            catch { return null; } // element became stale mid-scan — skip silently
        }

        /// <summary>
        /// Cheap count of all live descendants under <paramref name="el"/>, respecting the
        /// onscreen filter so the number matches what the caller would see if they drilled in
        /// via <c>/elements?id=&lt;id&gt;</c>. No ID hashing, no node construction, no mapping —
        /// just walks children to produce an integer so the caller can estimate cost.
        /// </summary>
        private static int CountDescendantsBelow(AutomationElement el, bool onscreenOnly)
        {
            AutomationElement[]? children;
            try
            {
                var fetchTask = Task.Run(() => el.FindAllChildren());
                children = fetchTask.Wait(ScanChildTimeout) ? fetchTask.Result : null;
            }
            catch { return 0; }
            if (children == null || children.Length == 0) return 0;

            int total = 0;
            foreach (var c in children)
            {
                try
                {
                    if (onscreenOnly && c.Properties.IsOffscreen.ValueOrDefault) continue;
                    total += 1 + CountDescendantsBelow(c, onscreenOnly);
                }
                catch { /* stale child — skip */ }
            }
            return total;
        }

        // Internal (not private) so the test project (InternalsVisibleTo) can construct
        // synthetic trees to verify FilterTreeByMatch / CollapseSingleChildChains / descendant counts.
        internal sealed class ElementNode
        {
            public int                 Id                { get; init; }
            public string              ControlType       { get; init; } = "";
            public string              Name              { get; init; } = "";
            public string              AutomationId      { get; init; } = "";
            public BoundingRect?       BoundingRectangle { get; init; }
            public List<ElementNode>?  Children          { get; init; }
            public int?                ChildCount        { get; init; }  // set only when children are omitted due to a depth limit — tells the caller it can expand this node via /elements?id=<Id>

            // ── Opt-in fields (emitted only when populated; JsonIgnoreCondition.WhenWritingNull keeps payloads small) ──
            public int?                DescendantCount   { get; init; }  // set alongside ChildCount on truncated nodes — total transitive descendants the caller could still drill into
            public string?             Path              { get; init; }  // ancestor breadcrumb, e.g. "Chrome > Document > Form" — set only when the caller requested IncludePath
            public string?             Value             { get; init; }  // Value pattern content — set only when the caller requested properties=extra
            public string?             HelpText          { get; init; }  // HelpText property — set only when the caller requested properties=extra
        }

        internal sealed class BoundingRect
        {
            public int X      { get; init; }
            public int Y      { get; init; }
            public int Width  { get; init; }
            public int Height { get; init; }
        }

        // ── Helpers ───────────────────────────────────────────────────────

        /// <summary>
        /// Recursively prunes an element tree to nodes whose ControlType matches
        /// <paramref name="typeFilter"/>, retaining structural ancestors (Window/Pane/Group)
        /// that lead to matching descendants so that spatial context is preserved.
        /// </summary>
        private static ElementNode? FilterTreeByType(ElementNode node, ControlType typeFilter, bool isRoot)
        {
            List<ElementNode>? filteredChildren = null;
            if (node.Children != null)
            {
                foreach (var child in node.Children)
                {
                    var filtered = FilterTreeByType(child, typeFilter, isRoot: false);
                    if (filtered != null)
                    {
                        filteredChildren ??= new List<ElementNode>();
                        filteredChildren.Add(filtered);
                    }
                }
            }

            bool keep = isRoot
                || node.ControlType.Equals(typeFilter.ToString(), StringComparison.OrdinalIgnoreCase)
                || filteredChildren != null;

            if (!keep) return null;

            return CloneWithChildren(node, filteredChildren);
        }

        /// <summary>
        /// Prunes the tree to branches whose Name, AutomationId, or Value (when populated)
        /// contains <paramref name="needle"/> (case-insensitive). Matching nodes keep their
        /// full subtree; non-matching ancestors are preserved only when they lie on the path
        /// to a match, giving the caller a breadcrumb without siblings.
        /// </summary>
        internal static ElementNode? FilterTreeByMatch(ElementNode node, string needle, bool isRoot)
        {
            if (string.IsNullOrEmpty(needle)) return node;

            bool selfMatches = MatchesNode(node, needle);

            // If this node matches, keep its entire subtree — the agent probably wants
            // context below the match. Non-matching siblings beneath a match are fine;
            // they're the siblings of a match, not unrelated noise.
            if (selfMatches) return node;

            List<ElementNode>? keptChildren = null;
            if (node.Children != null)
            {
                foreach (var child in node.Children)
                {
                    var kept = FilterTreeByMatch(child, needle, isRoot: false);
                    if (kept != null)
                    {
                        keptChildren ??= new List<ElementNode>();
                        keptChildren.Add(kept);
                    }
                }
            }

            // Keep the root so callers always get a valid tree skeleton, even if zero matches.
            // Keep intermediate nodes only when a descendant matched (they serve as breadcrumbs).
            if (!isRoot && keptChildren == null) return null;

            return CloneWithChildren(node, keptChildren);
        }

        private static bool MatchesNode(ElementNode node, string needle)
        {
            if (!string.IsNullOrEmpty(node.Name)         && node.Name.Contains(needle,         StringComparison.OrdinalIgnoreCase)) return true;
            if (!string.IsNullOrEmpty(node.AutomationId) && node.AutomationId.Contains(needle, StringComparison.OrdinalIgnoreCase)) return true;
            if (!string.IsNullOrEmpty(node.Value)        && node.Value.Contains(needle,        StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        /// <summary>
        /// Folds chains of identity-less single-child wrappers — e.g. the "1-in-1-in-1" Pane/Group
        /// chains browsers emit around every piece of web content. A node is collapsed when it has
        /// exactly one child AND has no Name AND has no AutomationId AND its ControlType is one of
        /// the generic containers (Pane/Group/Custom). IDs are never rewritten — the hoisted child
        /// keeps its original ID so follow-up /elements?id=&lt;id&gt; calls still resolve.
        /// </summary>
        internal static ElementNode? CollapseSingleChildChains(ElementNode? node)
        {
            if (node == null) return null;

            // Recurse first so we collapse bottom-up.
            List<ElementNode>? newChildren = null;
            if (node.Children != null)
            {
                foreach (var child in node.Children)
                {
                    var reduced = CollapseSingleChildChains(child);
                    if (reduced != null)
                    {
                        newChildren ??= new List<ElementNode>();
                        newChildren.Add(reduced);
                    }
                }
            }

            // A node qualifies for collapse if it's an identity-less wrapper with exactly one
            // surviving child. The child (possibly already collapsed) is hoisted into our place.
            if (IsCollapsibleWrapper(node) && newChildren != null && newChildren.Count == 1)
                return newChildren[0];

            return CloneWithChildren(node, newChildren);
        }

        private static bool IsCollapsibleWrapper(ElementNode node)
        {
            if (!string.IsNullOrEmpty(node.Name))         return false;
            if (!string.IsNullOrEmpty(node.AutomationId)) return false;
            return node.ControlType.Equals("Pane",   StringComparison.OrdinalIgnoreCase)
                || node.ControlType.Equals("Group",  StringComparison.OrdinalIgnoreCase)
                || node.ControlType.Equals("Custom", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Clones a node, substituting a new children list but preserving every other field.</summary>
        private static ElementNode CloneWithChildren(ElementNode node, List<ElementNode>? newChildren) =>
            new()
            {
                Id                = node.Id,
                ControlType       = node.ControlType,
                Name              = node.Name,
                AutomationId      = node.AutomationId,
                BoundingRectangle = node.BoundingRectangle,
                Children          = newChildren,
                ChildCount        = node.ChildCount,
                DescendantCount   = node.DescendantCount,
                Path              = node.Path,
                Value             = node.Value,
                HelpText          = node.HelpText
            };

        private static int CountNodes(ElementNode? node)
        {
            if (node == null) return 0;
            int count = 1;
            if (node.Children != null)
                foreach (var child in node.Children)
                    count += CountNodes(child);
            return count;
        }

        private static ControlType? ResolveControlType(string? name)
        {
            if (string.IsNullOrWhiteSpace(name) || name == "All") return null;
            return Enum.TryParse<ControlType>(name, true, out var ct) ? ct : null;
        }

        private static CommandResponse Ok(string msg, string? data = null) =>
            new() { Success = true,  Message = msg, Data = data };

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
