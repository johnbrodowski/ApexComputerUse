using FlaUI.Core.Definitions;

namespace ApexComputerUse
{
    /// <summary>
    /// Owns the Find &amp; Execute tab: control-type / action picker, the fuzzy Find flow
    /// (with confirmation dialogs for non-exact matches), and the Execute action runner.
    /// Form1 keeps designer-event stubs that delegate here so the controller can be
    /// unit-replaced and the tab logic kept out of Form1.
    /// </summary>
    internal sealed class FindExecuteTabController
    {
        // -- Per-control-type action menus ------------------------------------
        // Drives both the cmbControlType picker on the tab and what populates
        // cmbAction when a control type is selected.
        private static readonly Dictionary<string, string[]> ControlActions = new()
        {
            ["Window"] =
            [
                "Describe", "Get Bounding Rect", "Get Supported Patterns",
                "Minimize", "Maximize", "Restore", "Close",
                "Move (x,y)", "Resize (w,h)"
            ],
            ["Button"] =
            [
                "Invoke", "Describe", "Get Bounding Rect", "Get Supported Patterns",
                "Click", "Right-Click", "Double-Click", "Hover", "Set Focus"
            ],
            ["TextBox"] =
            [
                "Get Text", "Enter Text", "Set Value",
                "Select All", "Copy", "Cut", "Paste", "Undo", "Clear",
                "Insert at Caret", "Scroll Into View",
                "Describe", "Get Bounding Rect", "Get Supported Patterns"
            ],
            ["PasswordBox"] =
            [
                "Enter Text", "Clear", "Describe"
            ],
            ["Label"] =
            [
                "Get Text", "Describe", "Get Bounding Rect"
            ],
            ["ComboBox"] =
            [
                "Get Selected", "Get All Items", "Select Item (text)",
                "Expand Dropdown", "Collapse Dropdown",
                "Describe", "Get Supported Patterns"
            ],
            ["CheckBox"] =
            [
                "Get State", "Toggle", "Set Focus",
                "Describe", "Get Bounding Rect"
            ],
            ["RadioButton"] =
            [
                "Is Selected", "Select", "Set Focus",
                "Describe", "Get Bounding Rect"
            ],
            ["ListBox"] =
            [
                "Get Selected", "Get All Items",
                "Select by Text", "Select by Index",
                "Describe", "Get Supported Patterns"
            ],
            ["ListView"] =
            [
                "Get Row Count", "Get Column Count",
                "Get Cell (row,col)", "Get Row Values (row)", "Select Row (index)",
                "Describe", "Get Supported Patterns"
            ],
            ["DataGrid"] =
            [
                "Get Row Count", "Get Column Count",
                "Get Cell (row,col)", "Get Row Values (row)", "Select Row (index)",
                "Describe", "Get Supported Patterns"
            ],
            ["TreeView"] =
            [
                "Get Node Count", "Get Node Text (index)",
                "Expand Node (index)", "Collapse Node (index)", "Select Node (index)",
                "Describe", "Get Supported Patterns"
            ],
            ["Menu / MenuItem"] =
            [
                "Invoke", "Expand", "Open Context Menu",
                "Describe", "Get Bounding Rect"
            ],
            ["TabControl"] =
            [
                "Get Selected Tab", "Get Tab Count",
                "Select Tab (index)", "Select Tab (name)",
                "Describe", "Get Supported Patterns"
            ],
            ["Slider"] =
            [
                "Get Slider Value", "Set Slider Value", "Get Min", "Get Max",
                "Get Small Change", "Get Large Change",
                "Describe", "Get Supported Patterns"
            ],
            ["ProgressBar"] =
            [
                "Get Progress Value", "Get Min", "Get Max",
                "Describe", "Get Bounding Rect"
            ],
            ["Hyperlink"] =
            [
                "Invoke", "Describe", "Get Bounding Rect"
            ],
            ["Any Element"] =
            [
                "Describe", "Get Bounding Rect", "Get Supported Patterns",
                "Get Value (pattern)", "Set Value (pattern)",
                "Click", "Right-Click", "Double-Click", "Hover",
                "Set Focus", "Scroll Into View",
                "Select All", "Copy", "Cut", "Paste", "Undo", "Clear",
                "Insert at Caret", "Invoke (pattern)",
                "Expand", "Collapse",
                "Scroll Up", "Scroll Down", "Horizontal Scroll",
                "Capture Element", "Capture Screen",
                "Drag to Element (target AutomationId)", "Drag to Point (x,y)",
                "Wait for Element",
                "Send Key", "Send Keys",
                "Get Focused Element",
                "OCR Element", "OCR Element + Save", "OCR Region (x,y,w,h)", "OCR File"
            ],
        };

        private readonly CommandProcessor _processor;
        private readonly ApexHelper       _helper;
        private readonly ActionExecutor   _executor;
        private readonly Action<string>   _log;

        private readonly ComboBox _cmbControlType, _cmbAction, _cmbSearchType;
        private readonly TextBox  _txtWindowName, _txtElementId, _txtElementName, _txtInput;

