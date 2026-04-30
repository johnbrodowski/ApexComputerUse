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
        // -- ComboBox / ListBox (multi-strategy) ---------------------------

        /// <summary>
        /// Selects by text: tries SelectionItem on list children -> ComboBox.Select -> ListBox.Select.
        /// </summary>
        public void SelectComboBoxItem(AutomationElement el, string text)
        {
            var ownType = el.Properties.ControlType.ValueOrDefault;
            if (ownType == ControlType.Tab)
            {
                SelectTabByName(el, text);
                return;
            }

            // 1. If the element IS a List, select the named child item directly via SelectionItem
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
            if (el.Properties.ControlType.ValueOrDefault == ControlType.Tab)
            {
                SelectTab(el, index);
                return;
            }

            // Handle plain List element (e.g. WinForms ListBox / multi-select ListBox).
            // Use Descendants so <optgroup>-wrapped <option>s (which expose as Group > ListItem in UIA) are reachable.
            if (el.Properties.ControlType.ValueOrDefault == ControlType.List)
            {
                var listItems = el.FindAllChildren(cf => cf.ByControlType(ControlType.ListItem));
                if (listItems.Length == 0)
                    listItems = el.FindAllDescendants(cf => cf.ByControlType(ControlType.ListItem));
                if (listItems.Length == 0)
                    listItems = el.FindAllChildren(cf => cf.ByControlType(ControlType.CheckBox));
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
                // Expand manually to populate children - avoids FlaUI's Items getter which
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
            // Value pattern is the most reliable for WinForms combos - check it first
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
                catch { /* SelectionItem pattern not supported - fall through */ }
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
            // whose ControlType is List rather than ComboBox - scan children directly.
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

                // Strategy 1: SelectionItem children (direct child scan - avoids FlaUI's
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

        // -- CheckBox / RadioButton ----------------------------------------

        public void SelectRadioButton(AutomationElement el) => el.Click();

        public bool IsRadioButtonSelected(AutomationElement el) =>
            el.AsRadioButton().IsChecked;

        // -- ListBox -------------------------------------------------------

        public void SelectListBoxByIndex(AutomationElement el, int index) =>
            el.AsListBox().Select(index);

        public void SelectListBoxByText(AutomationElement el, string text) =>
            el.AsListBox().Select(text);

        public string GetListBoxSelected(AutomationElement el) =>
            el.AsListBox().SelectedItem?.Text ?? "(none)";

        public string[] GetListBoxItems(AutomationElement el) =>
            el.AsListBox().Items.Select(i => i.Text).ToArray();

        // -- ListView / DataGrid -------------------------------------------

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
            if (row < 0 || row >= p.RowCount) return $"Row {row} out of range (0-{p.RowCount - 1})";
            if (col < 0 || col >= p.ColumnCount) return $"Column {col} out of range (0-{p.ColumnCount - 1})";
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

        // -- TreeView ------------------------------------------------------

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

        // -- Menu / MenuItem -----------------------------------------------

        public void InvokeMenuItem(AutomationElement el) => el.AsMenuItem().Invoke();
        public void ExpandMenuItem(AutomationElement el) => el.AsMenuItem().Expand();
        public void OpenContextMenu(AutomationElement el) => el.RightClick();

        // -- TabControl ----------------------------------------------------

        public void SelectTab(AutomationElement el, int index)  => el.AsTab().SelectTabItem(index);
        public void SelectTabByName(AutomationElement el, string name) => el.AsTab().SelectTabItem(name);
        public string GetSelectedTabName(AutomationElement el) => el.AsTab().SelectedTabItem?.Name ?? string.Empty;
        public int GetTabCount(AutomationElement el) => el.AsTab().TabItems.Length;

        // -- Slider / Spinner (RangeValue) ---------------------------------

        public void SetSliderValue(AutomationElement el, double value) =>
            el.AsSlider().Value = value;

        public double GetSliderValue(AutomationElement el) =>
            el.AsSlider().Value;

        public double GetRangeMin(AutomationElement el) => el.Patterns.RangeValue.Pattern.Minimum;
        public double GetRangeMax(AutomationElement el) => el.Patterns.RangeValue.Pattern.Maximum;
        public double GetSmallChange(AutomationElement el) => el.Patterns.RangeValue.Pattern.SmallChange;
        public double GetLargeChange(AutomationElement el) => el.Patterns.RangeValue.Pattern.LargeChange;

        // -- ProgressBar ---------------------------------------------------

        public double GetProgressBarValue(AutomationElement el) => el.AsProgressBar().Value;

        // -- Label ---------------------------------------------------------

        public string GetLabelText(AutomationElement el) => el.AsLabel().Text;

    }
}

