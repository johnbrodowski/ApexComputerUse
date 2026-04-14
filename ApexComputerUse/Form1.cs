using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Text.Json;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.WindowsAPI;

namespace ApexComputerUse
{
    public partial class Form1 : Form
    {
        private readonly ApexHelper _helper = new();
        private readonly CommandProcessor _processor = new();
        private readonly SceneStore _sceneStore = new();
        private OcrHelper? _ocr;
        private HttpCommandServer? _http;
        private TelegramController? _telegram;
        private PipeCommandServer? _pipe;

        // ── Status bar ────────────────────────────────────────────────────
        private readonly System.Windows.Forms.Timer _statusTimer = new() { Interval = 2000 };
        private System.Diagnostics.PerformanceCounter? _cpuCounter;
        private long _netBytesPrev = -1;
        private CancellationTokenSource? _downloadCts;

        // ── Persistent settings ───────────────────────────────────────────
        private static readonly string SettingsDir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ApexComputerUse");
        private static readonly string SettingsFile = Path.Combine(SettingsDir, "settings.json");

        private sealed class AppSettings
        {
            public string ModelPath { get; set; } = "";
            public string ProjPath { get; set; } = "";
            /// <summary>
            /// API key for the HTTP server. Auto-generated on first launch.
            /// Clear this field (or delete settings.json) to disable auth.
            /// </summary>
            public string ApiKey { get; set; } = "";
            /// <summary>
            /// Comma-separated list of Telegram chat IDs allowed to control this machine.
            /// Leave empty to disable the whitelist (any user who discovers the bot token can connect).
            /// </summary>
            public string AllowedChatIds { get; set; } = "";
        }

