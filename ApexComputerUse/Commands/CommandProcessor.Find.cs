using System.Diagnostics;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;

namespace ApexComputerUse
{
    public partial class CommandProcessor
    {
        // -- Commands ------------------------------------------------------

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

            // No element search - target the window itself, unless a type filter was
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
                    catch { /* stale window - fall through to null check */ }

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
                    return Fail($"Element [map:{mappedId}] is stale - the target app has changed state. Run 'find' again.");
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
            string eNote   = eExact ? "" : $" (fuzzy '{searchVal}' -> '{eValue}')";

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
            // Recover the stable numeric ID if the caller previously ran /elements - reference
            // equality against the live AutomationElement handles the common "scan then find"
            // workflow. First-time /find without a prior scan simply omits `id`.
            int? id = preferredId;
            if (id == null)
            {
                // Fast path - reverse index is O(1) average. Falls back to the linear scan
                // below if the dictionary misses (e.g. FlaUI returned a new instance with a
                // different hash code), which is still correct via Equals (UIA CompareElements).
                if (_elementReverse.TryGetValue(el, out int mapped)) id = mapped;
                else
                {
                    foreach (var kv in _elementMap)
                    {
                        if (kv.Value.Equals(el)) { id = kv.Key; break; }
                    }
                }
            }

            BoundingRect? rect = null;
            try
            {
                var b = el.BoundingRectangle;
                if (b.Width > 0 || b.Height > 0)
                    rect = new BoundingRect { X = (int)b.X, Y = (int)b.Y, Width = (int)b.Width, Height = (int)b.Height };
            }
            catch { /* stale - leave rect null */ }

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

            return System.Text.Json.JsonSerializer.Serialize(payload, FormatAdapter.s_indentedCamel);
        }

        private CommandResponse CmdExecute(CommandRequest req)
        {
            // Batch mode: actions[] runs each entry as a full sub-request, allowing mid-batch
            // find/capture/ocr (anything routable through Process) without special-casing.
            if (req.Actions != null && req.Actions.Count > 0)
                return RunBatch(req);

            if (string.IsNullOrWhiteSpace(req.Action)) return Fail("'action' is required.");

            // wait-window polls the desktop window list, not an element - dispatch before the
            // current-element guard so callers don't need a stale find first.
            if (string.Equals(req.Action, "wait-window", StringComparison.OrdinalIgnoreCase))
                return RunWaitWindow(req);

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
            if (!IsElementValid(target)) return Fail("The selected element is stale - the target app has changed state. Run 'find' again.");

            // Source-emitting text reads are handled inline so the response can carry the
            // UIA pattern (TextPattern / ValuePattern / LegacyIAccessible / Name) that
            // produced the result. Lets agents tell a real read from a degenerate Name fallback.
            string actionLower = req.Action!.ToLowerInvariant();
            if (actionLower is "gettext" or "text" or "getvalue" or "value" or "getselectedtext")
            {
                string text;
                string source;
                if (actionLower is "gettext" or "text")
                    text = _helper.GetText(target, out source);
                else if (actionLower is "getvalue" or "value")
                    text = _helper.GetValue(target, out source);
                else
                    text = _helper.GetSelectedText(target, out source);
                return OkWithExtras($"'{req.Action}' executed.", text,
                    new Dictionary<string, string> { ["source"] = source });
            }

            // Generic property-match waiter. Replaces hand-rolled sleep+poll loops on the
            // client side. Reads the request's property/predicate/expected/timeout/interval
            // fields - which RunAction's plain (action,input) signature can't see - so we
            // dispatch from CmdExecute where we still hold the full request.
            if (actionLower == "waitfor")
                return RunWaitFor(target, req);

            // Wrap the action call so we can attach structured error data when the failure is
            // a missing UIA pattern (the most common cause of action errors). Other exceptions
            // pass through with the bare message as before so we don't swallow useful context.
            string result;
            try
            {
                result = RunAction(target, CurrentWindow, req.Action!, req.Value ?? "");
            }
            catch (Exception ex)
            {
                var errorData = BuildErrorData(target, actionLower);
                return errorData != null
                    ? FailWithData(ex.Message, errorData)
                    : Fail(ex.Message);
            }
            return string.IsNullOrEmpty(result)
                ? Ok($"'{req.Action}' executed.")
                : Ok($"'{req.Action}' executed.", result);
        }

