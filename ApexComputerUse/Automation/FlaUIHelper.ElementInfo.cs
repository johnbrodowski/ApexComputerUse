using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Capturing;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.Tools;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;

namespace ApexComputerUse
{
    public partial class ApexHelper
    {
        // -- Element info --------------------------------------------------

        public string Describe(AutomationElement el)
        {
            string name = SafeReadProperty(() => el.Properties.Name.ValueOrDefault ?? "");
            string controlType = SafeReadProperty(() => el.Properties.ControlType.ValueOrDefault.ToString(), "Unknown");
            string automationId = SafeReadProperty(() => el.Properties.AutomationId.ValueOrDefault ?? "");
            string enabled = SafeReadProperty(() => el.Properties.IsEnabled.ValueOrDefault.ToString(), "False");
            string offscreen = SafeReadProperty(() => el.Properties.IsOffscreen.ValueOrDefault.ToString(), "False");
            string className = SafeReadProperty(() => el.Properties.ClassName.ValueOrDefault ?? "");
            string framework = SafeReadProperty(() => el.Properties.FrameworkId.ValueOrDefault ?? "");

            return $"Name={name}  ControlType={controlType}  " +
                   $"AutomationId={automationId}  " +
                   $"Enabled={enabled}  Offscreen={offscreen}  " +
                   $"Class={className}  Framework={framework}";
        }

        private static string SafeReadProperty(Func<string> read, string fallback = "")
        {
            try { return read() ?? fallback; }
            catch { return fallback; }
        }

        /// <summary>
        /// Reads the Value pattern content if the element supports it; returns null otherwise.
        /// Safe for tree scans where many elements don't have the pattern - pattern probes
        /// and property reads both throw on unsupported / stale elements, so everything is
        /// wrapped in try/catch.
        /// </summary>
        public string? ReadValuePattern(AutomationElement el)
        {
            try
            {
                if (el.Patterns.Value.TryGetPattern(out var vp))
                    return vp.Value.ValueOrDefault;
            }
            catch { /* pattern unsupported or element went stale - treat as "no value" */ }
            return null;
        }

        /// <summary>Reads the HelpText property safely - returns null if unsupported or empty.</summary>
        public string? ReadHelpText(AutomationElement el)
        {
            try
            {
                var h = el.Properties.HelpText.ValueOrDefault;
                return string.IsNullOrEmpty(h) ? null : h;
            }
            catch { return null; }
        }

        public string GetBoundingRect(AutomationElement el)
        {
            // Stale-guarded: a dead UIA proxy throws COMException / ElementNotAvailableException
            // on property access. Return a sentinel rather than crash the dispatcher; recovery
            // happens upstream in CommandProcessor (parent-walk).
            try
            {
                var r = el.BoundingRectangle;
                return $"X={r.X}  Y={r.Y}  Width={r.Width}  Height={r.Height}";
            }
            catch (Exception ex)
            {
                CommandProcessor.LogSwallowed("GetBoundingRect/staleProxy", ex);
                return "(unavailable)";
            }
        }

        public string GetSupportedPatterns(AutomationElement el)
        {
            // Each IsSupported probe can throw on a stale proxy; wrap individually so a single
            // dead pattern doesn't void the whole list. Falls back to "(none)" when the element
            // is wholly unreachable.
            var supported = new List<string>();
            void Probe(string label, Func<bool> check)
            {
                try { if (check()) supported.Add(label); }
                catch (Exception ex) { CommandProcessor.LogSwallowed($"GetSupportedPatterns/{label}", ex); }
            }
            Probe("Invoke",          () => el.Patterns.Invoke.IsSupported);
            Probe("Value",           () => el.Patterns.Value.IsSupported);
            Probe("RangeValue",      () => el.Patterns.RangeValue.IsSupported);
            Probe("Selection",       () => el.Patterns.Selection.IsSupported);
            Probe("SelectionItem",   () => el.Patterns.SelectionItem.IsSupported);
            Probe("Toggle",          () => el.Patterns.Toggle.IsSupported);
            Probe("ExpandCollapse",  () => el.Patterns.ExpandCollapse.IsSupported);
            Probe("Grid",            () => el.Patterns.Grid.IsSupported);
            Probe("GridItem",        () => el.Patterns.GridItem.IsSupported);
            Probe("Table",           () => el.Patterns.Table.IsSupported);
            Probe("Scroll",          () => el.Patterns.Scroll.IsSupported);
            Probe("ScrollItem",      () => el.Patterns.ScrollItem.IsSupported);
            Probe("Transform",       () => el.Patterns.Transform.IsSupported);
            Probe("Transform2",      () => el.Patterns.Transform2.IsSupported);
            Probe("Dock",            () => el.Patterns.Dock.IsSupported);
            Probe("Text",            () => el.Patterns.Text.IsSupported);
            Probe("Window",          () => el.Patterns.Window.IsSupported);
            Probe("MultipleView",    () => el.Patterns.MultipleView.IsSupported);
            Probe("VirtualizedItem", () => el.Patterns.VirtualizedItem.IsSupported);
            Probe("Annotation",      () => el.Patterns.Annotation.IsSupported);
            return supported.Count > 0 ? string.Join(", ", supported) : "(none)";
        }

        /// <summary>Returns whether the element is enabled. Stale-safe: returns "False" on COM failure.</summary>
        public string IsElementEnabled(AutomationElement el)
        {
            try { return el.Properties.IsEnabled.ValueOrDefault.ToString(); }
            catch (Exception ex)
            {
                CommandProcessor.LogSwallowed("IsElementEnabled/staleProxy", ex);
                return "False";
            }
        }

