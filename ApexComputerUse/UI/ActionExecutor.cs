using FlaUI.Core.AutomationElements;
using FlaUI.Core.WindowsAPI;

namespace ApexComputerUse
{
    internal sealed class ActionExecutor : IDisposable
    {
        private readonly ApexHelper _helper;
        private readonly CommandProcessor _processor;
        private OcrHelper? _ocr;

        internal ActionExecutor(ApexHelper helper, CommandProcessor processor)
        {
            _helper = helper;
            _processor = processor;
        }

        internal string Execute(AutomationElement el, string action, string input)
        {
            return action switch
            {
                // -- Info ----------------------------------------------
                "Describe" => _helper.Describe(el),
                "Get Bounding Rect" => _helper.GetBoundingRect(el),
                "Get Supported Patterns" => _helper.GetSupportedPatterns(el),
                "Get Focused Element" => _helper.GetFocusedElement(),

                // -- Window --------------------------------------------
                "Minimize" => Do(() => _helper.MinimizeWindow(el.AsWindow())),
                "Maximize" => Do(() => _helper.MaximizeWindow(el.AsWindow())),
                "Restore" => Do(() => _helper.RestoreWindow(el.AsWindow())),
                "Close" => Do(() => _helper.CloseWindow(el.AsWindow())),
                "Move (x,y)" => Do(() => { var p = ParsePair(input); _helper.MoveWindow(el.AsWindow(), (int)p.x, (int)p.y); }),
                "Resize (w,h)" => Do(() => { var p = ParsePair(input); _helper.ResizeWindow(el.AsWindow(), p.x, p.y); }),

                // -- Mouse ---------------------------------------------
                "Click" => Do(() => _helper.ClickElement(el)),
                "Right-Click" => Do(() => _helper.RightClickElement(el)),
                "Double-Click" => Do(() => _helper.DoubleClickElement(el)),
                "Hover" => Do(() => _helper.HoverElement(el)),
                "Set Focus" => Do(() => _helper.SetFocus(el)),

                // -- Invoke / patterns ---------------------------------
                "Invoke" => Do(() => _helper.InvokeButton(el)),
                "Invoke (pattern)" => Do(() => _helper.InvokePattern(el)),
                "Expand" => Do(() => el.Patterns.ExpandCollapse.Pattern.Expand()),
                "Collapse" => Do(() => el.Patterns.ExpandCollapse.Pattern.Collapse()),
                "Open Context Menu" => Do(() => _helper.OpenContextMenu(el)),

                // -- Text ----------------------------------------------
                "Get Text" => _helper.GetText(el),
                "Enter Text" => Do(() => _helper.EnterText(el, input)),
                "Insert at Caret" => Do(() => _helper.InsertTextAtCaret(el, input)),
                "Get Value (pattern)" => _helper.GetValue(el),
                "Set Value (pattern)" => Do(() => _helper.SetValue(el, input)),
                "Select All" => Do(() => _helper.SelectAllText(el)),
                "Copy" => Do(() => _helper.CopyText(el)),
                "Cut" => Do(() => _helper.CutText(el)),
                "Paste" => Do(() => _helper.PasteText(el)),
                "Undo" => Do(() => _helper.UndoText(el)),
                "Clear" => Do(() => _helper.ClearText(el)),

                // -- Keyboard ------------------------------------------
                "Send Keys" => Do(() => _helper.SendKeys(input)),
                "Send Key" => Do(() => _helper.SendKey(ParseVKey(input))),

                // -- ComboBox ------------------------------------------
                "Get Selected" => _helper.GetComboBoxSelected(el),
                "Get All Items" => string.Join(", ", _helper.GetComboBoxItems(el)),
                "Select Item (text)" => Do(() => _helper.SelectComboBoxItem(el, input)),
                "Expand Dropdown" => Do(() => _helper.ExpandComboBox(el)),
                "Collapse Dropdown" => Do(() => _helper.CollapseComboBox(el)),

                // -- CheckBox -----------------------------------------
                "Get State" => _helper.IsCheckBoxChecked(el)?.ToString() ?? "indeterminate",
                "Toggle" => Do(() => _helper.ToggleCheckBox(el)),

                // -- RadioButton ---------------------------------------
                "Is Selected" => _helper.IsRadioButtonSelected(el).ToString(),
                "Select" => Do(() => _helper.SelectRadioButton(el)),

                // -- ListBox -------------------------------------------
                "Select by Text" => Do(() => _helper.SelectListBoxByText(el, input)),
                "Select by Index" => Do(() => _helper.SelectListBoxByIndex(el, ParseInt(input))),

                // -- Grid / ListView / DataGrid ------------------------
                "Get Row Count" => _helper.GetGridRowCount(el).ToString(),
                "Get Column Count" => _helper.GetGridColumnCount(el).ToString(),
                "Get Cell (row,col)" => GetCell(el, input),
                "Get Row Values (row)" => _helper.GetGridRowValues(el, ParseInt(input)),
                "Select Row (index)" => Do(() => _helper.SelectGridRow(el, ParseInt(input))),

                // -- TreeView ------------------------------------------
                "Get Node Count" => _helper.GetTreeNodeCount(el).ToString(),
                "Get Node Text (index)" => _helper.GetTreeNodeText(el, ParseInt(input)),
                "Expand Node (index)" => Do(() => _helper.ExpandTreeNode(el, ParseInt(input))),
                "Collapse Node (index)" => Do(() => _helper.CollapseTreeNode(el, ParseInt(input))),
                "Select Node (index)" => Do(() => _helper.SelectTreeNode(el, ParseInt(input))),

                // -- TabControl ----------------------------------------
                "Get Selected Tab" => _helper.GetSelectedTabName(el),
                "Get Tab Count" => _helper.GetTabCount(el).ToString(),
                "Select Tab (index)" => Do(() => _helper.SelectTab(el, ParseInt(input))),
                "Select Tab (name)" => Do(() => _helper.SelectTabByName(el, input)),

                // -- Slider / ProgressBar / RangeValue -----------------
                "Get Slider Value" => _helper.GetSliderValue(el).ToString("F2"),
                "Get Progress Value" => _helper.GetProgressBarValue(el).ToString("F2"),
                "Set Slider Value" => Do(() => _helper.SetSliderValue(el, double.Parse(input))),
                "Get Min" => _helper.GetRangeMin(el).ToString("F2"),
                "Get Max" => _helper.GetRangeMax(el).ToString("F2"),
                "Get Small Change" => _helper.GetSmallChange(el).ToString("F2"),
                "Get Large Change" => _helper.GetLargeChange(el).ToString("F2"),

                // -- Scroll --------------------------------------------
                "Scroll Into View" => Do(() => _helper.ScrollIntoView(el)),
                "Scroll Up" => _helper.ScrollUp(el, ParseIntOr(input, 3)),
                "Scroll Down" => _helper.ScrollDown(el, ParseIntOr(input, 3)),
                "Horizontal Scroll" => _helper.HorizontalScroll(el, ParseInt(input)),

                // -- Drag and Drop -------------------------------------
                "Drag to Element (target AutomationId)" => DragToElement(el, input),
                "Drag to Point (x,y)" => Do(() => { var p = ParsePair(input); _helper.DragAndDropToPoint(el, (int)p.x, (int)p.y); }),

                // -- Wait ----------------------------------------------
                "Wait for Element" => WaitForElement(input),

                // -- Screenshot ----------------------------------------
                "Capture Element" => _helper.CaptureElement(el, CaptureFolder()),
                "Capture Screen" => _helper.CaptureScreen(CaptureFolder()),

                // -- OCR -----------------------------------------------
                "OCR Element" => Ocr().OcrElement(el).ToString(),
                "OCR Element + Save" => Ocr().OcrElementAndSave(el, CaptureFolder()).ToString(),
                "OCR Region (x,y,w,h)" => OcrRegion(el, input),
                "OCR File" => Ocr().OcrFile(input).ToString(),

                _ => $"Unknown action: {action}"
            };
        }