        // -- Structured error data ------------------------------------------------

        // Maps each pattern-using action to the UIA pattern it expects. Used when an action
        // fails so we can tell the caller "you tried Toggle, this element supports Invoke".
        // Only listed actions get structured error_data; others fall back to the bare message.
        private static readonly Dictionary<string, string> s_actionPatternIntent =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["toggle"]       = "Toggle",      ["toggle-on"]   = "Toggle",   ["toggleon"]   = "Toggle",
                ["toggle-off"]   = "Toggle",      ["toggleoff"]   = "Toggle",   ["gettoggle"]  = "Toggle",
                ["expand"]       = "ExpandCollapse",
                ["collapse"]     = "ExpandCollapse",
                ["expandstate"]  = "ExpandCollapse",
                ["setrange"]     = "RangeValue",  ["getrange"]    = "RangeValue", ["rangeinfo"] = "RangeValue",
                ["select-item"]  = "SelectionItem", ["selectitem"] = "SelectionItem",
                ["addselect"]    = "SelectionItem",
                ["removeselect"] = "SelectionItem",
                ["isselected"]   = "SelectionItem",
                ["select"]       = "Selection",
                ["select-index"] = "Selection",   ["selectindex"] = "Selection",
                ["getitems"]     = "Selection",
                ["getselecteditem"] = "Selection",
                ["scrollinto"]   = "ScrollItem",  ["scrollintoview"] = "ScrollItem",
                ["scrollpercent"]= "Scroll",      ["getscrollinfo"]  = "Scroll",
                ["move"]         = "Transform",   ["resize"]      = "Transform",
                ["griditem"]     = "Grid",        ["gridinfo"]    = "Grid",
                ["griditeminfo"] = "GridItem",
                ["invoke"]       = "Invoke",
            };

        // Hint table: for a given supported pattern, suggests an action the caller could try.
        // Used to populate error_data.hint when the failed pattern is missing but a related
        // pattern is supported (e.g. failed Toggle, but Invoke is available -> suggest 'click').
        private static readonly Dictionary<string, string> s_patternToAction =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["Invoke"]         = "click",
                ["Toggle"]         = "toggle",
                ["Value"]          = "type",
                ["RangeValue"]     = "setrange",
                ["ExpandCollapse"] = "expand",
                ["SelectionItem"]  = "select-item",
                ["Selection"]      = "select",
                ["Transform"]      = "move",
                ["Scroll"]         = "scrollpercent",
                ["ScrollItem"]     = "scrollinto",
                ["Grid"]           = "griditem",
                ["Text"]           = "gettext",
            };

        /// <summary>
        /// Builds structured error data for a failed action: the pattern it tried, the patterns
        /// the element actually supports, the element's enable/offscreen state, and an optional
        /// hint suggesting a fallback action. Returns null when the action isn't pattern-driven
        /// (in which case the caller should emit the bare error string only).
        /// </summary>
        private Dictionary<string, object>? BuildErrorData(AutomationElement el, string actionLower)
        {
            if (!s_actionPatternIntent.TryGetValue(actionLower, out var failedPattern)) return null;

            // Supported patterns: reuse the existing helper that already powers the `patterns`
            // exec action. Returns a comma-separated list; we split it for structured output.
            string[] supported;
            try
            {
                string raw = _helper.GetSupportedPatterns(el) ?? "";
                supported = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            }
            catch { supported = Array.Empty<string>(); }

            bool enabled    = false;
            bool offscreen  = false;
            try { enabled   = el.Properties.IsEnabled.ValueOrDefault; }    catch { }
            try { offscreen = el.Properties.IsOffscreen.ValueOrDefault; }  catch { }

            // Pick the first supported pattern that has an action-name mapping for the hint.
            string? hint = null;
            foreach (var p in supported)
            {
                if (string.Equals(p, failedPattern, StringComparison.OrdinalIgnoreCase)) continue;
                if (s_patternToAction.TryGetValue(p, out var actName))
                { hint = $"Element supports {p}; try action={actName}"; break; }
            }

            var data = new Dictionary<string, object>
            {
                ["failed_pattern"]     = failedPattern,
                ["supported_patterns"] = supported,
                ["element_state"]      = new Dictionary<string, object>
                {
                    ["enabled"]   = enabled,
                    ["offscreen"] = offscreen
                }
            };
            if (hint != null) data["hint"] = hint;
            return data;
        }

        // -- waitfor: generic property-match polling -----------------------------

        /// <summary>
        /// Polls the current element until the requested property satisfies the predicate or the
        /// timeout elapses. Re-resolves the element each iteration so the "gone" predicate works
        /// even after the element is destroyed. Properties: value | text | name | isvisible |
        /// isenabled. Predicates: equals | contains | not-empty | visible | gone.
        /// </summary>
        private CommandResponse RunWaitFor(AutomationElement el, CommandRequest req)
        {
            string predicate = (req.Predicate ?? "").ToLowerInvariant();
            string property  = (req.Property  ?? "").ToLowerInvariant();
            string expected  = req.Expected ?? "";
            int    timeoutMs = req.Timeout  ?? 10_000;
            int    intervalMs = Math.Max(50, req.Interval ?? 200);

            // Predicates that don't need a property (operate on the element itself).
            bool elementLevel = predicate is "visible" or "gone";

            if (string.IsNullOrEmpty(predicate))
                return Fail("waitfor requires a 'predicate' (equals, contains, not-empty, visible, gone).");
            if (!elementLevel && string.IsNullOrEmpty(property))
                return Fail("waitfor predicate '" + predicate + "' requires a 'property' (value, text, name, isvisible, isenabled).");
            if (!elementLevel && property is not ("value" or "text" or "name" or "isvisible" or "isenabled"))
                return Fail($"waitfor property '{property}' is not supported. Valid: value, text, name, isvisible, isenabled.");
            if (predicate is "equals" or "contains" && string.IsNullOrEmpty(expected))
                return Fail("waitfor predicate '" + predicate + "' requires an 'expected' value.");

            var sw = Stopwatch.StartNew();
            string lastObserved = "";
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                bool elementValid = IsElementValid(el);

                // 'gone' is the only predicate that succeeds when the element is no longer valid.
                if (predicate == "gone")
                {
                    if (!elementValid)
                        return Ok($"waitfor satisfied in {sw.ElapsedMilliseconds}ms (element gone)");
                    lastObserved = "present";
                }
                else if (!elementValid)
                {
                    // Other predicates can't read a stale element - keep polling in case a
                    // re-resolved element appears, but most likely we'll just timeout cleanly.
                    Thread.Sleep(intervalMs);
                    continue;
                }
                else if (predicate == "visible")
                {
                    bool offscreen = false;
                    try { offscreen = el.Properties.IsOffscreen.ValueOrDefault; } catch { }
                    lastObserved = offscreen ? "offscreen" : "visible";
                    if (!offscreen)
                        return Ok($"waitfor satisfied in {sw.ElapsedMilliseconds}ms (visible)");
                }
                else
                {
                    // Read the requested property.
                    string actual;
                    try
                    {
                        actual = property switch
                        {
                            "value"     => _helper.GetValue(el),
                            "text"      => _helper.GetText(el),
                            "name"      => el.Properties.Name.ValueOrDefault ?? "",
                            "isvisible" => (!el.Properties.IsOffscreen.ValueOrDefault).ToString(),
                            "isenabled" => el.Properties.IsEnabled.ValueOrDefault.ToString(),
                            _           => ""
                        };
                    }
                    catch { actual = ""; }
                    lastObserved = actual;

                    bool match = predicate switch
                    {
                        "equals"    => string.Equals(actual, expected, StringComparison.Ordinal),
                        "contains"  => actual.Contains(expected, StringComparison.OrdinalIgnoreCase),
                        "not-empty" => !string.IsNullOrEmpty(actual),
                        _           => false
                    };
                    if (match)
                        return OkWithExtras(
                            $"waitfor satisfied in {sw.ElapsedMilliseconds}ms",
                            actual,
                            new Dictionary<string, string>
                            {
                                ["property"]      = property,
                                ["predicate"]     = predicate,
                                ["elapsed_ms"]    = sw.ElapsedMilliseconds.ToString()
                            });
                }
                Thread.Sleep(intervalMs);
            }

            // Timeout - return failure with the last observed value so the caller can debug.
            var errData = new Dictionary<string, object>
            {
                ["timeout_ms"]     = timeoutMs,
                ["predicate"]      = predicate,
                ["property"]       = property,
                ["expected"]       = expected,
                ["last_observed"]  = lastObserved
            };
            return FailWithData($"waitfor timed out after {timeoutMs}ms (predicate={predicate})", errData);
        }

        // -- wait-window: poll the desktop window list ---------------------------

        /// <summary>
        /// Polls the OS window list until a window title satisfies the predicate, or timeout.
        /// Independent of the current-element pointer - lets callers wait for newly-created
        /// windows (e.g. a debug console after launching an app) without a hand-rolled sleep.
        /// On success, sets <see cref="CurrentWindow"/> and registers the id in
        /// <c>_windowMap</c> so the next /find or /elements call resolves it.
        /// Predicates: equals | contains | not-empty | gone.
        /// </summary>
        private CommandResponse RunWaitWindow(CommandRequest req)
        {
            string predicate = (req.Predicate ?? "").ToLowerInvariant();
            string expected  = req.Expected ?? "";
            int    timeoutMs  = req.Timeout  ?? 10_000;
            int    intervalMs = Math.Max(50, req.Interval ?? 250);

            if (string.IsNullOrEmpty(predicate))
                return Fail("wait-window requires a 'predicate' (equals, contains, not-empty, gone).");
            if (predicate is not ("equals" or "contains" or "not-empty" or "gone"))
                return Fail($"wait-window predicate '{predicate}' is not supported. Valid: equals, contains, not-empty, gone.");
            if (predicate is "equals" or "contains" or "gone" && string.IsNullOrEmpty(expected))
                return Fail($"wait-window predicate '{predicate}' requires an 'expected' value.");

            var sw = Stopwatch.StartNew();
            List<string> lastTitles = new();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                FlaUI.Core.AutomationElements.AutomationElement[] windows;
                try { windows = _helper.GetDesktopWindows(); }
                catch { windows = Array.Empty<FlaUI.Core.AutomationElements.AutomationElement>(); }

                lastTitles.Clear();
                FlaUI.Core.AutomationElements.AutomationElement? matched = null;
                string matchedTitle = "";

                foreach (var w in windows)
                {
                    string title;
                    try { title = w.Properties.Name.ValueOrDefault ?? ""; }
                    catch { continue; }
                    lastTitles.Add(title);

                    bool isMatch = predicate switch
                    {
                        "equals"    => string.Equals(title, expected, StringComparison.Ordinal),
                        "contains"  => title.Contains(expected, StringComparison.OrdinalIgnoreCase),
                        "not-empty" => !string.IsNullOrEmpty(title),
                        _           => false
                    };
                    if (isMatch)
                    {
                        matched      = w;
                        matchedTitle = title;
                        break;
                    }
                }

                if (predicate == "gone")
                {
                    // Success when no window currently matches `expected`.
                    bool anyMatch = lastTitles.Any(t =>
                        t.Contains(expected, StringComparison.OrdinalIgnoreCase));
                    if (!anyMatch)
                        return OkWithExtras(
                            $"wait-window satisfied in {sw.ElapsedMilliseconds}ms (gone)", "",
                            new Dictionary<string, string>
                            {
                                ["predicate"]  = predicate,
                                ["elapsed_ms"] = sw.ElapsedMilliseconds.ToString()
                            });
                }
                else if (matched != null)
                {
                    // Register the matched window in the map and make it current so the next
                    // /find element call doesn't need a redundant window= field.
                    var hwnd = matched.Properties.NativeWindowHandle.ValueOrDefault;
                    string hash = _idGen.GenerateElementHash(matched, null, null, hwnd: hwnd, excludeName: true);
                    int id = _idGen.GenerateIdFromHash(hash);
                    var asWindow = matched.AsWindow();
                    _windowMap[id] = asWindow;
                    CurrentWindow  = asWindow;
                    _windowDesc    = matchedTitle;

                    return OkWithExtras(
                        $"wait-window satisfied in {sw.ElapsedMilliseconds}ms",
                        id.ToString(),
                        new Dictionary<string, string>
                        {
                            ["window_id"]  = id.ToString(),
                            ["title"]      = matchedTitle,
                            ["predicate"]  = predicate,
                            ["elapsed_ms"] = sw.ElapsedMilliseconds.ToString()
                        });
                }

                Thread.Sleep(intervalMs);
            }

            // Timeout - return the titles we last saw so the caller can debug.
            var errData = new Dictionary<string, object>
            {
                ["timeout_ms"]            = timeoutMs,
                ["predicate"]             = predicate,
                ["expected"]              = expected,
                ["last_observed_titles"]  = lastTitles.ToArray()
            };
            return FailWithData($"wait-window timed out after {timeoutMs}ms (predicate={predicate})", errData);
        }

        // -- Batch dispatcher --------------------------------------------------

        /// <summary>
        /// Runs each entry in <see cref="CommandRequest.Actions"/> as a full sub-request via
        /// <see cref="Process"/>. Each entry's <c>cmd</c> defaults to "execute" so simple
        /// batches don't need to specify it. <see cref="CommandRequest.StopOnError"/> defaults
        /// to true: the first failing step ends the batch (remaining steps are skipped).
        /// Re-entrant on _stateLock - C# Monitor allows the same thread to re-acquire.
        /// </summary>
        private CommandResponse RunBatch(CommandRequest req)
        {
            bool stopOnError = req.StopOnError ?? true;
            var stepResults  = new List<Dictionary<string, object?>>();
            int succeeded    = 0;
            bool allOk       = true;

            for (int i = 0; i < req.Actions!.Count; i++)
            {
                var step = req.Actions[i];
                if (string.IsNullOrWhiteSpace(step.Command)) step.Command = "execute";
                // 'ai' inference inside batch is rejected for v1 - inference paths run outside
                // _stateLock and mixing them with locked steps risks subtle ordering issues.
                if (step.Command.Equals("ai", StringComparison.OrdinalIgnoreCase))
                {
                    stepResults.Add(new Dictionary<string, object?>
                    {
                        ["step"]    = i,
                        ["cmd"]     = "ai",
                        ["success"] = false,
                        ["error"]   = "ai commands are not supported inside a batch (v1)."
                    });
                    allOk = false;
                    if (stopOnError) break;
                    continue;
                }

                CommandResponse stepResp;
                try { stepResp = Process(step); }
                catch (Exception ex) { stepResp = Fail(ex.Message); }

                var entry = new Dictionary<string, object?>
                {
                    ["step"]    = i,
                    ["cmd"]     = step.Command,
                    ["action"]  = step.Action,
                    ["success"] = stepResp.Success,
                    ["message"] = stepResp.Message,
                };
                if (!string.IsNullOrEmpty(stepResp.Data)) entry["data"]       = stepResp.Data;
                if (stepResp.Extras != null)              entry["extras"]     = stepResp.Extras;
                if (!stepResp.Success)                    entry["error"]      = stepResp.Message;
                if (stepResp.ErrorData != null)           entry["error_data"] = stepResp.ErrorData;
                stepResults.Add(entry);

                if (stepResp.Success) succeeded++;
                else { allOk = false; if (stopOnError) break; }
            }

            var payload = new Dictionary<string, object?>
            {
                ["stop_on_error"] = stopOnError,
                ["total_steps"]   = req.Actions!.Count,
                ["executed"]      = stepResults.Count,
                ["succeeded"]     = succeeded,
                ["results"]       = stepResults
            };
            string json = System.Text.Json.JsonSerializer.Serialize(payload, FormatAdapter.s_indented);
            return new CommandResponse
            {
                Success = allOk,
                Message = allOk
                    ? $"batch: {succeeded}/{req.Actions.Count} steps succeeded"
                    : $"batch: {succeeded}/{req.Actions.Count} steps succeeded; first failure at step {stepResults.Count - 1}",
                Data    = json
            };
        }

    }
}

