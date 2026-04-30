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
            if (!IsElementValid(target)) return Fail("The selected element is stale - the target app has changed state. Run 'find' again.");

            string result = RunAction(target, CurrentWindow, req.Action!, req.Value ?? "");
            return string.IsNullOrEmpty(result)
                ? Ok($"'{req.Action}' executed.")
                : Ok($"'{req.Action}' executed.", result);
        }

    }
}