        /// <summary>Returns whether the element is visible (not off-screen). Stale-safe: returns "False" on COM failure.</summary>
        public string IsElementVisible(AutomationElement el)
        {
            try { return (!el.Properties.IsOffscreen.ValueOrDefault).ToString(); }
            catch (Exception ex)
            {
                CommandProcessor.LogSwallowed("IsElementVisible/staleProxy", ex);
                return "False";
            }
        }

        // -- Focus ---------------------------------------------------------

        public void SetFocus(AutomationElement el)
        {
            // WPF / WinForms controls often refuse Focus() unless their hosting window
            // is foreground; bring it forward first so callers see consistent behaviour.
            BringContainerWindowToFront(el);
            el.Focus();
        }

        public string GetFocusedElement()
        {
            var el = _automation.FocusedElement();
            return el == null ? "(none)" : Describe(el);
        }

        // -- Click (smart: Invoke -> Toggle -> SelectionItem -> mouse) --------

        /// <summary>
        /// Smart click: tries Invoke, then Toggle, then SelectionItem, falls back to mouse click.
        /// </summary>
        public void ClickElement(AutomationElement el)
        {
            if (el.Patterns.Invoke.TryGetPattern(out var invoke))     { invoke.Invoke(); return; }
            if (el.Patterns.Toggle.TryGetPattern(out var toggle))     { toggle.Toggle(); return; }
            if (el.Patterns.SelectionItem.TryGetPattern(out var si))  { si.Select(); return; }

            // Mouse fallback needs a clickable point. Foreground/restore the container
            // window first so off-screen or minimized windows become clickable, and
            // translate FlaUI's bare NoClickablePointException into a useful message.
            BringContainerWindowToFront(el);
            try { el.Click(); }
            catch (FlaUI.Core.Exceptions.NoClickablePointException)
            {
                throw new InvalidOperationException(
                    "Element has no clickable point - it may be off-screen, zero-size, or fully covered by another window.");
            }
        }

        /// <summary>Mouse-only left click (bypasses pattern logic).</summary>
        public void MouseClickElement(AutomationElement el)
        {
            // Mouse input is window-scoped: clicks land in whichever window is foreground.
            // Bring the container forward so the click reaches the intended target.
            BringContainerWindowToFront(el);
            el.Click();
        }

        public void RightClickElement(AutomationElement el)
        {
            BringContainerWindowToFront(el);
            el.RightClick();
        }

        public void DoubleClickElement(AutomationElement el)
        {
            BringContainerWindowToFront(el);
            el.DoubleClick();
        }

        public void MiddleClickElement(AutomationElement el)
        {
            BringContainerWindowToFront(el);
            var pt = el.GetClickablePoint();
            Mouse.Click(pt, MouseButton.Middle);
        }

        /// <summary>Click at x,y offset from the element's top-left corner.</summary>
        public void ClickAtOffset(AutomationElement el, int offsetX, int offsetY)
        {
            BringContainerWindowToFront(el);
            var r = el.BoundingRectangle;
            Mouse.Click(new System.Drawing.Point((int)r.X + offsetX, (int)r.Y + offsetY));
        }

        public void HoverElement(AutomationElement el)
        {
            var center = el.GetClickablePoint();
            Mouse.MoveTo(center);
        }

        // -- Button / Hyperlink / MenuItem ---------------------------------

        public void InvokeButton(AutomationElement el) =>
            el.AsButton().Invoke();

        public void InvokePattern(AutomationElement el) =>
            el.Patterns.Invoke.Pattern.Invoke();

        // -- Invoke/Toggle/SelectionItem pattern direct access -------------

        public void SelectItem(AutomationElement el)
        {
            if (el.Patterns.SelectionItem.TryGetPattern(out var p)) p.Select();
            else throw new InvalidOperationException("SelectionItem pattern not supported");
        }

        public void AddToSelection(AutomationElement el)
        {
            if (el.Patterns.SelectionItem.TryGetPattern(out var p)) p.AddToSelection();
            else throw new InvalidOperationException("SelectionItem pattern not supported");
        }

        public void RemoveFromSelection(AutomationElement el)
        {
            if (el.Patterns.SelectionItem.TryGetPattern(out var p)) p.RemoveFromSelection();
            else throw new InvalidOperationException("SelectionItem pattern not supported");
        }

        public string IsSelected(AutomationElement el)
        {
            if (el.Patterns.SelectionItem.TryGetPattern(out var p)) return p.IsSelected.ValueOrDefault.ToString();
            return "SelectionItem pattern not supported";
        }

        /// <summary>Returns names of currently selected items from a Selection container.</summary>
        public string GetSelectionInfo(AutomationElement el)
        {
            if (!el.Patterns.Selection.TryGetPattern(out var p)) return "Selection pattern not supported";
            var items = p.Selection.ValueOrDefault ?? Array.Empty<AutomationElement>();
            var names = items.Select(i => i.Properties.Name.ValueOrDefault ?? "(unnamed)").ToArray();
            bool multi    = p.CanSelectMultiple.ValueOrDefault;
            bool required = p.IsSelectionRequired.ValueOrDefault;
            return $"Selected: [{string.Join(", ", names)}]  CanMultiSelect={multi}  SelectionRequired={required}";
        }

    }
}

