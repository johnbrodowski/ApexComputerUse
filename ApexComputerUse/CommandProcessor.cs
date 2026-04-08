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
        public string? Action       { get; set; }
        public string? Value        { get; set; }
        public string? ModelPath    { get; set; }   // ai init — LLM model .gguf path
        public string? MmProjPath   { get; set; }   // ai init — mmproj .gguf path
        public string? Prompt       { get; set; }   // ai describe/ask — question text
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
        private readonly Dictionary<int, AutomationElement> _elementMap = new();
        private readonly Dictionary<int, Window>            _windowMap  = new();
        private IntPtr _mappedWindowHandle = IntPtr.Zero;

        public AutomationElement? CurrentElement { get; private set; }
        public Window?            CurrentWindow  { get; private set; }

        /// <summary>True when the AI/multimodal model has been loaded and is ready.</summary>
        public bool IsModelLoaded => _mtmd?.IsInitialized == true;

        /// <summary>True while the model is generating a response (inference in progress).</summary>
        public bool IsProcessing { get; private set; }

        private string _windowDesc  = "(none)";
        private string _elementDesc = "(none)";
        private readonly object _lock = new();

        /// Fired on every command for display in the form's log.
        public event Action<string>? OnLog;

        // ── Entry point ───────────────────────────────────────────────────

        public CommandResponse Process(CommandRequest req)
        {
            lock (_lock)
            {
                try
                {
                    var response = req.Command.ToLowerInvariant() switch
                    {
                        "find"              => CmdFind(req),
                        "execute" or "exec" => CmdExecute(req),
                        "ocr"               => CmdOcr(req),
                        "ai"                => CmdAi(req),
                        "status"            => CmdStatus(),
                        "windows"           => CmdListWindows(),
                        "elements"          => CmdListElements(req),
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

            // No element search — target the window itself
            if (string.IsNullOrWhiteSpace(req.AutomationId) && string.IsNullOrWhiteSpace(req.ElementName))
            {
                CurrentElement = window;
                _elementDesc   = _helper.Describe(window);
                return Ok($"Window: {wTitle}{wNote}", _elementDesc);
            }

            ControlType? filter = ResolveControlType(req.SearchType);
            bool   byId      = !string.IsNullOrWhiteSpace(req.AutomationId);
            string searchVal = byId ? req.AutomationId! : req.ElementName!;

            // Map lookup: if the search value is a numeric mapped ID, resolve directly
            if (byId && int.TryParse(searchVal, out int mappedId) && _elementMap.TryGetValue(mappedId, out var mappedEl))
            {
                try
                {
                    CurrentElement = mappedEl;
                    _elementDesc   = _helper.Describe(mappedEl);
                    return Ok($"Window: {wTitle}{wNote} | Element [map:{mappedId}]", _elementDesc);
                }
                catch { } // stale — fall through to fuzzy find
            }

            var el = _helper.FindElementFuzzy(window, searchVal, filter, byId,
                         out string eValue, out bool eExact);

            if (el == null) return Fail($"No element found for '{searchVal}'.");

            CurrentElement = el;
            _elementDesc   = _helper.Describe(el);
            string eNote   = eExact ? "" : $" (fuzzy '{searchVal}' → '{eValue}')";

            return Ok($"Window: {wTitle}{wNote} | Element{eNote}", _elementDesc);
        }

        private CommandResponse CmdExecute(CommandRequest req)
        {
            if (CurrentElement == null) return Fail("No element selected. Use 'find' first.");
            if (string.IsNullOrWhiteSpace(req.Action))  return Fail("'action' is required.");

            string result = RunAction(CurrentElement, CurrentWindow, req.Action!, req.Value ?? "");
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
                    var region = new System.Drawing.Rectangle(
                        int.Parse(parts[0].Trim()), int.Parse(parts[1].Trim()),
                        int.Parse(parts[2].Trim()), int.Parse(parts[3].Trim()));
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
            if (hwnd != _mappedWindowHandle)
            {
                _elementMap.Clear();
                _idGen.Reset();
                _mappedWindowHandle = hwnd;
            }
            else
            {
                _elementMap.Clear();
            }

            var root = ScanElementsIntoMap(CurrentWindow, null, null);

            string json = System.Text.Json.JsonSerializer.Serialize(root,
                new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented          = true,
                    PropertyNamingPolicy   = System.Text.Json.JsonNamingPolicy.CamelCase,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                });

            return Ok($"{_elementMap.Count} element(s)", json);
        }

        // ── AI (Multimodal) commands ──────────────────────────────────────

        private CommandResponse CmdAi(CommandRequest req)
        {
            return (req.Action?.ToLowerInvariant() ?? "status") switch
            {
                "init"     => CmdAiInit(req),
                "status"   => CmdAiStatus(),
                "describe" => CmdAiDescribe(req),
                "file"     => CmdAiFile(req),
                "ask"      => CmdAiAsk(req),
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
            return InitModelAsync(req.ModelPath!, req.MmProjPath!).GetAwaiter().GetResult();
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

        private CommandResponse CmdAiDescribe(CommandRequest req)
        {
            if (_mtmd == null || !_mtmd.IsInitialized) return Fail("AI not initialized. Use ai action=init first.");
            if (CurrentElement == null)                 return Fail("No element selected. Use 'find' first.");
            string prompt = req.Prompt ?? req.Value ?? "Describe what you see in this UI element.";
            IsProcessing = true;
            try   { string result = _mtmd.DescribeElementAsync(CurrentElement, prompt).GetAwaiter().GetResult();
                    return Ok("AI description", result); }
            finally { IsProcessing = false; }
        }

        private CommandResponse CmdAiFile(CommandRequest req)
        {
            if (_mtmd == null || !_mtmd.IsInitialized) return Fail("AI not initialized. Use ai action=init first.");
            string? path = req.Value;
            if (string.IsNullOrWhiteSpace(path)) return Fail("value=<file path> required.");
            string prompt = req.Prompt ?? "Describe this media.";
            IsProcessing = true;
            try   { string result = _mtmd.DescribeImageAsync(path!, prompt).GetAwaiter().GetResult();
                    return Ok($"AI file description ({Path.GetFileName(path)})", result); }
            finally { IsProcessing = false; }
        }

        private CommandResponse CmdAiAsk(CommandRequest req)
        {
            if (_mtmd == null || !_mtmd.IsInitialized) return Fail("AI not initialized. Use ai action=init first.");
            if (CurrentElement == null)                 return Fail("No element selected. Use 'find' first.");
            string prompt = req.Prompt ?? req.Value ?? "";
            if (string.IsNullOrWhiteSpace(prompt)) return Fail("prompt= required.");
            IsProcessing = true;
            try   { string result = _mtmd.DescribeElementAsync(CurrentElement, prompt).GetAwaiter().GetResult();
                    return Ok("AI answer", result); }
            finally { IsProcessing = false; }
        }

        private static CommandResponse CmdHelp() => Ok("Commands", """
            find     window=<title|id> [id=<automationId|mapId>] [name=<name>] [type=<ControlType>]
            exec     action=<action> [value=<input>]
            ocr      [value=x,y,w,h]
            capture  [action=screen|window|element|elements] [value=id1,id2,...]
            ai       action=<sub> ...
              init     model=<path> proj=<path>
              status
              describe [prompt=<text>]
              file     value=<path> [prompt=<text>]
              ask      prompt=<text>
            status
            windows
            elements [type=<ControlType>]
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

        private ElementNode? ScanElementsIntoMap(AutomationElement el, string? parentHash, int? parentId, int siblingIndex = 0, int depth = 0)
        {
            if (depth > ScanMaxDepth) return null;
            try
            {
                var ct = el.Properties.ControlType.ValueOrDefault;
                bool isWindowOrPane = ct == ControlType.Window || ct == ControlType.Pane;
                string hash = _idGen.GenerateElementHash(el, parentId, parentHash,
                                  excludeName: isWindowOrPane, siblingIndex: siblingIndex);
                int id = _idGen.GenerateIdFromHash(hash);
                _elementMap[id] = el;

                // Fetch children on a background thread with a timeout to avoid
                // hanging on UWP-hosted elements that block UIA traversal indefinitely.
                AutomationElement[]? children = null;
                try
                {
                    var fetchTask = Task.Run(() => el.FindAllChildren());
                    children = fetchTask.Wait(ScanChildTimeout) ? fetchTask.Result : null;
                }
                catch { children = null; }

                List<ElementNode>? childNodes = null;
                if (children != null)
                {
                    for (int i = 0; i < children.Length; i++)
                    {
                        var child = ScanElementsIntoMap(children[i], hash, id, i, depth + 1);
                        if (child != null)
                        {
                            childNodes ??= new List<ElementNode>();
                            childNodes.Add(child);
                        }
                    }
                }

                return new ElementNode
                {
                    Id           = id,
                    ControlType  = ct.ToString(),
                    Name         = el.Properties.Name.ValueOrDefault ?? "",
                    AutomationId = el.Properties.AutomationId.ValueOrDefault ?? "",
                    Children     = childNodes
                };
            }
            catch { return null; } // element became stale mid-scan — skip silently
        }

        private sealed class ElementNode
        {
            public int                 Id           { get; init; }
            public string              ControlType  { get; init; } = "";
            public string              Name         { get; init; } = "";
            public string              AutomationId { get; init; } = "";
            public List<ElementNode>?  Children     { get; init; }
        }

        // ── Helpers ───────────────────────────────────────────────────────

        private static ControlType? ResolveControlType(string? name)
        {
            if (string.IsNullOrWhiteSpace(name) || name == "All") return null;
            return Enum.TryParse<ControlType>(name, true, out var ct) ? ct : null;
        }

        private static CommandResponse Ok(string msg, string? data = null) =>
            new() { Success = true,  Message = msg, Data = data };

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
