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
        // -- Text / Value reading (smart fallback chains) ------------------

        /// <summary>
        /// Gets element text: Text pattern -> Value pattern -> Name property.
        /// </summary>
        public string GetText(AutomationElement el) => GetText(el, out _);

        /// <summary>
        /// Gets element text and reports which UIA pattern supplied the result. Source values:
        /// "TextPattern", "ValuePattern", "LegacyIAccessible", "Name". Lets agents distinguish a
        /// legitimate text read from a degenerate Name fallback.
        /// </summary>
        public string GetText(AutomationElement el, out string source)
        {
            if (el.Patterns.Text.TryGetPattern(out var tp))
            { source = "TextPattern"; return tp.DocumentRange.GetText(-1); }
            if (el.Patterns.Value.TryGetPattern(out var vp))
            { source = "ValuePattern"; return vp.Value.ValueOrDefault ?? ""; }
            if (el.Patterns.LegacyIAccessible.TryGetPattern(out var la))
            {
                var v = la.Value.ValueOrDefault;
                if (!string.IsNullOrEmpty(v)) { source = "LegacyIAccessible"; return v; }
            }
            source = "Name";
            return el.Properties.Name.ValueOrDefault ?? "";
        }

        /// <summary>
        /// Gets element value: Value pattern -> Text pattern -> LegacyIAccessible -> Name.
        /// </summary>
        public string GetValue(AutomationElement el) => GetValue(el, out _);

        /// <summary>
        /// Gets element value and reports which UIA pattern supplied the result. Source values:
        /// "ValuePattern", "TextPattern", "LegacyIAccessible", "Name".
        /// </summary>
        public string GetValue(AutomationElement el, out string source)
        {
            if (el.Patterns.Value.TryGetPattern(out var vp))
            { source = "ValuePattern"; return vp.Value.ValueOrDefault ?? ""; }
            if (el.Patterns.Text.TryGetPattern(out var tp))
            { source = "TextPattern"; return tp.DocumentRange.GetText(-1); }
            if (el.Patterns.LegacyIAccessible.TryGetPattern(out var la))
            {
                var v = la.Value.ValueOrDefault;
                if (!string.IsNullOrEmpty(v)) { source = "LegacyIAccessible"; return v; }
            }
            source = "Name";
            return el.Properties.Name.ValueOrDefault ?? "";
        }

        /// <summary>Gets the currently selected text via Text pattern selection ranges.</summary>
        public string GetSelectedText(AutomationElement el) => GetSelectedText(el, out _);

        /// <summary>
        /// Gets the currently selected text and reports the source pattern.
        /// Source is "TextPattern" on success, "None" when the pattern is not supported.
        /// </summary>
        public string GetSelectedText(AutomationElement el, out string source)
        {
            if (!el.Patterns.Text.TryGetPattern(out var tp))
            { source = "None"; return "Text pattern not supported"; }
            source = "TextPattern";
            var selections = tp.GetSelection();
            return selections.Length > 0
                ? string.Join("", selections.Select(s => s.GetText(-1)))
                : "";
        }

        // -- Value writing (smart fallback chains) -------------------------

        /// <summary>
        /// Sets element value: Value pattern -> RangeValue (if numeric) -> keyboard fallback.
        /// </summary>
        public void SetValue(AutomationElement el, string value)
        {
            if (el.Patterns.Value.TryGetPattern(out var vp))
            {
                if (!vp.IsReadOnly.ValueOrDefault) { vp.SetValue(value); return; }
            }
            if (el.Patterns.RangeValue.TryGetPattern(out var rvp))
            {
                if (!rvp.IsReadOnly.ValueOrDefault && double.TryParse(value, out double d))
                { rvp.SetValue(d); return; }
            }
            // Keyboard fallback: select-all, delete, type
            el.Focus();
            Thread.Sleep(FocusDelayMs);
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
            Thread.Sleep(30);
            Keyboard.Press(VirtualKeyShort.DELETE);
            Keyboard.Release(VirtualKeyShort.DELETE);
            Thread.Sleep(30);
            Keyboard.Type(value);
        }

        /// <summary>Clears the element value (sets to empty string or 0 for range).</summary>
        public void ClearValue(AutomationElement el)
        {
            if (el.Patterns.Value.TryGetPattern(out var vp))
            {
                if (!vp.IsReadOnly.ValueOrDefault) { vp.SetValue(""); return; }
            }
            SelectAllText(el);
            Keyboard.Press(VirtualKeyShort.DELETE);
            Keyboard.Release(VirtualKeyShort.DELETE);
        }

        /// <summary>Appends text to the current element value.</summary>
        public void AppendValue(AutomationElement el, string value)
        {
            if (el.Patterns.Value.TryGetPattern(out var vp) && !vp.IsReadOnly.ValueOrDefault)
            {
                vp.SetValue((vp.Value.ValueOrDefault ?? "") + value);
                return;
            }
            el.Focus();
            Thread.Sleep(FocusDelayMs);
            Keyboard.Press(VirtualKeyShort.END);
            Keyboard.Release(VirtualKeyShort.END);
            Keyboard.Type(value);
        }

        // -- RangeValue pattern --------------------------------------------
        // NOTE: WinForms TrackBar always exposes UIA RangeValue in the range 0-100,
        // regardless of the control's actual Minimum/Maximum. Callers must use UIA-scaled
        // values (e.g. UIA=20 ? actual 200 for a slider with actual max=1000).
        // Valid snap points for non-unit TickFrequency are multiples of (100?(max-min)).

        public void SetRangeValue(AutomationElement el, double value)
        {
            if (el.Patterns.RangeValue.TryGetPattern(out var p))
            {
                // WinForms TrackBar: SetValue throws for large ranges, or silently no-ops - verify.
                try
                {
                    p.SetValue(value);
                    Thread.Sleep(50);
                    if (Math.Abs(p.Value.ValueOrDefault - value) < 0.5) return;
                }
                catch { /* fall through to Value / keyboard fallback */ }
            }
            // Fallback: write via Value pattern (e.g. WinForms TrackBar exposes Value not RangeValue)
            if (el.Patterns.Value.TryGetPattern(out var vp) && !vp.IsReadOnly.ValueOrDefault)
            {
                try
                {
                    vp.SetValue(value.ToString("G"));
                    Thread.Sleep(50);
                    // WinForms TrackBar's Value setter silently no-ops for arbitrary numbers;
                    // verify the read-back before declaring success.
                    if (double.TryParse(vp.Value.ValueOrDefault, out var v2) &&
                        Math.Abs(v2 - value) < 0.5) return;
                }
                catch { /* fall through */ }
            }
            // Fallback: WinForms NumericUpDown - set value via child Edit element
            var childEdit = el.FindAllChildren()
                .FirstOrDefault(c => c.Properties.ControlType.ValueOrDefault == FlaUI.Core.Definitions.ControlType.Edit);
            if (childEdit != null && childEdit.Patterns.Value.TryGetPattern(out var cvp) && !cvp.IsReadOnly.ValueOrDefault)
            {
                try
                {
                    cvp.SetValue(value.ToString("G"));
                    Thread.Sleep(50);
                    if (double.TryParse(cvp.Value.ValueOrDefault, out var v3) &&
                        Math.Abs(v3 - value) < 0.5) return;
                }
                catch { /* fall through to keyboard */ }
            }
            // Keyboard fallback for controls like WinForms TrackBar whose Value pattern is
            // present but read-only (pattern writes are silently ignored). Press Home to reach
            // the minimum, probe the actual step size with one Right press, then advance to target.
            el.Focus();
            Thread.Sleep(FocusDelayMs);
            Keyboard.Press(VirtualKeyShort.HOME); Keyboard.Release(VirtualKeyShort.HOME);
            Thread.Sleep(50);
            if (!double.TryParse(GetRangeValue(el), out double min)) min = 0;
            Keyboard.Press(VirtualKeyShort.RIGHT); Keyboard.Release(VirtualKeyShort.RIGHT);
            Thread.Sleep(50);
            double step = double.TryParse(GetRangeValue(el), out double afterOne) && afterOne > min
                          ? afterOne - min : 1;
            Keyboard.Press(VirtualKeyShort.HOME); Keyboard.Release(VirtualKeyShort.HOME);
            Thread.Sleep(30);
            int steps = (int)Math.Round((value - min) / step);
            for (int i = 0; i < Math.Max(0, steps); i++)
            {
                Keyboard.Press(VirtualKeyShort.RIGHT); Keyboard.Release(VirtualKeyShort.RIGHT);
                Thread.Sleep(10);
            }
        }

        public string GetRangeValue(AutomationElement el)
        {
            if (el.Patterns.RangeValue.TryGetPattern(out var p)) return p.Value.ValueOrDefault.ToString("G");
            if (el.Patterns.Value.TryGetPattern(out var vp)) return vp.Value.ValueOrDefault ?? "";
            // Fallback: WinForms NumericUpDown - read value from child Edit element
            var childEdit = el.FindAllChildren()
                .FirstOrDefault(c => c.Properties.ControlType.ValueOrDefault == FlaUI.Core.Definitions.ControlType.Edit);
            if (childEdit != null && childEdit.Patterns.Value.TryGetPattern(out var cvp))
                return cvp.Value.ValueOrDefault ?? "";
            return "RangeValue pattern not supported";
        }

        public string GetRangeInfo(AutomationElement el)
        {
            if (el.Patterns.RangeValue.TryGetPattern(out var p))
                return $"Value={p.Value.ValueOrDefault:G}  Min={p.Minimum.ValueOrDefault:G}  " +
                       $"Max={p.Maximum.ValueOrDefault:G}  SmallChange={p.SmallChange.ValueOrDefault:G}  " +
                       $"LargeChange={p.LargeChange.ValueOrDefault:G}";
            // Fallback: Slider API (WinForms TrackBar uses Value pattern, not RangeValue).
            // Read each property via the raw Value pattern to avoid FlaUI's Slider wrapper
            // calling internal pattern accessors that throw NullReferenceException.
            if (el.Patterns.Value.TryGetPattern(out var valuePat))
            {
                double val = 0;
                if (double.TryParse(valuePat.Value.ValueOrDefault, out var parsed)) val = parsed;
                var ct = el.Properties.ControlType.ValueOrDefault;
                // For Slider/ScrollBar try to get min/max from FlaUI wrapper safely
                double min = 0, max = 100, small = double.NaN, large = double.NaN;
                try { var s = el.AsSlider(); min = s.Minimum; max = s.Maximum; small = s.SmallChange; large = s.LargeChange; } catch { }
                return $"Value={val:G}  Min={min:G}  Max={max:G}  SmallChange={small:G}  LargeChange={large:G}";
            }
            return "RangeValue pattern not supported";
        }

        // -- Toggle pattern ------------------------------------------------

        public void ToggleCheckBox(AutomationElement el)
        {
            if (el.Patterns.Toggle.TryGetPattern(out var p)) p.Toggle();
            else ClickElement(el);
        }

        public string GetToggleState(AutomationElement el)
        {
            if (el.Patterns.Toggle.TryGetPattern(out var p))
                return p.ToggleState.ValueOrDefault.ToString();

            return IsCheckBoxChecked(el) switch
            {
                true => ToggleState.On.ToString(),
                false => ToggleState.Off.ToString(),
                null => ToggleState.Indeterminate.ToString()
            };
        }

        /// <summary>Sets a toggle element to a specific on (true) or off (false) state.</summary>
        public void SetToggleState(AutomationElement el, bool targetOn)
        {
            if (el.Patterns.Toggle.TryGetPattern(out var p))
            {
                var state = p.ToggleState.ValueOrDefault;
                bool isOn = state == ToggleState.On;
                if (isOn != targetOn) p.Toggle();
                // Handle 3-state: if we toggled to Indeterminate, toggle once more
                if (p.ToggleState.ValueOrDefault == ToggleState.Indeterminate) p.Toggle();
                return;
            }

            for (int i = 0; i < 3; i++)
            {
                var state = IsCheckBoxChecked(el);
                if (state.HasValue && state.Value == targetOn) return;
                ClickElement(el);
                Thread.Sleep(100);
            }

            throw new InvalidOperationException("Unable to set checkbox state");
        }

        public bool? IsCheckBoxChecked(AutomationElement el)
        {
            try { return el.AsCheckBox().IsChecked; }
            catch { return null; }
        }

        // -- ExpandCollapse pattern ----------------------------------------

        public string GetExpandCollapseState(AutomationElement el)
        {
            if (el.Patterns.ExpandCollapse.TryGetPattern(out var p))
                return p.ExpandCollapseState.ValueOrDefault.ToString();
            return "ExpandCollapse pattern not supported";
        }

    }
}

