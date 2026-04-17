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
    /// <summary>
    /// Wraps FlaUI interactions for every common WPF/WinForms control type and pattern.
    /// All pattern access uses TryGetPattern to avoid exceptions on unsupported elements.
    /// </summary>
    public class ApexHelper : IDisposable
    {
        private readonly UIA3Automation _automation = new();

        private const int FocusDelayMs    = 50;
        private const int DragStepDelayMs = 100;

        // ── Window ────────────────────────────────────────────────────────

        public Window? FindWindow(string titleContains)
        {
            var desktop = _automation.GetDesktop();
            var match = desktop
                .FindAllChildren()
                .FirstOrDefault(w => w.Name.Contains(titleContains, StringComparison.OrdinalIgnoreCase));
            return match?.AsWindow();
        }

        public void MoveWindow(Window window, int x, int y) =>
            window.Patterns.Transform.Pattern.Move(x, y);

        public void ResizeWindow(Window window, double width, double height) =>
            window.Patterns.Transform.Pattern.Resize(width, height);

        public void MinimizeWindow(Window window) =>
            window.Patterns.Window.Pattern.SetWindowVisualState(WindowVisualState.Minimized);

        public void MaximizeWindow(Window window) =>
            window.Patterns.Window.Pattern.SetWindowVisualState(WindowVisualState.Maximized);

        public void RestoreWindow(Window window) =>
            window.Patterns.Window.Pattern.SetWindowVisualState(WindowVisualState.Normal);

        public void CloseWindow(Window window) =>
            window.Close();

        /// <summary>Gets Normal / Maximized / Minimized state of the element's window.</summary>
        public string GetWindowState(AutomationElement el)
        {
            if (el.Patterns.Window.TryGetPattern(out var wp))
                return wp.WindowVisualState.ValueOrDefault.ToString();
            return "Window pattern not supported";
        }

        // ── Window fuzzy find ─────────────────────────────────────────────

        public Window? FindWindowFuzzy(string title, out string matchedTitle, out bool isExact)
        {
            var all = _automation.GetDesktop().FindAllChildren()
                          .Where(w => !string.IsNullOrWhiteSpace(w.Name))
                          .ToArray();

            // 1. Exact (case-insensitive)
            var exact = all.FirstOrDefault(w => w.Name.Equals(title, StringComparison.OrdinalIgnoreCase));
            if (exact != null) { matchedTitle = exact.Name; isExact = true; return exact.AsWindow(); }

            // 2. Contains
            var contains = all.Where(w => w.Name.Contains(title, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (contains.Length == 1) { matchedTitle = contains[0].Name; isExact = false; return contains[0].AsWindow(); }

            // 3. Closest Levenshtein (with distance threshold)
            var closest = all.MinBy(w => Levenshtein(title.ToLower(), w.Name.ToLower()));
            if (closest == null) { matchedTitle = ""; isExact = false; return null; }
            int bestDist = Levenshtein(title.ToLower(), closest.Name.ToLower());
            if (bestDist > (int)(title.Length * 0.6)) { matchedTitle = ""; isExact = false; return null; }
            matchedTitle = closest.Name;
            isExact = false;
            return closest.AsWindow();
        }

        // ── Element fuzzy find ────────────────────────────────────────────

        public AutomationElement? FindElementFuzzy(
            Window window, string searchText, ControlType? filterType, bool searchById,
            out string matchedValue, out bool isExact)
        {
            AutomationElement[] all;
            try
            {
                var fetchTask = filterType.HasValue
                    ? Task.Run(() => window.FindAllDescendants(cf => cf.ByControlType(filterType.Value)))
                    : Task.Run(() => window.FindAllDescendants());
                if (!fetchTask.Wait(5000) || fetchTask.Result == null)
                    { matchedValue = ""; isExact = false; return null; }
                all = fetchTask.Result;
            }
            catch { matchedValue = ""; isExact = false; return null; }

            if (all.Length == 0) { matchedValue = ""; isExact = false; return null; }

            Func<AutomationElement, string> getValue = searchById
                ? el => el.Properties.AutomationId.ValueOrDefault ?? ""
                : el => el.Properties.Name.ValueOrDefault ?? "";

            var exactEl = all.FirstOrDefault(el =>
                getValue(el).Equals(searchText, StringComparison.OrdinalIgnoreCase));
            if (exactEl != null) { matchedValue = getValue(exactEl); isExact = true; return exactEl; }

            var cont = all.Where(el =>
                getValue(el).Contains(searchText, StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrEmpty(getValue(el))).ToArray();
            if (cont.Length == 1) { matchedValue = getValue(cont[0]); isExact = false; return cont[0]; }

            var cands = all.Where(el => !string.IsNullOrEmpty(getValue(el))).ToArray();
            if (cands.Length == 0) { matchedValue = ""; isExact = false; return null; }

            var closest2 = cands.MinBy(el => Levenshtein(searchText.ToLower(), getValue(el).ToLower()))!;
            int bestDist2 = Levenshtein(searchText.ToLower(), getValue(closest2).ToLower());
            if (bestDist2 > (int)(searchText.Length * 0.6))
                { matchedValue = ""; isExact = false; return null; }
            matchedValue = getValue(closest2);
            isExact = false;
            return closest2;
        }

        // ── Levenshtein ───────────────────────────────────────────────────

        public static int Levenshtein(string a, string b)
        {
            if (a.Length == 0) return b.Length;
            if (b.Length == 0) return a.Length;
            var d = new int[a.Length + 1, b.Length + 1];
            for (int i = 0; i <= a.Length; i++) d[i, 0] = i;
            for (int j = 0; j <= b.Length; j++) d[0, j] = j;
            for (int i = 1; i <= a.Length; i++)
                for (int j = 1; j <= b.Length; j++)
                    d[i, j] = a[i - 1] == b[j - 1]
                        ? d[i - 1, j - 1]
                        : 1 + Math.Min(d[i - 1, j - 1], Math.Min(d[i - 1, j], d[i, j - 1]));
            return d[a.Length, b.Length];
        }

        // ── Element lookup ────────────────────────────────────────────────

        public AutomationElement? FindByAutomationId(Window window, string automationId) =>
            window.FindFirstDescendant(cf => cf.ByAutomationId(automationId));

        public AutomationElement? FindByName(Window window, string name) =>
            window.FindFirstDescendant(cf => cf.ByName(name));

        public AutomationElement? WaitForElement(Window window, string automationId, int timeoutMs = 5000) =>
            Retry.WhileNull(
                () => window.FindFirstDescendant(cf => cf.ByAutomationId(automationId)),
                TimeSpan.FromMilliseconds(timeoutMs),
                TimeSpan.FromMilliseconds(200)
            ).Result;

        // ── Discovery ─────────────────────────────────────────────────────

        public AutomationElement[] GetDesktopWindows() =>
            _automation.GetDesktop()
                .FindAllChildren()
                .Where(w => !string.IsNullOrWhiteSpace(w.Name))
                .ToArray();

        public string[] ListWindowTitles() =>
            GetDesktopWindows().Select(w => w.Name).OrderBy(x => x).ToArray();

        public string[] ListElements(Window window, ControlType? filterType = null)
        {
            var all = filterType.HasValue
                ? window.FindAllDescendants(cf => cf.ByControlType(filterType.Value))
                : window.FindAllDescendants();
            return all
                .Where(e => !string.IsNullOrWhiteSpace(e.Name) || !string.IsNullOrWhiteSpace(e.AutomationId))
                .Select(e => $"[{e.ControlType,-16}] Name='{e.Name}'  Id='{e.AutomationId}'")
                .ToArray();
        }

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

        // ── Text / Value reading (smart fallback chains) ──────────────────

        /// <summary>
        /// Gets element text: Text pattern → Value pattern → Name property.
        /// </summary>
        public string GetText(AutomationElement el)
        {
            if (el.Patterns.Text.TryGetPattern(out var tp))
                return tp.DocumentRange.GetText(-1);
            if (el.Patterns.Value.TryGetPattern(out var vp))
                return vp.Value.ValueOrDefault ?? "";
            if (el.Patterns.LegacyIAccessible.TryGetPattern(out var la))
            {
                var v = la.Value.ValueOrDefault;
                if (!string.IsNullOrEmpty(v)) return v;
            }
            return el.Properties.Name.ValueOrDefault ?? "";
        }

        /// <summary>
        /// Gets element value: Value pattern → Text pattern → LegacyIAccessible → Name.
        /// </summary>
        public string GetValue(AutomationElement el)
        {
            if (el.Patterns.Value.TryGetPattern(out var vp))
                return vp.Value.ValueOrDefault ?? "";
            if (el.Patterns.Text.TryGetPattern(out var tp))
                return tp.DocumentRange.GetText(-1);
            if (el.Patterns.LegacyIAccessible.TryGetPattern(out var la))
            {
                var v = la.Value.ValueOrDefault;
                if (!string.IsNullOrEmpty(v)) return v;
            }
            return el.Properties.Name.ValueOrDefault ?? "";
        }

        /// <summary>Gets the currently selected text via Text pattern selection ranges.</summary>
        public string GetSelectedText(AutomationElement el)
        {
            if (!el.Patterns.Text.TryGetPattern(out var tp))
                return "Text pattern not supported";
            var selections = tp.GetSelection();
            return selections.Length > 0
                ? string.Join("", selections.Select(s => s.GetText(-1)))
                : "";
        }

        // ── Value writing (smart fallback chains) ─────────────────────────

        /// <summary>
        /// Sets element value: Value pattern → RangeValue (if numeric) → keyboard fallback.
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

        // ── RangeValue pattern ────────────────────────────────────────────

        public void SetRangeValue(AutomationElement el, double value)
        {
            if (el.Patterns.RangeValue.TryGetPattern(out var p)) { p.SetValue(value); return; }
            // Fallback: write via Value pattern (e.g. WinForms TrackBar exposes Value not RangeValue)
            if (el.Patterns.Value.TryGetPattern(out var vp) && !vp.IsReadOnly.ValueOrDefault)
            {
                vp.SetValue(value.ToString("G"));
                return;
            }
            // Fallback: WinForms NumericUpDown — set value via child Edit element
            var childEdit = el.FindAllChildren()
                .FirstOrDefault(c => c.Properties.ControlType.ValueOrDefault == FlaUI.Core.Definitions.ControlType.Edit);
            if (childEdit != null && childEdit.Patterns.Value.TryGetPattern(out var cvp) && !cvp.IsReadOnly.ValueOrDefault)
            {
                cvp.SetValue(value.ToString("G"));
                return;
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
            // Fallback: WinForms NumericUpDown — read value from child Edit element
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

        // ── Toggle pattern ────────────────────────────────────────────────

        public void ToggleCheckBox(AutomationElement el)
        {
            if (el.Patterns.Toggle.TryGetPattern(out var p)) p.Toggle();
            else el.AsCheckBox().Toggle();
        }

        public string GetToggleState(AutomationElement el)
        {
            if (el.Patterns.Toggle.TryGetPattern(out var p))
                return p.ToggleState.ValueOrDefault.ToString();
            return "Toggle pattern not supported";
        }

        /// <summary>Sets a toggle element to a specific on (true) or off (false) state.</summary>
        public void SetToggleState(AutomationElement el, bool targetOn)
        {
            if (!el.Patterns.Toggle.TryGetPattern(out var p))
                throw new InvalidOperationException("Toggle pattern not supported");
            var state = p.ToggleState.ValueOrDefault;
            bool isOn = state == ToggleState.On;
            if (isOn != targetOn) p.Toggle();
            // Handle 3-state: if we toggled to Indeterminate, toggle once more
            if (p.ToggleState.ValueOrDefault == ToggleState.Indeterminate) p.Toggle();
        }

        public bool? IsCheckBoxChecked(AutomationElement el) =>
            el.AsCheckBox().IsChecked;

        // ── ExpandCollapse pattern ────────────────────────────────────────

        public string GetExpandCollapseState(AutomationElement el)
        {
            if (el.Patterns.ExpandCollapse.TryGetPattern(out var p))
                return p.ExpandCollapseState.ValueOrDefault.ToString();
            return "ExpandCollapse pattern not supported";
        }

        // ── TextBox / PasswordBox ─────────────────────────────────────────

        public void EnterText(AutomationElement el, string text)
        {
            // Keyboard.Type can leave Shift latched after shifted characters (e.g. &, $).
            // Clipboard paste avoids keyboard simulation entirely and handles all characters correctly.
            Exception? clipEx = null;
            var sta = new Thread(() =>
            {
                try { System.Windows.Forms.Clipboard.SetText(text, System.Windows.Forms.TextDataFormat.UnicodeText); }
                catch (Exception e) { clipEx = e; }
            });
            sta.SetApartmentState(ApartmentState.STA);
            sta.Start();
            sta.Join();
            if (clipEx != null)
            {
                // Fallback: original keyboard typing
                el.AsTextBox().Enter(text);
                return;
            }
            el.Focus();
            Thread.Sleep(FocusDelayMs);
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_V);
            Thread.Sleep(50);
        }

        public void SelectAllText(AutomationElement el)
        {
            el.Focus();
            Thread.Sleep(FocusDelayMs);
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
        }

        public void CopyText(AutomationElement el)
        {
            el.Focus();
            Thread.Sleep(FocusDelayMs);
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_C);
        }

        public void CutText(AutomationElement el)
        {
            el.Focus();
            Thread.Sleep(FocusDelayMs);
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_X);
        }

        public void PasteText(AutomationElement el)
        {
            el.Focus();
            Thread.Sleep(FocusDelayMs);
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_V);
        }

        public void UndoText(AutomationElement el)
        {
            el.Focus();
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z);
        }

        public void ClearText(AutomationElement el)
        {
            SelectAllText(el);
            Keyboard.Press(VirtualKeyShort.DELETE);
            Keyboard.Release(VirtualKeyShort.DELETE);
        }

        public void InsertTextAtCaret(AutomationElement el, string text)
        {
            el.Focus();
            Keyboard.Type(text);
        }

        // ── Keyboard ──────────────────────────────────────────────────────

        public void SendKey(VirtualKeyShort key) =>
            Keyboard.Press(key);

        public void SendKeys(string text) =>
            Keyboard.Type(text);

        public void SendShortcut(VirtualKeyShort modifier, VirtualKeyShort key) =>
            Keyboard.TypeSimultaneously(modifier, key);

        /// <summary>
        /// Focuses the element then sends keys with full notation support:
        ///   • {KEY} tokens: {CTRL}, {ALT}, {SHIFT}, {ENTER}, {TAB}, {DELETE}, {F5}, etc.
        ///   • Modifier combos: "Ctrl+A", "Alt+F4", "Shift+Tab"
        ///   • Single key name: "Enter", "Escape", "Tab", "F5"
        ///   • Literal text: anything else is typed character by character
        /// </summary>
        public void SendKeysEnhanced(AutomationElement el, string keys)
        {
            el.Focus();
            Thread.Sleep(FocusDelayMs);

            // Handle "Modifier+{KEY}" mixed notation — convert to "{MODIFIER}{KEY}" for SendBraceKeys
            if (keys.Contains('+') && keys.Contains('{'))
            {
                int plusIdx  = keys.IndexOf('+');
                int braceIdx = keys.IndexOf('{');
                if (plusIdx < braceIdx)
                {
                    string modWord = keys[..plusIdx].Trim();
                    string rest    = keys[(plusIdx + 1)..].Trim();
                    var modVk = ParseVirtualKey(modWord);
                    if (modVk != null && rest.StartsWith('{'))
                    {
                        SendBraceKeys("{" + modWord.ToUpper() + "}" + rest);
                        return;
                    }
                }
            }

            if (keys.Contains('{'))
            {
                SendBraceKeys(keys);
                return;
            }

            if (keys.Contains('+'))
            {
                var parts    = keys.Split('+', 2);
                var modifier = ParseVirtualKey(parts[0].Trim());
                var key      = ParseVirtualKey(parts[1].Trim());
                if (modifier != null && key != null)
                {
                    Keyboard.TypeSimultaneously(modifier.Value, key.Value);
                    return;
                }
            }

            var vk = ParseVirtualKey(keys);
            if (vk != null)
            {
                Keyboard.Press(vk.Value);
                Keyboard.Release(vk.Value);
            }
            else
            {
                Keyboard.Type(keys);
            }
        }

        private static void SendBraceKeys(string keys)
        {
            int i = 0;
            VirtualKeyShort? heldModifier = null;
            while (i < keys.Length)
            {
                if (keys[i] == '{')
                {
                    int end = keys.IndexOf('}', i + 1);
                    if (end < 0) break;
                    string name = keys.Substring(i + 1, end - i - 1);
                    var vk = ParseVirtualKey(name);
                    if (vk.HasValue)
                    {
                        bool isMod = vk.Value is VirtualKeyShort.CONTROL or VirtualKeyShort.ALT or VirtualKeyShort.SHIFT;
                        if (isMod)
                            heldModifier = vk.Value;
                        else if (heldModifier.HasValue)
                        {
                            Keyboard.TypeSimultaneously(heldModifier.Value, vk.Value);
                            heldModifier = null;
                        }
                        else
                        {
                            Keyboard.Press(vk.Value);
                            Keyboard.Release(vk.Value);
                        }
                    }
                    i = end + 1;
                }
                else
                {
                    if (heldModifier.HasValue)
                    {
                        var charVk = ParseVirtualKey(keys[i].ToString());
                        if (charVk.HasValue)
                            Keyboard.TypeSimultaneously(heldModifier.Value, charVk.Value);
                        else
                            Keyboard.Type(keys[i].ToString());
                        heldModifier = null;
                    }
                    else
                        Keyboard.Type(keys[i].ToString());
                    i++;
                }
            }
        }

        private static VirtualKeyShort? ParseVirtualKey(string name) =>
            name.ToLowerInvariant() switch
            {
                "enter" or "return"   => VirtualKeyShort.RETURN,
                "tab"                 => VirtualKeyShort.TAB,
                "escape" or "esc"     => VirtualKeyShort.ESCAPE,
                "backspace" or "back" => VirtualKeyShort.BACK,
                "delete" or "del"     => VirtualKeyShort.DELETE,
                "space"               => VirtualKeyShort.SPACE,
                "up"                  => VirtualKeyShort.UP,
                "down"                => VirtualKeyShort.DOWN,
                "left"                => VirtualKeyShort.LEFT,
                "right"               => VirtualKeyShort.RIGHT,
                "home"                => VirtualKeyShort.HOME,
                "end"                 => VirtualKeyShort.END,
                "pageup"              => VirtualKeyShort.PRIOR,
                "pagedown"            => VirtualKeyShort.NEXT,
                "insert"              => VirtualKeyShort.INSERT,
                "ctrl" or "control"   => VirtualKeyShort.CONTROL,
                "alt"                 => VirtualKeyShort.ALT,
                "shift"               => VirtualKeyShort.SHIFT,
                "f1"  => VirtualKeyShort.F1,  "f2"  => VirtualKeyShort.F2,
                "f3"  => VirtualKeyShort.F3,  "f4"  => VirtualKeyShort.F4,
                "f5"  => VirtualKeyShort.F5,  "f6"  => VirtualKeyShort.F6,
                "f7"  => VirtualKeyShort.F7,  "f8"  => VirtualKeyShort.F8,
                "f9"  => VirtualKeyShort.F9,  "f10" => VirtualKeyShort.F10,
                "f11" => VirtualKeyShort.F11, "f12" => VirtualKeyShort.F12,
                _ => name.Length == 1 && char.IsLetterOrDigit(name[0])
                     ? (VirtualKeyShort?)((VirtualKeyShort)char.ToUpper(name[0]))
                     : null
            };

        // ── Mouse ─────────────────────────────────────────────────────────

        public void ScrollUp(int amount = 3)   => Mouse.Scroll(amount);
        public void ScrollDown(int amount = 3) => Mouse.Scroll(-amount);
        public void HorizontalScroll(int amount) => Mouse.HorizontalScroll(amount);

        public void DragAndDrop(AutomationElement source, AutomationElement target)
        {
            var from = source.GetClickablePoint();
            var to   = target.GetClickablePoint();
            Mouse.Drag(from, to);
        }

        public void DragAndDropToPoint(AutomationElement source, int x, int y)
        {
            var from = source.GetClickablePoint();
            Mouse.Drag(from, new System.Drawing.Point(x, y));
        }

        // ── Scroll pattern ────────────────────────────────────────────────

        public void ScrollIntoView(AutomationElement el) =>
            el.Patterns.ScrollItem.Pattern.ScrollIntoView();

        public void ScrollElement(AutomationElement el,
            ScrollAmount vertical, ScrollAmount horizontal = ScrollAmount.NoAmount) =>
            el.Patterns.Scroll.Pattern.Scroll(horizontal, vertical);

        /// <summary>Scrolls an element to a horizontal and vertical percent (0–100).</summary>
        public void ScrollByPercent(AutomationElement el, double hPercent, double vPercent)
        {
            if (!el.Patterns.Scroll.TryGetPattern(out var p))
                throw new InvalidOperationException("Scroll pattern not supported");
            p.SetScrollPercent(hPercent, vPercent);
        }

        public string GetScrollInfo(AutomationElement el)
        {
            if (!el.Patterns.Scroll.TryGetPattern(out var p))
                return "Scroll pattern not supported";
            return $"HorizontalPercent={p.HorizontalScrollPercent.ValueOrDefault:G}  " +
                   $"VerticalPercent={p.VerticalScrollPercent.ValueOrDefault:G}  " +
                   $"HScrollable={p.HorizontallyScrollable.ValueOrDefault}  " +
                   $"VScrollable={p.VerticallyScrollable.ValueOrDefault}";
        }

        // ── Transform pattern ─────────────────────────────────────────────

        /// <summary>Moves an element using the Transform pattern.</summary>
        public void MoveElement(AutomationElement el, double x, double y)
        {
            if (!el.Patterns.Transform.TryGetPattern(out var p))
                throw new InvalidOperationException("Transform pattern not supported");
            if (!p.CanMove.ValueOrDefault) throw new InvalidOperationException("Element cannot be moved");
            p.Move(x, y);
        }

        /// <summary>Resizes an element using the Transform pattern.</summary>
        public void ResizeElement(AutomationElement el, double width, double height)
        {
            if (!el.Patterns.Transform.TryGetPattern(out var p))
                throw new InvalidOperationException("Transform pattern not supported");
            if (!p.CanResize.ValueOrDefault) throw new InvalidOperationException("Element cannot be resized");
            p.Resize(width, height);
        }

        // ── Highlight ─────────────────────────────────────────────────────

        public void HighlightElement(AutomationElement el)
        {
            _ = Task.Run(() =>
            {
                try { el.DrawHighlight(false, System.Drawing.Color.Orange, TimeSpan.FromSeconds(1)); }
                catch { }
            });
        }

        // ── ComboBox / ListBox (multi-strategy) ───────────────────────────

        /// <summary>
        /// Selects by text: tries SelectionItem on list children → ComboBox.Select → ListBox.Select.
        /// </summary>
        public void SelectComboBoxItem(AutomationElement el, string text)
        {
            // 1. If the element IS a List, select the named child item directly via SelectionItem
            var ownType = el.Properties.ControlType.ValueOrDefault;
            if (ownType == ControlType.List)
            {
                var items = el.FindAllChildren(cf => cf.ByControlType(ControlType.ListItem));
                var match = items.FirstOrDefault(i => i.Properties.Name.ValueOrDefault == text);
                if (match != null)
                {
                    if (match.Patterns.SelectionItem.TryGetPattern(out var sp)) { sp.Select(); return; }
                    match.Click();
                    return;
                }
                throw new InvalidOperationException($"Item '{text}' not found in List");
            }

            // 2. Try SelectionItem on matching list child (ComboBox with embedded List)
            var listChild = el.FindFirstDescendant(cf => cf.ByControlType(ControlType.List));
            if (listChild != null)
            {
                var items = listChild.FindAllChildren(cf => cf.ByControlType(ControlType.ListItem));
                var match = items.FirstOrDefault(i => i.Properties.Name.ValueOrDefault == text);
                if (match != null && match.Patterns.SelectionItem.TryGetPattern(out var sp))
                {
                    // Ensure dropdown is open for select to stick
                    var btn = el.FindFirstChild(cf => cf.ByControlType(ControlType.Button));
                    if (btn != null) btn.Click(); else el.Click();
                    Thread.Sleep(150);
                    sp.Select();
                    Thread.Sleep(50);
                    Keyboard.Press(VirtualKeyShort.RETURN);
                    Keyboard.Release(VirtualKeyShort.RETURN);
                    return;
                }
            }

            // 2. Try FlaUI ComboBox wrapper
            var combo = el.AsComboBox();
            if (combo != null)
            {
                combo.Select(text);
                return;
            }

            // 3. Try FlaUI ListBox wrapper
            var listBox = el.AsListBox();
            if (listBox != null)
            {
                listBox.Select(text);
                return;
            }

            throw new InvalidOperationException($"Could not select '{text}': element is not a ComboBox or ListBox");
        }

        /// <summary>Selects a ComboBox or ListBox item by zero-based index.</summary>
        public void SelectByIndex(AutomationElement el, int index)
        {
            // Handle plain List element (e.g. WinForms ListBox / multi-select ListBox)
            if (el.Properties.ControlType.ValueOrDefault == ControlType.List)
            {
                var listItems = el.FindAllChildren(cf => cf.ByControlType(ControlType.ListItem));
                if (index < listItems.Length)
                {
                    if (listItems[index].Patterns.SelectionItem.TryGetPattern(out var sp)) sp.Select();
                    else listItems[index].Click();
                    return;
                }
                throw new InvalidOperationException($"Index {index} out of range");
            }

            var combo = el.AsComboBox();
            if (combo != null)
            {
                // Expand manually to populate children — avoids FlaUI's Items getter which
                // calls Expand() internally and throws NullReferenceException on WinForms combos.
                try
                {
                    if (el.Patterns.ExpandCollapse.TryGetPattern(out var ecp)) ecp.Expand();
                }
                catch { }
                Thread.Sleep(300);

                var children = el.FindAllChildren(cf => cf.ByControlType(ControlType.ListItem));
                if (children.Length == 0)
                {
                    // WinForms DropDown: children appear under a List child
                    foreach (var child in el.FindAllChildren())
                    {
                        if (child.Properties.ControlType.ValueOrDefault == ControlType.List)
                        {
                            children = child.FindAllChildren(cf => cf.ByControlType(ControlType.ListItem));
                            break;
                        }
                    }
                }

                if (index < children.Length)
                {
                    if (children[index].Patterns.SelectionItem.TryGetPattern(out var sp)) sp.Select();
                    else children[index].Click();
                    try { if (el.Patterns.ExpandCollapse.TryGetPattern(out var ecp2)) ecp2.Collapse(); } catch { }
                    return;
                }
                try { if (el.Patterns.ExpandCollapse.TryGetPattern(out var ecp3)) ecp3.Collapse(); } catch { }
                throw new InvalidOperationException($"Index {index} out of range (found {children.Length} items)");
            }
            var listBox = el.AsListBox();
            if (listBox != null)
            {
                if (index < listBox.Items.Length) { listBox.Items[index].Select(); return; }
                throw new InvalidOperationException($"Index {index} out of range");
            }
            throw new InvalidOperationException("Element is not a ComboBox or ListBox");
        }

        public string GetComboBoxSelected(AutomationElement el)
        {
            // Value pattern is the most reliable for WinForms combos — check it first
            // to avoid triggering FlaUI's ComboBox.Expand() which throws NullReferenceException
            // when the ExpandCollapse pattern is absent.
            if (el.Patterns.Value.TryGetPattern(out var vpEarly))
            {
                var v = vpEarly.Value.ValueOrDefault;
                if (!string.IsNullOrEmpty(v)) return v;
            }
            var combo = el.AsComboBox();
            if (combo != null)
            {
                try
                {
                    string? sel = combo.SelectedItem?.Text;
                    if (sel != null) return sel;
                }
                catch { /* SelectionItem pattern not supported — fall through */ }
            }
            var listBox = el.AsListBox();
            if (listBox != null)
            {
                try
                {
                    var sel = listBox.SelectedItems;
                    if (sel.Length > 0) return sel[0].Text;
                }
                catch { /* fall through */ }
            }
            return "(none)";
        }

        public string[] GetComboBoxItems(AutomationElement el)
        {
            // Fast path for plain List elements (e.g. WinForms ListBox / multi-select ListBox)
            // whose ControlType is List rather than ComboBox — scan children directly.
            var ct = el.Properties.ControlType.ValueOrDefault;
            if (ct == FlaUI.Core.Definitions.ControlType.List)
            {
                try
                {
                    return el.FindAllChildren()
                        .Where(c => c.Properties.ControlType.ValueOrDefault == FlaUI.Core.Definitions.ControlType.ListItem)
                        .Select(c => c.Properties.Name.ValueOrDefault ?? "")
                        .Where(s => !string.IsNullOrEmpty(s))
                        .ToArray();
                }
                catch { return Array.Empty<string>(); }
            }

            var combo = el.AsComboBox();
            if (combo != null)
            {
                // Always expand to ensure children are visible in UIA tree;
                // track state to know whether to collapse after.
                bool wasCollapsed = true;
                try { wasCollapsed = combo.ExpandCollapseState == ExpandCollapseState.Collapsed; }
                catch { }
                // Use the raw UIA pattern directly to avoid FlaUI wrapper quirks
                try
                {
                    if (el.Patterns.ExpandCollapse.TryGetPattern(out var ecp))
                        ecp.Expand();
                }
                catch { }
                Thread.Sleep(500); // give UIA time to populate children after expand

                // Strategy 1: SelectionItem children (direct child scan — avoids FlaUI's
                // ComboBox.Items getter which calls Expand() internally and throws
                // NullReferenceException on WinForms combos that lack the pattern).
                string[] items = Array.Empty<string>();
                try
                {
                    items = el.FindAllChildren()
                        .Where(c => c.Properties.ControlType.ValueOrDefault == FlaUI.Core.Definitions.ControlType.ListItem)
                        .Select(c => c.Properties.Name.ValueOrDefault ?? "")
                        .Where(s => !string.IsNullOrEmpty(s))
                        .ToArray();
                }
                catch { }

                // Strategy 2: Scan direct children for a List container
                // (WinForms ComboBox exposes a List child when expanded)
                if (items.Length == 0)
                {
                    try
                    {
                        foreach (var child in el.FindAllChildren())
                        {
                            if (child.Properties.ControlType.ValueOrDefault == FlaUI.Core.Definitions.ControlType.List)
                            {
                                var listItems = child.FindAllChildren();
                                items = listItems
                                    .Select(i => i.Properties.Name.ValueOrDefault ?? "")
                                    .Where(s => !string.IsNullOrEmpty(s))
                                    .ToArray();
                                break;
                            }
                        }
                    }
                    catch { }
                }

                if (wasCollapsed) try { if (el.Patterns.ExpandCollapse.TryGetPattern(out var ecpC)) ecpC.Collapse(); } catch { }
                return items;
            }

            var listBox = el.AsListBox();
            if (listBox != null)
            {
                string[] lbItems = Array.Empty<string>();
                try { lbItems = listBox.Items.Select(i => i.Text).ToArray(); } catch { }
                if (lbItems.Length == 0)
                {
                    // Fallback: scan children directly (handles WinForms ListBox)
                    try
                    {
                        lbItems = el.FindAllChildren()
                            .Where(c => c.Properties.ControlType.ValueOrDefault == FlaUI.Core.Definitions.ControlType.ListItem)
                            .Select(c => c.Properties.Name.ValueOrDefault ?? "")
                            .Where(s => !string.IsNullOrEmpty(s))
                            .ToArray();
                    }
                    catch { }
                }
                return lbItems;
            }

            // Fallback: return current value as single-item list (e.g. WinForms ComboBox
            // that exposes only Value+ExpandCollapse, not Selection/SelectionItem)
            if (el.Patterns.Value.TryGetPattern(out var vp))
            {
                string v = vp.Value.ValueOrDefault ?? "";
                return string.IsNullOrEmpty(v) ? Array.Empty<string>() : new[] { v };
            }
            return Array.Empty<string>();
        }

        public void ExpandComboBox(AutomationElement el)
        {
            if (el.Patterns.ExpandCollapse.TryGetPattern(out var p)) p.Expand();
        }
        public void CollapseComboBox(AutomationElement el)
        {
            if (el.Patterns.ExpandCollapse.TryGetPattern(out var p)) p.Collapse();
        }

        // ── CheckBox / RadioButton ────────────────────────────────────────

        public void SelectRadioButton(AutomationElement el) => el.Click();

        public bool IsRadioButtonSelected(AutomationElement el) =>
            el.AsRadioButton().IsChecked;

        // ── ListBox ───────────────────────────────────────────────────────

        public void SelectListBoxByIndex(AutomationElement el, int index) =>
            el.AsListBox().Select(index);

        public void SelectListBoxByText(AutomationElement el, string text) =>
            el.AsListBox().Select(text);

        public string GetListBoxSelected(AutomationElement el) =>
            el.AsListBox().SelectedItem?.Text ?? "(none)";

        public string[] GetListBoxItems(AutomationElement el) =>
            el.AsListBox().Items.Select(i => i.Text).ToArray();

        // ── ListView / DataGrid ───────────────────────────────────────────

        public string GetGridCell(AutomationElement el, int row, int col) =>
            el.AsGrid().Rows[row].Cells[col].Value;

        public int GetGridRowCount(AutomationElement el) =>
            el.AsGrid().RowCount;

        public int GetGridColumnCount(AutomationElement el) =>
            el.AsGrid().ColumnCount;

        public void SelectGridRow(AutomationElement el, int row) =>
            el.AsGrid().Rows[row].Select();

        public string GetGridRowValues(AutomationElement el, int row)
        {
            var cells = el.AsGrid().Rows[row].Cells;
            return string.Join(" | ", cells.Select(c => c.Value));
        }

        /// <summary>Gets an item from a Grid pattern element at (row, col).</summary>
        public string GetGridItem(AutomationElement el, int row, int col)
        {
            if (!el.Patterns.Grid.TryGetPattern(out var p))
                return "Grid pattern not supported";
            if (row < 0 || row >= p.RowCount) return $"Row {row} out of range (0–{p.RowCount - 1})";
            if (col < 0 || col >= p.ColumnCount) return $"Column {col} out of range (0–{p.ColumnCount - 1})";
            var item = p.GetItem(row, col);
            return Describe(item);
        }

        public string GetGridInfo(AutomationElement el)
        {
            if (!el.Patterns.Grid.TryGetPattern(out var p)) return "Grid pattern not supported";
            return $"Rows={p.RowCount}  Columns={p.ColumnCount}";
        }

        public string GetGridItemInfo(AutomationElement el)
        {
            if (!el.Patterns.GridItem.TryGetPattern(out var p)) return "GridItem pattern not supported";
            return $"Row={p.Row}  Column={p.Column}  RowSpan={p.RowSpan}  ColumnSpan={p.ColumnSpan}";
        }

        // ── TreeView ──────────────────────────────────────────────────────

        public void ExpandTreeNode(AutomationElement el, int index) =>
            el.AsTree().Items[index].Expand();

        public void CollapseTreeNode(AutomationElement el, int index) =>
            el.AsTree().Items[index].Collapse();

        public void SelectTreeNode(AutomationElement el, int index) =>
            el.AsTree().Items[index].Select();

        public string GetTreeNodeText(AutomationElement el, int index) =>
            el.AsTree().Items[index].Text;

        public int GetTreeNodeCount(AutomationElement el) =>
            el.AsTree().Items.Length;

        // ── Menu / MenuItem ───────────────────────────────────────────────

        public void InvokeMenuItem(AutomationElement el) => el.AsMenuItem().Invoke();
        public void ExpandMenuItem(AutomationElement el) => el.AsMenuItem().Expand();
        public void OpenContextMenu(AutomationElement el) => el.RightClick();

        // ── TabControl ────────────────────────────────────────────────────

        public void SelectTab(AutomationElement el, int index)  => el.AsTab().SelectTabItem(index);
        public void SelectTabByName(AutomationElement el, string name) => el.AsTab().SelectTabItem(name);
        public string GetSelectedTabName(AutomationElement el) => el.AsTab().SelectedTabItem.Name;
        public int GetTabCount(AutomationElement el) => el.AsTab().TabItems.Length;

        // ── Slider / Spinner (RangeValue) ─────────────────────────────────

        public void SetSliderValue(AutomationElement el, double value) =>
            el.AsSlider().Value = value;

        public double GetSliderValue(AutomationElement el) =>
            el.AsSlider().Value;

        public double GetRangeMin(AutomationElement el) => el.Patterns.RangeValue.Pattern.Minimum;
        public double GetRangeMax(AutomationElement el) => el.Patterns.RangeValue.Pattern.Maximum;
        public double GetSmallChange(AutomationElement el) => el.Patterns.RangeValue.Pattern.SmallChange;
        public double GetLargeChange(AutomationElement el) => el.Patterns.RangeValue.Pattern.LargeChange;

        // ── ProgressBar ───────────────────────────────────────────────────

        public double GetProgressBarValue(AutomationElement el) => el.AsProgressBar().Value;

        // ── Label ─────────────────────────────────────────────────────────

        public string GetLabelText(AutomationElement el) => el.AsLabel().Text;

        // ── Screenshot ────────────────────────────────────────────────────

        public string CaptureElement(AutomationElement el, string folder)
        {
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, $"capture_{DateTime.Now:yyyyMMdd_HHmmss}.png");
            using var img = Capture.Element(el);
            img.ToFile(path);
            return path;
        }

        public string CaptureScreen(string folder)
        {
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, $"screen_{DateTime.Now:yyyyMMdd_HHmmss}.png");
            using var img = Capture.Screen();
            img.ToFile(path);
            return path;
        }

        // ── Capture to base64 ─────────────────────────────────────────────

        public string CaptureElementToBase64(AutomationElement el)
        {
            using var img = Capture.Element(el);
            return BitmapToBase64(img.Bitmap);
        }

        public string CaptureScreenToBase64()
        {
            using var img = Capture.Screen();
            return BitmapToBase64(img.Bitmap);
        }

        public string StitchElementsToBase64(IList<AutomationElement> elements)
        {
            var captures = elements.Select(e => Capture.Element(e)).ToList();
            try
            {
                const int gap = 4;
                int width  = captures.Max(c => c.Bitmap.Width);
                int height = captures.Sum(c => c.Bitmap.Height) + gap * (captures.Count - 1);

                using var canvas = new System.Drawing.Bitmap(width, Math.Max(height, 1));
                using var g      = System.Drawing.Graphics.FromImage(canvas);
                g.Clear(System.Drawing.Color.FromArgb(40, 40, 40));

                int y = 0;
                foreach (var cap in captures)
                {
                    g.DrawImage(cap.Bitmap, 0, y);
                    y += cap.Bitmap.Height + gap;
                }
                return BitmapToBase64(canvas);
            }
            finally { foreach (var c in captures) c.Dispose(); }
        }

        private static string BitmapToBase64(System.Drawing.Bitmap bmp)
        {
            using var ms = new System.IO.MemoryStream();
            bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            return Convert.ToBase64String(ms.ToArray());
        }

        public void Dispose() => _automation.Dispose();
    }
}