        private string GetCell(AutomationElement el, string input)
        {
            var parts = input.Split(',');
            if (parts.Length != 2) return "Enter as row,col  e.g. 0,2";
            return _helper.GetGridCell(el, ParseInt(parts[0].Trim()), ParseInt(parts[1].Trim()));
        }

        private string DragToElement(AutomationElement source, string targetId)
        {
            var window = _processor.CurrentWindow;
            if (window == null) return "No window found.";
            var target = _helper.FindByAutomationId(window, targetId)
                      ?? _helper.FindByName(window, targetId);
            if (target == null) return $"Target element '{targetId}' not found.";
            _helper.DragAndDrop(source, target);
            return "";
        }

        private string WaitForElement(string automationId)
        {
            var window = _processor.CurrentWindow;
            if (window == null) return "No window found.";
            var el = _helper.WaitForElement(window, automationId);
            if (el == null) return $"'{automationId}' did not appear within timeout.";
            _processor.SetCurrentTarget(window, el);
            return $"Found: {_helper.Describe(el)}";
        }

        private static string Do(Action action) { action(); return ""; }

        private static int ParseInt(string s) =>
            int.TryParse(s, out int n) ? n : throw new ArgumentException($"Expected integer, got '{s}'");

        private static int ParseIntOr(string s, int fallback) =>
            int.TryParse(s, out int n) ? n : fallback;

        private static (double x, double y) ParsePair(string s)
        {
            var parts = s.Split(',');
            if (parts.Length != 2) throw new ArgumentException("Enter as x,y  e.g. 100,200");
            return (double.Parse(parts[0].Trim()), double.Parse(parts[1].Trim()));
        }

        private static VirtualKeyShort ParseVKey(string s)
        {
            if (Enum.TryParse<VirtualKeyShort>(s, true, out var key)) return key;
            throw new ArgumentException($"Unknown key '{s}'. Use VirtualKeyShort names e.g. RETURN, TAB, DELETE, KEY_A");
        }

        private OcrHelper Ocr() => _ocr ??= new OcrHelper();

        private string OcrRegion(AutomationElement el, string input)
        {
            var parts = input.Split(',');
            if (parts.Length != 4) return "Enter as x,y,w,h  e.g. 0,0,200,50";
            var region = new System.Drawing.Rectangle(
                ParseInt(parts[0].Trim()), ParseInt(parts[1].Trim()),
                ParseInt(parts[2].Trim()), ParseInt(parts[3].Trim()));
            return Ocr().OcrElementRegion(el, region).ToString();
        }

        private static string CaptureFolder() =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Apex_Captures");

        public void Dispose() => _ocr?.Dispose();
    }
}