        internal FindExecuteTabController(
            CommandProcessor processor,
            ApexHelper       helper,
            ActionExecutor   executor,
            Action<string>   log,
            ComboBox cmbControlType, ComboBox cmbAction, ComboBox cmbSearchType,
            TextBox  txtWindowName,  TextBox  txtElementId, TextBox txtElementName,
            TextBox  txtInput)
        {
            _processor      = processor;
            _helper         = helper;
            _executor       = executor;
            _log            = log;
            _cmbControlType = cmbControlType;
            _cmbAction      = cmbAction;
            _cmbSearchType  = cmbSearchType;
            _txtWindowName  = txtWindowName;
            _txtElementId   = txtElementId;
            _txtElementName = txtElementName;
            _txtInput       = txtInput;
        }

        internal void Init()
        {
            _cmbControlType.Items.AddRange(ControlActions.Keys.ToArray<object>());
            _cmbControlType.SelectedIndex = 0;

            // Search-type filter: "All" + every ControlType except Unknown
            _cmbSearchType.Items.Add("All");
            foreach (ControlType ct in Enum.GetValues<ControlType>())
                if (ct != ControlType.Unknown)
                    _cmbSearchType.Items.Add(ct.ToString());
            _cmbSearchType.SelectedIndex = 0;
        }

        // -- Control-type picker ----------------------------------------------

        internal void ControlTypeChanged()
        {
            _cmbAction.Items.Clear();
            if (_cmbControlType.SelectedItem is string type && ControlActions.TryGetValue(type, out var actions))
                _cmbAction.Items.AddRange(actions.ToArray<object>());
            if (_cmbAction.Items.Count > 0)
                _cmbAction.SelectedIndex = 0;
        }

        // -- Find -------------------------------------------------------------

        internal void Find()
        {
            _processor.SetCurrentTarget(null, null);
            try
            {
                string title = _txtWindowName.Text.Trim();
                if (string.IsNullOrEmpty(title)) { _log("Enter a Window Name."); return; }

                var window = _helper.FindWindowFuzzy(title, out string matchedTitle, out bool windowExact);
                if (window == null) { _log($"No window found for \"{title}\"."); return; }

                if (!windowExact)
                {
                    var answer = MessageBox.Show(
                        $"No exact window match.\nUse closest match?\n\n\"{matchedTitle}\"",
                        "Closest Window Match", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (answer == DialogResult.No) { _log("Window match rejected."); return; }
                }
                _log($"Window ({(windowExact ? "exact" : "fuzzy")}): \"{window.Name}\"");

                string autoId = _txtElementId.Text.Trim();
                string name   = _txtElementName.Text.Trim();

                if (string.IsNullOrEmpty(autoId) && string.IsNullOrEmpty(name))
                {
                    _processor.SetCurrentTarget(window, window);
                    _log($"Targeting window: {_helper.Describe(window)}");
                    return;
                }

                ControlType? filterType = null;
                if (_cmbSearchType.SelectedItem is string st && st != "All")
                    filterType = Enum.Parse<ControlType>(st);

                bool searchById  = !string.IsNullOrEmpty(autoId);
                string searchVal = searchById ? autoId : name;

                var el = _helper.FindElementFuzzy(
                    window, searchVal, filterType, searchById,
                    out string matchedValue, out bool elementExact);

                if (el == null)
                {
                    _processor.SetCurrentTarget(window, null);
                    _log($"No element found for \"{searchVal}\".");
                    return;
                }

                if (!elementExact)
                {
                    string field = searchById ? "AutomationId" : "Name";
                    var answer = MessageBox.Show(
                        $"No exact element match for \"{searchVal}\".\nUse closest match?\n\n{field}: \"{matchedValue}\"",
                        "Closest Element Match", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (answer == DialogResult.No)
                    {
                        _processor.SetCurrentTarget(window, null);
                        _log("Element match rejected.");
                        return;
                    }
                }

                _processor.SetCurrentTarget(window, el);
                _log($"Element ({(elementExact ? "exact" : "fuzzy")}): {_helper.Describe(el)}");
            }
            catch (Exception ex) { _log($"Error: {ex.Message}"); }
        }

        // -- Execute ----------------------------------------------------------

        internal void Execute()
        {
            var element = _processor.CurrentElement;
            if (element == null) { _log("Find an element first."); return; }
            if (!CommandProcessor.IsElementValid(element))
            {
                _log("Element is no longer available (target app closed or changed). Run 'Find' again.");
                _processor.SetCurrentTarget(_processor.CurrentWindow, null);
                return;
            }
            string action = _cmbAction.SelectedItem?.ToString() ?? "";
            string input  = _txtInput.Text.Trim();
            try
            {
                string result = _executor.Execute(element, action, input);
                _log(string.IsNullOrEmpty(result) ? $"'{action}' done." : $"Result: {result}");
            }
            catch (Exception ex) { _log($"Error: {ex.Message}"); }
        }
    }
}
