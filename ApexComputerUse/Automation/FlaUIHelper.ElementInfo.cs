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
        // ── Element info ──────────────────────────────────────────────────

        public string Describe(AutomationElement el) =>
            $"Name={el.Properties.Name.ValueOrDefault}  ControlType={el.Properties.ControlType.ValueOrDefault}  " +
            $"AutomationId={el.Properties.AutomationId.ValueOrDefault}  " +
            $"Enabled={el.Properties.IsEnabled.ValueOrDefault}  Offscreen={el.Properties.IsOffscreen.ValueOrDefault}  " +
            $"Class={el.Properties.ClassName.ValueOrDefault}  Framework={el.Properties.FrameworkId.ValueOrDefault}";

        /// <summary>
        /// Reads the Value pattern content if the element supports it; returns null otherwise.
        /// Safe for tree scans where many elements don't have the pattern — pattern probes
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
            catch { /* pattern unsupported or element went stale — treat as "no value" */ }
            return null;
        }

        /// <summary>Reads the HelpText property safely — returns null if unsupported or empty.</summary>
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
            var r = el.BoundingRectangle;
            return $"X={r.X}  Y={r.Y}  Width={r.Width}  Height={r.Height}";
        }

        public string GetSupportedPatterns(AutomationElement el)
        {
            var supported = new List<string>();
            if (el.Patterns.Invoke.IsSupported)         supported.Add("Invoke");
            if (el.Patterns.Value.IsSupported)          supported.Add("Value");
            if (el.Patterns.RangeValue.IsSupported)     supported.Add("RangeValue");
            if (el.Patterns.Selection.IsSupported)      supported.Add("Selection");
            if (el.Patterns.SelectionItem.IsSupported)  supported.Add("SelectionItem");
            if (el.Patterns.Toggle.IsSupported)         supported.Add("Toggle");
            if (el.Patterns.ExpandCollapse.IsSupported) supported.Add("ExpandCollapse");
            if (el.Patterns.Grid.IsSupported)           supported.Add("Grid");
            if (el.Patterns.GridItem.IsSupported)       supported.Add("GridItem");
            if (el.Patterns.Table.IsSupported)          supported.Add("Table");
            if (el.Patterns.Scroll.IsSupported)         supported.Add("Scroll");
            if (el.Patterns.ScrollItem.IsSupported)     supported.Add("ScrollItem");
            if (el.Patterns.Transform.IsSupported)      supported.Add("Transform");
            if (el.Patterns.Transform2.IsSupported)     supported.Add("Transform2");
            if (el.Patterns.Dock.IsSupported)           supported.Add("Dock");
            if (el.Patterns.Text.IsSupported)           supported.Add("Text");
            if (el.Patterns.Window.IsSupported)         supported.Add("Window");
            if (el.Patterns.MultipleView.IsSupported)   supported.Add("MultipleView");
            if (el.Patterns.VirtualizedItem.IsSupported)supported.Add("VirtualizedItem");
            if (el.Patterns.Annotation.IsSupported)     supported.Add("Annotation");
            return supported.Count > 0 ? string.Join(", ", supported) : "(none)";
        }

        /// <summary>Returns whether the element is enabled.</summary>
        public string IsElementEnabled(AutomationElement el) =>
            el.Properties.IsEnabled.ValueOrDefault.ToString();

        /// <summary>Returns whether the element is visible (not off-screen).</summary>
        public string IsElementVisible(AutomationElement el) =>
            (!el.Properties.IsOffscreen.ValueOrDefault).ToString();

        // ── Focus ─────────────────────────────────────────────────────────

        public void SetFocus(AutomationElement el) => el.Focus();

        public string GetFocusedElement()
        {
            var el = _automation.FocusedElement();
            return el == null ? "(none)" : Describe(el);
        }

        // ── Click (smart: Invoke → Toggle → SelectionItem → mouse) ────────

        /// <summary>
        /// Smart click: tries Invoke, then Toggle, then SelectionItem, falls back to mouse click.
        /// </summary>
        public void ClickElement(AutomationElement el)
        {
            if (el.Patterns.Invoke.TryGetPattern(out var invoke))     { invoke.Invoke(); return; }
            if (el.Patterns.Toggle.TryGetPattern(out var toggle))     { toggle.Toggle(); return; }
            if (el.Patterns.SelectionItem.TryGetPattern(out var si))  { si.Select(); return; }
            el.Click();
        }

        /// <summary>Mouse-only left click (bypasses pattern logic).</summary>
        public void MouseClickElement(AutomationElement el) => el.Click();

        public void RightClickElement(AutomationElement el) => el.RightClick();

        public void DoubleClickElement(AutomationElement el) => el.DoubleClick();

        public void MiddleClickElement(AutomationElement el)
        {
            var pt = el.GetClickablePoint();
            Mouse.Click(pt, MouseButton.Middle);
        }

        /// <summary>Click at x,y offset from the element's top-left corner.</summary>
        public void ClickAtOffset(AutomationElement el, int offsetX, int offsetY)
        {
            var r = el.BoundingRectangle;
            Mouse.Click(new System.Drawing.Point((int)r.X + offsetX, (int)r.Y + offsetY));
        }

        public void HoverElement(AutomationElement el)
        {
            var center = el.GetClickablePoint();
            Mouse.MoveTo(center);
        }

        // ── Button / Hyperlink / MenuItem ─────────────────────────────────

        public void InvokeButton(AutomationElement el) =>
            el.AsButton().Invoke();

        public void InvokePattern(AutomationElement el) =>
            el.Patterns.Invoke.Pattern.Invoke();

        // ── Invoke/Toggle/SelectionItem pattern direct access ─────────────

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