        private void LoadSettings()
        {
            // Layer 1: Apply appsettings.json / env-var defaults (AppConfig) as baseline.
            var cfg = AppConfig.Current;
            if (cfg.HttpPort != 8081) txtHttpPort.Text = cfg.HttpPort.ToString();
            if (!string.IsNullOrWhiteSpace(cfg.PipeName) &&
                cfg.PipeName != "ApexComputerUse") txtPipeName.Text = cfg.PipeName;
            if (!string.IsNullOrWhiteSpace(cfg.ModelPath)) txtModelPath.Text = cfg.ModelPath;
            if (!string.IsNullOrWhiteSpace(cfg.MmProjPath)) txtProjPath.Text = cfg.MmProjPath;
            if (!string.IsNullOrWhiteSpace(cfg.ApiKey)) txtApiKey.Text = cfg.ApiKey;
            if (!string.IsNullOrWhiteSpace(cfg.AllowedChatIds)) txtAllowedChatIds.Text = cfg.AllowedChatIds;
            if (!string.IsNullOrWhiteSpace(cfg.TelegramToken)) txtBotToken.Text = cfg.TelegramToken;

            // Layer 2: User's saved preferences in AppData override the above.
            try
            {
                if (!File.Exists(SettingsFile))
                {
                    // First launch — generate a key and persist it immediately.
                    EnsureApiKey();
                    return;
                }
                var s = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsFile));
                if (s == null) return;
                if (!string.IsNullOrWhiteSpace(s.ModelPath)) txtModelPath.Text = s.ModelPath;
                if (!string.IsNullOrWhiteSpace(s.ProjPath)) txtProjPath.Text = s.ProjPath;
                if (!string.IsNullOrWhiteSpace(s.ApiKey)) txtApiKey.Text = s.ApiKey;
                else EnsureApiKey();   // old settings file missing key
                if (!string.IsNullOrWhiteSpace(s.AllowedChatIds)) txtAllowedChatIds.Text = s.AllowedChatIds;
            }
            catch { /* ignore corrupt settings — will regenerate on next save */ }
        }

        private void SaveSettings()
        {
            try
            {
                Directory.CreateDirectory(SettingsDir);
                var s = new AppSettings
                {
                    ModelPath = txtModelPath.Text,
                    ProjPath = txtProjPath.Text,
                    ApiKey = txtApiKey.Text,
                    AllowedChatIds = txtAllowedChatIds.Text
                };
                File.WriteAllText(SettingsFile,
                    JsonSerializer.Serialize(s, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { /* ignore write errors */ }
        }

        /// <summary>Generates a new random API key and writes it to the API key field.</summary>
        private void EnsureApiKey()
        {
            if (!string.IsNullOrWhiteSpace(txtApiKey.Text)) return;
            txtApiKey.Text = GenerateApiKey();
            SaveSettings();
        }

        private static string GenerateApiKey()
        {
            var bytes = new byte[24];
            System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
            return Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
        }

        private AutomationElement? _foundElement;
        private Window? _targetWindow;

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

        public Form1()
        {
            InitializeComponent();

            // Route CommandProcessor logs to the status box
            _processor.OnLog += msg => { BeginInvoke(() => Log(msg)); AppLog.FromOnLog(msg); };

            // Action control-type picker
            cmbControlType.Items.AddRange(ControlActions.Keys.ToArray<object>());
            cmbControlType.SelectedIndex = 0;

            // Search-type filter: "All" + every ControlType except Unknown
            cmbSearchType.Items.Add("All");
            foreach (ControlType ct in Enum.GetValues<ControlType>())
                if (ct != ControlType.Unknown)
                    cmbSearchType.Items.Add(ct.ToString());
            cmbSearchType.SelectedIndex = 0;

            // Status bar timer — CPU counter init happens on background thread to avoid startup lag
            _statusTimer.Tick += StatusTimer_Tick;
            _statusTimer.Start();
            Task.Run(() =>
            {
                try { _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total"); }
                catch { /* not available in all environments */ }
            });

            // Inject SceneStore into CommandProcessor
            _processor.SceneStore = _sceneStore;

            // Restore saved model paths
            LoadSettings();

            // First-launch hint: if the default model files are absent, go to the Model tab
            this.Load += (_, _) => CheckFirstLaunch();

            // Auto-start HTTP server once the form is fully loaded
            this.Load += (_, _) => btnStartHttp_Click(this, EventArgs.Empty);
        }

        private void cmbControlType_SelectedIndexChanged(object sender, EventArgs e)
        {
            cmbAction.Items.Clear();
            if (cmbControlType.SelectedItem is string type && ControlActions.TryGetValue(type, out var actions))
                cmbAction.Items.AddRange(actions.ToArray<object>());
            if (cmbAction.Items.Count > 0)
                cmbAction.SelectedIndex = 0;
        }

        // ── Command input ─────────────────────────────────────────────────

        private void txtCommand_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Return)
            {
                RunCommand();
                e.SuppressKeyPress = true;
            }
        }

        private void btnRun_Click(object sender, EventArgs e) => RunCommand();

        private void RunCommand()
        {
            string input = txtCommand.Text.Trim();
            if (string.IsNullOrEmpty(input)) return;

            var req = ParseCommandLine(input);
            if (req == null) { Log($"Unknown command. Type 'help' for a list."); return; }

            try
            {
                var response = _processor.Process(req);
                Log(response.ToText());
            }
            catch (Exception ex) { Log($"Error: {ex.Message}"); }
        }

        private CommandRequest? ParseCommandLine(string input)
        {
            var parts = input.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            string cmd = parts[0].ToLowerInvariant();
            string args = parts.Length > 1 ? parts[1] : "";
            var kv = ParseKvArgs(args);

            return cmd switch
            {
                "find" => new CommandRequest
                {
                    Command = "find",
                    Window = kv.Get("window", "w"),
                    AutomationId = kv.Get("id", "automationid"),
                    ElementName = kv.Get("name", "n"),
                    SearchType = kv.Get("type", "t")
                },
                "exec" or "execute" => new CommandRequest
                {
                    Command = "execute",
                    Action = kv.Get("action", "a"),
                    Value = kv.Get("value", "v")
                },
                "ocr" => new CommandRequest
                {
                    Command = "ocr",
                    Value = kv.Get("value", "region") ?? (args.Contains(',') ? args : null)
                },
                "ai" => new CommandRequest
                {
                    Command = "ai",
                    Action = kv.Get("action", "a")
                                   ?? args.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                                         .FirstOrDefault()?.ToLowerInvariant(),
                    ModelPath = kv.Get("model"),
                    MmProjPath = kv.Get("proj"),
                    Value = kv.Get("value", "path", "v"),
                    Prompt = kv.Get("prompt", "p")
                },
                "status" => new CommandRequest { Command = "status" },
                "windows" => new CommandRequest { Command = "windows" },
                "elements" => new CommandRequest
                {
                    Command = "elements",
                    SearchType = kv.Get("type", "t") ?? (args.Length > 0 ? args.Trim() : null)
                },
                "help" => new CommandRequest { Command = "help" },
                _ => null
            };
        }

        private static Dictionary<string, string> ParseKvArgs(string input)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            int i = 0;
            while (i < input.Length)
            {
                while (i < input.Length && input[i] == ' ') i++;
                if (i >= input.Length) break;
                int keyStart = i;
                while (i < input.Length && input[i] != '=' && input[i] != ' ') i++;
                string key = input[keyStart..i].Trim();
                if (string.IsNullOrEmpty(key)) { i++; continue; }
                if (i >= input.Length || input[i] != '=') { result[key] = ""; continue; }
                i++; // skip '='
                string value;
                if (i < input.Length && input[i] == '"')
                {
                    i++;
                    int vs = i;
                    while (i < input.Length && input[i] != '"') i++;
                    value = input[vs..i];
                    if (i < input.Length) i++;
                }
                else
                {
                    int vs = i;
                    while (i < input.Length && input[i] != ' ') i++;
                    value = input[vs..i];
                }
                result[key] = value;
            }
            return result;
        }

        // ── Find ──────────────────────────────────────────────────────────

        private void btnFind_Click(object sender, EventArgs e)
        {
            _foundElement = null;
            _targetWindow = null;
            try
            {
                string title = txtWindowName.Text.Trim();
                if (string.IsNullOrEmpty(title)) { Log("Enter a Window Name."); return; }

                // Fuzzy window find
                _targetWindow = _helper.FindWindowFuzzy(title, out string matchedTitle, out bool windowExact);
                if (_targetWindow == null) { Log($"No window found for \"{title}\"."); return; }

                if (!windowExact)
                {
                    var answer = MessageBox.Show(
                        $"No exact window match.\nUse closest match?\n\n\"{matchedTitle}\"",
                        "Closest Window Match", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (answer == DialogResult.No) { Log("Window match rejected."); _targetWindow = null; return; }
                }
                Log($"Window ({(windowExact ? "exact" : "fuzzy")}): \"{_targetWindow.Name}\"");

                string autoId = txtElementId.Text.Trim();
                string name = txtElementName.Text.Trim();

                // If no element search term, target the window itself
                if (string.IsNullOrEmpty(autoId) && string.IsNullOrEmpty(name))
                {
                    _foundElement = _targetWindow;
                    Log($"Targeting window: {_helper.Describe(_foundElement)}");
                    return;
                }

                // Resolve Search Type filter
                ControlType? filterType = null;
                if (cmbSearchType.SelectedItem is string st && st != "All")
                    filterType = Enum.Parse<ControlType>(st);

                bool searchById = !string.IsNullOrEmpty(autoId);
                string searchVal = searchById ? autoId : name;

                // Fuzzy element find
                var el = _helper.FindElementFuzzy(
                    _targetWindow, searchVal, filterType, searchById,
                    out string matchedValue, out bool elementExact);

                if (el == null) { Log($"No element found for \"{searchVal}\"."); return; }

                if (!elementExact)
                {
                    string field = searchById ? "AutomationId" : "Name";
                    var answer = MessageBox.Show(
                        $"No exact element match for \"{searchVal}\".\nUse closest match?\n\n{field}: \"{matchedValue}\"",
                        "Closest Element Match", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (answer == DialogResult.No) { Log("Element match rejected."); return; }
                }

                _foundElement = el;
                Log($"Element ({(elementExact ? "exact" : "fuzzy")}): {_helper.Describe(_foundElement)}");
            }
            catch (Exception ex) { Log($"Error: {ex.Message}"); }
        }

        // ── Execute ───────────────────────────────────────────────────────

        private void btnExecute_Click(object sender, EventArgs e)
        {
            if (_foundElement == null) { Log("Find an element first."); return; }

            string action = cmbAction.SelectedItem?.ToString() ?? "";
            string input = txtInput.Text.Trim();
            try
            {
                string result = ExecuteAction(_foundElement, action, input);
                Log(string.IsNullOrEmpty(result) ? $"'{action}' done." : $"Result: {result}");
            }
            catch (Exception ex) { Log($"Error: {ex.Message}"); }
        }

        private string ExecuteAction(AutomationElement el, string action, string input)
        {
            return action switch
            {
                // ── Info ──────────────────────────────────────────────
                "Describe" => _helper.Describe(el),
                "Get Bounding Rect" => _helper.GetBoundingRect(el),
                "Get Supported Patterns" => _helper.GetSupportedPatterns(el),
                "Get Focused Element" => _helper.GetFocusedElement(),

                // ── Window ────────────────────────────────────────────
                "Minimize" => Do(() => _helper.MinimizeWindow(el.AsWindow())),
                "Maximize" => Do(() => _helper.MaximizeWindow(el.AsWindow())),
                "Restore" => Do(() => _helper.RestoreWindow(el.AsWindow())),
                "Close" => Do(() => _helper.CloseWindow(el.AsWindow())),
                "Move (x,y)" => Do(() => { var p = ParsePair(input); _helper.MoveWindow(el.AsWindow(), (int)p.x, (int)p.y); }),
                "Resize (w,h)" => Do(() => { var p = ParsePair(input); _helper.ResizeWindow(el.AsWindow(), p.x, p.y); }),

                // ── Mouse ─────────────────────────────────────────────
                "Click" => Do(() => _helper.ClickElement(el)),
                "Right-Click" => Do(() => _helper.RightClickElement(el)),
                "Double-Click" => Do(() => _helper.DoubleClickElement(el)),
                "Hover" => Do(() => _helper.HoverElement(el)),
                "Set Focus" => Do(() => _helper.SetFocus(el)),

                // ── Invoke / patterns ─────────────────────────────────
                "Invoke" => Do(() => _helper.InvokeButton(el)),
                "Invoke (pattern)" => Do(() => _helper.InvokePattern(el)),
                "Expand" => Do(() => el.Patterns.ExpandCollapse.Pattern.Expand()),
                "Collapse" => Do(() => el.Patterns.ExpandCollapse.Pattern.Collapse()),
                "Open Context Menu" => Do(() => _helper.OpenContextMenu(el)),

                // ── Text ──────────────────────────────────────────────
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

                // ── Keyboard ──────────────────────────────────────────
                "Send Keys" => Do(() => _helper.SendKeys(input)),
                "Send Key" => Do(() => _helper.SendKey(ParseVKey(input))),

                // ── ComboBox ──────────────────────────────────────────
                "Get Selected" => _helper.GetComboBoxSelected(el),
                "Get All Items" => string.Join(", ", _helper.GetComboBoxItems(el)),
                "Select Item (text)" => Do(() => _helper.SelectComboBoxItem(el, input)),
                "Expand Dropdown" => Do(() => _helper.ExpandComboBox(el)),
                "Collapse Dropdown" => Do(() => _helper.CollapseComboBox(el)),

                // ── CheckBox ─────────────────────────────────────────
                "Get State" => _helper.IsCheckBoxChecked(el)?.ToString() ?? "indeterminate",
                "Toggle" => Do(() => _helper.ToggleCheckBox(el)),

                // ── RadioButton ───────────────────────────────────────
                "Is Selected" => _helper.IsRadioButtonSelected(el).ToString(),
                "Select" => Do(() => _helper.SelectRadioButton(el)),

                // ── ListBox ───────────────────────────────────────────
                "Select by Text" => Do(() => _helper.SelectListBoxByText(el, input)),
                "Select by Index" => Do(() => _helper.SelectListBoxByIndex(el, ParseInt(input))),

                // ── Grid / ListView / DataGrid ────────────────────────
                "Get Row Count" => _helper.GetGridRowCount(el).ToString(),
                "Get Column Count" => _helper.GetGridColumnCount(el).ToString(),
                "Get Cell (row,col)" => GetCell(el, input),
                "Get Row Values (row)" => _helper.GetGridRowValues(el, ParseInt(input)),
                "Select Row (index)" => Do(() => _helper.SelectGridRow(el, ParseInt(input))),

                // ── TreeView ──────────────────────────────────────────
                "Get Node Count" => _helper.GetTreeNodeCount(el).ToString(),
                "Get Node Text (index)" => _helper.GetTreeNodeText(el, ParseInt(input)),
                "Expand Node (index)" => Do(() => _helper.ExpandTreeNode(el, ParseInt(input))),
                "Collapse Node (index)" => Do(() => _helper.CollapseTreeNode(el, ParseInt(input))),
                "Select Node (index)" => Do(() => _helper.SelectTreeNode(el, ParseInt(input))),

                // ── TabControl ────────────────────────────────────────
                "Get Selected Tab" => _helper.GetSelectedTabName(el),
                "Get Tab Count" => _helper.GetTabCount(el).ToString(),
                "Select Tab (index)" => Do(() => _helper.SelectTab(el, ParseInt(input))),
                "Select Tab (name)" => Do(() => _helper.SelectTabByName(el, input)),

                // ── Slider / ProgressBar / RangeValue ─────────────────
                "Get Slider Value" => _helper.GetSliderValue(el).ToString("F2"),
                "Get Progress Value" => _helper.GetProgressBarValue(el).ToString("F2"),
                "Set Slider Value" => Do(() => _helper.SetSliderValue(el, double.Parse(input))),
                "Get Min" => _helper.GetRangeMin(el).ToString("F2"),
                "Get Max" => _helper.GetRangeMax(el).ToString("F2"),
                "Get Small Change" => _helper.GetSmallChange(el).ToString("F2"),
                "Get Large Change" => _helper.GetLargeChange(el).ToString("F2"),

                // ── Scroll ────────────────────────────────────────────
                "Scroll Into View" => Do(() => _helper.ScrollIntoView(el)),
                "Scroll Up" => Do(() => _helper.ScrollUp(ParseIntOr(input, 3))),
                "Scroll Down" => Do(() => _helper.ScrollDown(ParseIntOr(input, 3))),
                "Horizontal Scroll" => Do(() => _helper.HorizontalScroll(ParseInt(input))),

                // ── Drag and Drop ─────────────────────────────────────
                "Drag to Element (target AutomationId)" => DragToElement(el, input),
                "Drag to Point (x,y)" => Do(() => { var p = ParsePair(input); _helper.DragAndDropToPoint(el, (int)p.x, (int)p.y); }),

                // ── Wait ──────────────────────────────────────────────
                "Wait for Element" => WaitForElement(input),

                // ── Screenshot ────────────────────────────────────────
                "Capture Element" => _helper.CaptureElement(el, CaptureFolder()),
                "Capture Screen" => _helper.CaptureScreen(CaptureFolder()),

                // ── OCR ───────────────────────────────────────────────
                "OCR Element" => Ocr().OcrElement(el).ToString(),
                "OCR Element + Save" => Ocr().OcrElementAndSave(el, CaptureFolder()).ToString(),
                "OCR Region (x,y,w,h)" => OcrRegion(el, input),
                "OCR File" => Ocr().OcrFile(input).ToString(),

                _ => $"Unknown action: {action}"
            };
        }

        // ── Helpers ───────────────────────────────────────────────────────

        private string GetCell(AutomationElement el, string input)
        {
            var parts = input.Split(',');
            if (parts.Length != 2) return "Enter as row,col  e.g. 0,2";
            return _helper.GetGridCell(el, ParseInt(parts[0].Trim()), ParseInt(parts[1].Trim()));
        }

        private string DragToElement(AutomationElement source, string targetId)
        {
            if (_targetWindow == null) return "No window found.";
            var target = _helper.FindByAutomationId(_targetWindow, targetId)
                      ?? _helper.FindByName(_targetWindow, targetId);
            if (target == null) return $"Target element '{targetId}' not found.";
            _helper.DragAndDrop(source, target);
            return "";
        }

        private string WaitForElement(string automationId)
        {
            if (_targetWindow == null) return "No window found.";
            var el = _helper.WaitForElement(_targetWindow, automationId);
            if (el == null) return $"'{automationId}' did not appear within timeout.";
            _foundElement = el;
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
            // Allow bare letters / digits like "A", "RETURN", "TAB", "DELETE"
            throw new ArgumentException($"Unknown key '{s}'. Use VirtualKeyShort names e.g. RETURN, TAB, DELETE, KEY_A");
        }

        private OcrHelper Ocr() =>
            _ocr ??= new OcrHelper();

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

        private void btnClear_Click(object sender, EventArgs e) => txtStatus.Clear();

        // ── Remote Control ────────────────────────────────────────────────

        private void btnStartHttp_Click(object sender, EventArgs e)
        {
            if (_http?.IsRunning == true)
            {
                _http.Stop();
                btnStartHttp.Text = "Start HTTP";
                lblHttpStatus.Text = "Stopped";
                lblHttpStatus.ForeColor = Color.Gray;
                return;
            }

            if (!int.TryParse(txtHttpPort.Text.Trim(), out int port) || port < 1 || port > 65535)
            {
                Log("Invalid port number.");
                return;
            }

            try
            {
                string apiKey = txtApiKey.Text.Trim();
                var appCfg = AppConfig.Current;
                _http = new HttpCommandServer(port, _processor, _sceneStore, apiKey,
                            enableShellRun: appCfg.EnableShellRun,
                            bindAll: appCfg.HttpBindAll);
                _http.OnLog += msg => { BeginInvoke(() => Log(msg)); AppLog.FromOnLog(msg); };
                _http.Start();
                btnStartHttp.Text = "Stop HTTP";
                string authNote = string.IsNullOrWhiteSpace(apiKey) ? " (no auth)" : " (auth enabled)";
                lblHttpStatus.Text = $"Listening :{port}{authNote}";
                lblHttpStatus.ForeColor = string.IsNullOrWhiteSpace(apiKey) ? Color.DarkOrange : Color.Green;
            }
            catch (Exception ex) { Log($"HTTP start error: {ex.Message}"); }
        }

        private void btnStartPipe_Click(object sender, EventArgs e)
        {
            if (_pipe?.IsRunning == true)
            {
                _pipe.Stop();
                btnStartPipe.Text = "Start Pipe";
                lblPipeStatus.Text = "Stopped";
                lblPipeStatus.ForeColor = Color.Gray;
                return;
            }

            string name = txtPipeName.Text.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                Log("Enter a pipe name.");
                return;
            }

            try
            {
                _pipe = new PipeCommandServer(name, _processor);
                _pipe.OnLog += msg => { BeginInvoke(() => Log(msg)); AppLog.FromOnLog(msg); };
                _pipe.Start();
                btnStartPipe.Text = "Stop Pipe";
                lblPipeStatus.Text = $"Running: {name}";
                lblPipeStatus.ForeColor = Color.Green;
            }
            catch (Exception ex) { Log($"Pipe start error: {ex.Message}"); }
        }

        private void btnStartTelegram_Click(object sender, EventArgs e)
        {
            if (_telegram?.IsRunning == true)
            {
                _telegram.Stop();
                btnStartTelegram.Text = "Start Telegram";
                lblTelegramStatus.Text = "Telegram: Stopped";
                lblTelegramStatus.ForeColor = Color.Gray;
                return;
            }

            string token = txtBotToken.Text.Trim();
            if (string.IsNullOrWhiteSpace(token))
            {
                Log("Enter a Telegram bot token.");
                return;
            }

            try
            {
                // Parse comma-separated allowed chat IDs from the UI field.
                var allowedIds = txtAllowedChatIds.Text
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(s => long.TryParse(s, out long id) ? (long?)id : null)
                    .Where(id => id.HasValue)
                    .Select(id => id!.Value)
                    .ToList();

                _telegram = new TelegramController(token, _processor, allowedIds.Count > 0 ? allowedIds : null);
                _telegram.OnLog += msg => { BeginInvoke(() => Log(msg)); AppLog.FromOnLog(msg); };
                _telegram.Start();
                btnStartTelegram.Text = "Stop Telegram";
                string authNote = allowedIds.Count > 0
                    ? $" ({allowedIds.Count} allowed chat(s))"
                    : " (no chat ID filter)";
                lblTelegramStatus.Text = $"Telegram: Running{authNote}";
                lblTelegramStatus.ForeColor = allowedIds.Count > 0 ? Color.Green : Color.DarkOrange;
            }
            catch (Exception ex) { Log($"Telegram start error: {ex.Message}"); }
        }

        private void btnCopyApiKey_Click(object sender, EventArgs e)
        {
            string key = txtApiKey.Text.Trim();
            if (string.IsNullOrWhiteSpace(key)) { Log("API key is empty."); return; }
            Clipboard.SetText(key);
            Log("API key copied to clipboard.");
        }

        private void Log(string msg) =>
            txtStatus.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}{Environment.NewLine}");

        // ── Status bar ────────────────────────────────────────────────────

        private void StatusTimer_Tick(object? sender, EventArgs e)
        {
            // CPU
            try
            {
                if (_cpuCounter != null)
                    lblStatCpu.Text = $"CPU: {_cpuCounter.NextValue():0}%";
            }
            catch { lblStatCpu.Text = "CPU: --"; }

            // RAM (process working set)
            try
            {
                long ramMb = Process.GetCurrentProcess().WorkingSet64 / 1024 / 1024;
                lblStatRam.Text = $"RAM: {ramMb} MB";
            }
            catch { lblStatRam.Text = "RAM: --"; }

            // Model state
            if (_processor.IsProcessing)
                lblStatModel.Text = "Model: ⚙ Processing";
            else if (_processor.IsModelLoaded)
                lblStatModel.Text = "Model: Loaded";
            else
                lblStatModel.Text = "Model: --";

            // Network (total bytes/sec across all adapters)
            try
            {
                long totalBytes = 0;
                foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
                    if (nic.OperationalStatus == OperationalStatus.Up)
                    {
                        var stats = nic.GetIPv4Statistics();
                        totalBytes += stats.BytesSent + stats.BytesReceived;
                    }

                if (_netBytesPrev >= 0)
                {
                    long delta = totalBytes - _netBytesPrev;
                    double kbps = delta / 1024.0 / (_statusTimer.Interval / 1000.0);
                    lblStatNet.Text = kbps >= 1024
                        ? $"Net: {kbps / 1024:0.0} MB/s"
                        : $"Net: {kbps:0} KB/s";
                }
                _netBytesPrev = totalBytes;
            }
            catch { lblStatNet.Text = "Net: --"; }
        }

        // ── Model tab ────────────────────────────────────────────────────

        private void btnBrowseModel_Click(object sender, EventArgs e)
        {
            using var dlg = new OpenFileDialog
            { Filter = "GGUF Model|*.gguf|All Files|*.*", Title = "Select LLM Model (.gguf)" };
            if (dlg.ShowDialog() == DialogResult.OK)
                txtModelPath.Text = dlg.FileName;
        }

        private void btnBrowseProj_Click(object sender, EventArgs e)
        {
            using var dlg = new OpenFileDialog
            { Filter = "GGUF Projector|*.gguf|All Files|*.*", Title = "Select Multimodal Projector (.gguf)" };
            if (dlg.ShowDialog() == DialogResult.OK)
                txtProjPath.Text = dlg.FileName;
        }

        // ── Default model / tessdata paths ───────────────────────────────
        private static readonly string DefaultModelsDir =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "models");
        private static readonly string DefaultTessdataDir =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tessdata");

        private static readonly (string Url, string RelPath, string Label)[] SetupFiles =
        [
            (
                "https://huggingface.co/LiquidAI/LFM2.5-VL-450M-GGUF/resolve/main/LFM2.5-VL-450M-Q4_0.gguf",
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "models", "LFM2.5-VL-450M-Q4_0.gguf"),
                "LFM2.5-VL model"
            ),
            (
                "https://huggingface.co/LiquidAI/LFM2.5-VL-450M-GGUF/resolve/main/mmproj-LFM2.5-VL-450m-F16.gguf",
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "models", "mmproj-LFM2.5-VL-450m-F16.gguf"),
                "projector"
            ),
            (
                "https://github.com/tesseract-ocr/tessdata/raw/refs/heads/main/eng.traineddata",
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tessdata", "eng.traineddata"),
                "eng.traineddata"
            ),
        ];

        private void CheckFirstLaunch()
        {
            bool anyMissing = SetupFiles.Any(f => !File.Exists(f.RelPath));
            if (!anyMissing) return;

            // Switch to Model tab so the user sees the Download All button
            tabMain.SelectedTab = tabPageModel;
            lblDownloadStatus.ForeColor = Color.DarkBlue;
            lblDownloadStatus.Text = "First launch — click \"Download All\" to set up models and tessdata.";
        }

        // Shared streaming download helper; returns true on success.
        private async Task<bool> DownloadFileAsync(
            string url, string destPath,
            Action<string, Color> setStatus,
            Action<int> setProgress,
            CancellationToken ct)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
            using var client = new System.Net.Http.HttpClient();
            using var resp = await client.GetAsync(url, System.Net.Http.HttpCompletionOption.ResponseHeadersRead, ct);
            resp.EnsureSuccessStatusCode();

            long total = resp.Content.Headers.ContentLength ?? -1;
            using var src = await resp.Content.ReadAsStreamAsync(ct);
            using var dest = File.Create(destPath);

            var buffer = new byte[81920];
            long downloaded = 0;
            int read;
            while ((read = await src.ReadAsync(buffer, ct)) > 0)
            {
                await dest.WriteAsync(buffer.AsMemory(0, read), ct);
                downloaded += read;
                if (total > 0)
                {
                    int pct = (int)(downloaded * 100 / total);
                    BeginInvoke(() =>
                    {
                        setProgress(pct);
                        setStatus($"{downloaded / 1024 / 1024} MB / {total / 1024 / 1024} MB  ({pct}%)", Color.DarkBlue);
                    });
                }
            }
            return true;
        }

        private async void btnDownloadAll_Click(object sender, EventArgs e)
        {
            if (_downloadCts != null) { _downloadCts.Cancel(); return; }

            _downloadCts = new CancellationTokenSource();
            btnDownloadAll.Text = "Cancel";
            btnDownload.Enabled = false;
            pbarDownload.Value = 0;

            try
            {
                for (int i = 0; i < SetupFiles.Length; i++)
                {
                    var (url, dest, label) = SetupFiles[i];
                    if (File.Exists(dest))
                    {
                        BeginInvoke(() =>
                        {
                            lblDownloadStatus.ForeColor = Color.Gray;
                            lblDownloadStatus.Text = $"[{i + 1}/{SetupFiles.Length}] {label} already exists — skipping.";
                        });
                        await Task.Delay(400, _downloadCts.Token); // brief pause so user can read it
                        continue;
                    }

                    BeginInvoke(() =>
                    {
                        pbarDownload.Value = 0;
                        lblDownloadStatus.ForeColor = Color.DarkBlue;
                        lblDownloadStatus.Text = $"[{i + 1}/{SetupFiles.Length}] Downloading {label}…";
                    });

                    await DownloadFileAsync(
                        url, dest,
                        (msg, col) => { lblDownloadStatus.Text = $"[{i + 1}/{SetupFiles.Length}] {label}: {msg}"; lblDownloadStatus.ForeColor = col; },
                        pct => pbarDownload.Value = pct,
                        _downloadCts.Token);
                }

                BeginInvoke(() =>
                {
                    pbarDownload.Value = 100;
                    lblDownloadStatus.ForeColor = Color.Green;
                    lblDownloadStatus.Text = "All files downloaded.";
                    // Auto-populate model path boxes
                    txtModelPath.Text = SetupFiles[0].RelPath;
                    txtProjPath.Text = SetupFiles[1].RelPath;
                    SaveSettings();
                    Log($"Setup complete. Model: {SetupFiles[0].RelPath}");
                    Log($"Projector: {SetupFiles[1].RelPath}");
                    Log($"Tessdata:  {SetupFiles[2].RelPath}");
                });
            }
            catch (OperationCanceledException)
            {
                BeginInvoke(() =>
                {
                    lblDownloadStatus.ForeColor = Color.Gray;
                    lblDownloadStatus.Text = "Cancelled.";
                    // Clean up any partial file that was being written
                    foreach (var (_, dest, _) in SetupFiles)
                        if (File.Exists(dest))
                            try
                            {
                                // Only delete if it looks incomplete (very small)
                                if (new FileInfo(dest).Length < 1024) File.Delete(dest);
                            }
                            catch { }
                });
            }
            catch (Exception ex)
            {
                BeginInvoke(() =>
                {
                    lblDownloadStatus.ForeColor = Color.Red;
                    lblDownloadStatus.Text = $"Error: {ex.Message}";
                    Log($"Download All error: {ex.Message}");
                });
            }
            finally
            {
                _downloadCts?.Dispose();
                _downloadCts = null;
                BeginInvoke(() =>
                {
                    btnDownloadAll.Text = "Download All  (LFM2.5-VL model + projector + tessdata)";
                    btnDownload.Enabled = true;
                });
            }
        }

        private async void btnLoadModel_Click(object sender, EventArgs e)
        {
            string model = txtModelPath.Text.Trim();
            string proj = txtProjPath.Text.Trim();
            if (string.IsNullOrWhiteSpace(model)) { Log("Enter a model path."); return; }
            if (string.IsNullOrWhiteSpace(proj)) { Log("Enter a projector path."); return; }

            btnLoadModel.Enabled = false;
            lblModelStatus.Text = "Loading…";
            lblModelStatus.ForeColor = Color.DarkOrange;

            try
            {
                var resp = await _processor.InitModelAsync(model, proj);
                Log(resp.ToText());
                if (resp.Success)
                {
                    lblModelStatus.Text = "Loaded ✓";
                    lblModelStatus.ForeColor = Color.Green;
                    SaveSettings();
                }
                else
                {
                    lblModelStatus.Text = "Failed — see Console tab";
                    lblModelStatus.ForeColor = Color.Red;
                }
            }
            catch (Exception ex)
            {
                Log($"[Load Model] Unhandled: {ex}");
                lblModelStatus.Text = "Failed — see Console tab";
                lblModelStatus.ForeColor = Color.Red;
            }
            finally
            {
                btnLoadModel.Enabled = true;
            }
        }

        private async void btnDownload_Click(object sender, EventArgs e)
        {
            // Toggle: if already downloading, cancel
            if (_downloadCts != null)
            {
                _downloadCts.Cancel();
                return;
            }

            string url = txtDownloadUrl.Text.Trim();
            if (string.IsNullOrWhiteSpace(url)) { Log("Enter a download URL."); return; }

            // Default save folder = same as model path directory, else Desktop
            string defaultDir = string.IsNullOrWhiteSpace(txtProjPath.Text)
                ? Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
                : Path.GetDirectoryName(txtProjPath.Text) ?? Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

            string fileName = Path.GetFileName(new Uri(url).LocalPath);
            if (string.IsNullOrWhiteSpace(fileName)) fileName = "model.gguf";

            using var saveDlg = new SaveFileDialog
            {
                Title = "Save Vision Model As",
                Filter = "GGUF Model|*.gguf|All Files|*.*",
                FileName = fileName,
                InitialDirectory = defaultDir
            };
            if (saveDlg.ShowDialog() != DialogResult.OK) return;

            string destPath = saveDlg.FileName;
            _downloadCts = new CancellationTokenSource();
            btnDownload.Text = "Cancel";
            pbarDownload.Value = 0;
            lblDownloadStatus.ForeColor = Color.DarkBlue;
            lblDownloadStatus.Text = "Starting download…";

            try
            {
                await DownloadFileAsync(
                    url, destPath,
                    (msg, col) => { lblDownloadStatus.Text = msg; lblDownloadStatus.ForeColor = col; },
                    pct => pbarDownload.Value = pct,
                    _downloadCts.Token);

                BeginInvoke(() =>
                {
                    pbarDownload.Value = 100;
                    lblDownloadStatus.ForeColor = Color.Green;
                    lblDownloadStatus.Text = $"Done — {destPath}";
                    txtProjPath.Text = destPath;
                    Log($"Vision model downloaded to: {destPath}");
                });
            }
            catch (OperationCanceledException)
            {
                BeginInvoke(() =>
                {
                    lblDownloadStatus.ForeColor = Color.Gray;
                    lblDownloadStatus.Text = "Cancelled.";
                    if (File.Exists(destPath)) try { File.Delete(destPath); } catch { }
                });
            }
            catch (Exception ex)
            {
                BeginInvoke(() =>
                {
                    lblDownloadStatus.ForeColor = Color.Red;
                    lblDownloadStatus.Text = $"Error: {ex.Message}";
                    Log($"Download error: {ex.Message}");
                });
            }
            finally
            {
                _downloadCts?.Dispose();
                _downloadCts = null;
                BeginInvoke(() => btnDownload.Text = "Download");
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            SaveSettings();
            _statusTimer.Stop();
            _statusTimer.Dispose();
            _cpuCounter?.Dispose();
            _downloadCts?.Cancel();
            _http?.Stop();
            _telegram?.Stop();
            _pipe?.Stop();
            _processor.Dispose();
            _helper.Dispose();
            _ocr?.Dispose();
            base.OnFormClosed(e);
        }

        private async void runAIComputerUseToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using var modelDlg = new OpenFileDialog
            { Filter = "GGUF Model|*.gguf", Title = "Select LLM Model (.gguf)" };
            if (modelDlg.ShowDialog() != DialogResult.OK) return;

            using var projDlg = new OpenFileDialog
            { Filter = "GGUF Projector|*.gguf", Title = "Select Multimodal Projector (.gguf)" };
            if (projDlg.ShowDialog() != DialogResult.OK) return;

            await MtmdInteractiveModeExecute.RunComputerUseMode(modelDlg.FileName, projDlg.FileName);
        }

        private void outputUiMapToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Sync the GUI tab's window to the processor whenever they differ
            if (_targetWindow != null)
            {
                var targetHwnd = _targetWindow.Properties.NativeWindowHandle.ValueOrDefault;
                var processorHwnd = _processor.CurrentWindow?.Properties.NativeWindowHandle.ValueOrDefault ?? IntPtr.Zero;
                if (targetHwnd != processorHwnd)
                    _processor.Process(new CommandRequest { Command = "find", Window = _targetWindow.Name });
            }

            var response = _processor.Process(new CommandRequest { Command = "elements" });
            Log(response.ToText());
        }




        private UiMapRenderer CreateUiMapRenderer() => new(includedControlTypes: new[] {
        "Button", "Document", "Text", "Window", "Pane", "MenuItem", "TitleBar",
        "CheckBox", "ComboBox", "DataGrid", "Edit", "Group", "Hyperlink", "List",
        "ListItem", "Menu", "MenuBar", "Slider", "Spinner", "StatusBar", "ScrollBar",
        "Tab", "ToolTip", "ToolBar", "TabItem", "Image", "AppBar", "Calendar",
        "Custom", "DataItem", "Header", "HeaderItem", "ProgressBar", "RadioButton",
        "SemanticZoom", "Separator", "SplitButton", "Table", "Thumb", "Tree",
        "TreeItem", "Unknown"
    });

        private void renderTestToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Sync the GUI tab's selected window to the processor if they differ
            if (_targetWindow != null)
            {
                var targetHwnd = _targetWindow.Properties.NativeWindowHandle.ValueOrDefault;
                var processorHwnd = _processor.CurrentWindow?.Properties.NativeWindowHandle.ValueOrDefault ?? IntPtr.Zero;
                if (targetHwnd != processorHwnd)
                    _processor.Process(new CommandRequest { Command = "find", Window = _targetWindow.Name });
            }

            var response = _processor.Process(new CommandRequest { Command = "elements" });
            if (!response.Success || string.IsNullOrWhiteSpace(response.Data))
            {
                Log("Render UI Map: no elements — select a window first.");
                return;
            }

            string json = response.Data;
            var renderer = CreateUiMapRenderer();
            string winName = _processor.CurrentWindow?.Properties.Name.ValueOrDefault ?? "window";
            string safeName = string.Concat(winName.Split(Path.GetInvalidFileNameChars()));

            using var dlg = new SaveFileDialog
            {
                Title = "Save UI Map Image",
                Filter = "PNG Image (*.png)|*.png",
                FileName = $"ui_map_{safeName}"
            };

            if (dlg.ShowDialog() == DialogResult.OK)
            {
                var screen = Screen.PrimaryScreen?.Bounds ?? new Rectangle(0, 0, 1920, 1080);
                renderer.Render(json, dlg.FileName, screen.Width, screen.Height);
                Log($"UI Map saved: {dlg.FileName}");
            }

            renderer.ShowOverlay(json, 5000);
        }

        private void sceneEditorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var editor = new SceneEditorForm(_sceneStore);
            editor.Show(this);
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }
    }
}
