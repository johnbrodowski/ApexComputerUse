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
        private readonly CommandProcessor  _processor  = new();
        private readonly CommandDispatcher _dispatcher;
        private readonly SceneStore _sceneStore = new();
        private readonly AiChatService _chatService = new();
        private OcrHelper? _ocr;
        private HttpCommandServer? _http;
        private TelegramController? _telegram;
        private PipeCommandServer? _pipe;

        /// <summary>
        /// Shared log-forwarding delegate for processor + all I/O servers.
        /// Captured as a field so it can be unsubscribed symmetrically on Stop / FormClosed —
        /// if a server outlives the form and fires OnLog, BeginInvoke on a disposed form would throw.
        /// Self-guards against a disposed form as a belt-and-suspenders against race windows.
        /// </summary>
        private readonly Action<string> _logHandler;

        // ── Status bar ────────────────────────────────────────────────────
        private readonly System.Windows.Forms.Timer _statusTimer = new() { Interval = 2000 };
        private System.Diagnostics.PerformanceCounter? _cpuCounter;
        private long _netBytesPrev = -1;

        // Cached set of up-adapters for the status-bar Net counter. Enumerating
        // NetworkInterface.GetAllNetworkInterfaces() costs ~10ms on machines with many NICs,
        // and the status timer ticks every 2s on the UI thread; refresh only when the OS
        // reports an address change rather than re-enumerating every tick.
        private NetworkInterface[] _cachedNics = Array.Empty<NetworkInterface>();
        private readonly NetworkAddressChangedEventHandler _nicChangedHandler;

        private readonly DownloadManager _downloader = new();

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
            catch (Exception ex) { AppLog.Warning($"LoadSettings: settings file appears corrupt and was ignored — {ex.Message}"); }
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
            catch (Exception ex) { AppLog.Warning($"SaveSettings: failed to write settings — {ex.Message}"); }
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
            _dispatcher        = new CommandDispatcher(_processor);
            _nicChangedHandler = (_, _) => RefreshNicCache();

            InitializeComponent();

            _logHandler = msg =>
            {
                // Always forward to the persistent app log; it has no UI coupling.
                AppLog.FromOnLog(msg);
                if (IsDisposed || Disposing || !IsHandleCreated) return;
                try { BeginInvoke(() => Log(msg)); }
                catch (ObjectDisposedException) { /* form closed between check and call */ }
                catch (InvalidOperationException) { /* handle destroyed between check and call */ }
            };

            // Route CommandProcessor logs to the status box
            _processor.OnLog += _logHandler;

            // Action control-type picker
            cmbControlType.Items.AddRange(ControlActions.Keys.ToArray<object>());
            cmbControlType.SelectedIndex = 0;

            // Search-type filter: "All" + every ControlType except Unknown
            cmbSearchType.Items.Add("All");
            foreach (ControlType ct in Enum.GetValues<ControlType>())
                if (ct != ControlType.Unknown)
                    cmbSearchType.Items.Add(ct.ToString());
            cmbSearchType.SelectedIndex = 0;

            // Seed NIC cache and refresh only when the OS reports address changes.
            RefreshNicCache();
            NetworkChange.NetworkAddressChanged += _nicChangedHandler;

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

            // Populate the Chat tab provider list and defaults
            InitChatTab();

            // First-launch hint: if the default model files are absent, go to the Model tab
            this.Load += (_, _) => { WireDownloader(); CheckFirstLaunch(); };

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

            var req = CommandLineParser.Parse(input);
            if (req == null) { Log($"Unknown command. Type 'help' for a list."); return; }

            try
            {
                var response = _dispatcher.Dispatch(req);
                Log(response.ToText());
            }
            catch (Exception ex) { Log($"Error: {ex.Message}"); }
        }

        // ── Find ──────────────────────────────────────────────────────────

        private void btnFind_Click(object sender, EventArgs e)
        {
            _processor.SetCurrentTarget(null, null);
            try
            {
                string title = txtWindowName.Text.Trim();
                if (string.IsNullOrEmpty(title)) { Log("Enter a Window Name."); return; }

                // Fuzzy window find
                var window = _helper.FindWindowFuzzy(title, out string matchedTitle, out bool windowExact);
                if (window == null) { Log($"No window found for \"{title}\"."); return; }

                if (!windowExact)
                {
                    var answer = MessageBox.Show(
                        $"No exact window match.\nUse closest match?\n\n\"{matchedTitle}\"",
                        "Closest Window Match", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (answer == DialogResult.No) { Log("Window match rejected."); return; }
                }
                Log($"Window ({(windowExact ? "exact" : "fuzzy")}): \"{window.Name}\"");

                string autoId = txtElementId.Text.Trim();
                string name = txtElementName.Text.Trim();

                // If no element search term, target the window itself
                if (string.IsNullOrEmpty(autoId) && string.IsNullOrEmpty(name))
                {
                    _processor.SetCurrentTarget(window, window);
                    Log($"Targeting window: {_helper.Describe(window)}");
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
                    window, searchVal, filterType, searchById,
                    out string matchedValue, out bool elementExact);

                if (el == null)
                {
                    _processor.SetCurrentTarget(window, null);
                    Log($"No element found for \"{searchVal}\".");
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
                        Log("Element match rejected.");
                        return;
                    }
                }

                _processor.SetCurrentTarget(window, el);
                Log($"Element ({(elementExact ? "exact" : "fuzzy")}): {_helper.Describe(el)}");
            }
            catch (Exception ex) { Log($"Error: {ex.Message}"); }
        }

        // ── Execute ───────────────────────────────────────────────────────

        private void btnExecute_Click(object sender, EventArgs e)
        {
            var element = _processor.CurrentElement;
            if (element == null) { Log("Find an element first."); return; }
            if (!CommandProcessor.IsElementValid(element))
            {
                Log("Element is no longer available (target app closed or changed). Run 'Find' again.");
                _processor.SetCurrentTarget(_processor.CurrentWindow, null);
                return;
            }

            string action = cmbAction.SelectedItem?.ToString() ?? "";
            string input = txtInput.Text.Trim();
            try
            {
                string result = ExecuteAction(element, action, input);
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
                _http = new HttpCommandServer(port, _processor, _sceneStore, _chatService, apiKey,
                            enableShellRun: appCfg.EnableShellRun,
                            bindAll: appCfg.HttpBindAll);
                _http.OnLog += _logHandler;
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
                _pipe.OnLog += _logHandler;
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
                _telegram.OnLog += _logHandler;
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

        /// <summary>
        /// Top-level exception boundary for <c>async void</c> event handlers.
        /// Unhandled exceptions in async void fire-and-forget tasks otherwise propagate
        /// through the synchronization context and can tear down the process.
        /// </summary>
        private async Task SafeRun(Func<Task> fn, string? context = null)
        {
            try { await fn(); }
            catch (OperationCanceledException) { /* user cancellation — not an error */ }
            catch (Exception ex)
            {
                string tag = context ?? "handler";
                Log($"[{tag}] Unhandled: {ex.Message}");
                AppLog.FromOnLog($"[{tag}] {ex}");
            }
        }

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

            // Network (total bytes/sec across all adapters).
            // Iterates the cached adapter array; refreshed on NetworkAddressChanged.
            try
            {
                long totalBytes = 0;
                foreach (var nic in _cachedNics)
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

        private void RefreshNicCache()
        {
            try { _cachedNics = NetworkInterface.GetAllNetworkInterfaces(); }
            catch { _cachedNics = Array.Empty<NetworkInterface>(); }
            // Force recalc of the bytes delta — counters on the new adapter set aren't
            // comparable to the old totals, so skip this tick's kbps display.
            _netBytesPrev = -1;
        }

        // ── Chat tab ─────────────────────────────────────────────────────

        private void InitChatTab()
        {
            cboAiProvider.Items.AddRange(_chatService.RegisteredProviders.Cast<object>().ToArray());

            // Select the default provider without triggering SelectedIndexChanged yet
            var idx = cboAiProvider.Items.IndexOf(_chatService.CurrentProvider);
            cboAiProvider.SelectedIndex = idx >= 0 ? idx : 0;

            LoadChatProviderFields(_chatService.CurrentProvider);
            lblAiSettingsPath.Text = $"Settings: {AiMessagingCore.Configuration.AiSettings.DefaultFilePath}";
        }

        private void LoadChatProviderFields(string provider)
        {
            var defaults = _chatService.GetProviderDefaults(provider);
            txtAiModel.Text         = defaults?.Model ?? "";
            txtAiSystemPrompt.Text  = defaults?.SystemPrompt ?? "";
            txtAiApiKey.Text        = _chatService.GetApiKey(provider);
        }

        private void cboAiProvider_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cboAiProvider.SelectedItem?.ToString() is string p)
                LoadChatProviderFields(p);
        }

        private void btnAiSaveSettings_Click(object sender, EventArgs e)
        {
            if (cboAiProvider.SelectedItem?.ToString() is not string provider) return;
            _chatService.ApplySettings(
                provider,
                txtAiModel.Text.Trim(),
                txtAiSystemPrompt.Text.Trim(),
                txtAiApiKey.Text.Trim());
            lblAiSessionStatus.Text      = "Settings saved — next message starts a new session.";
            lblAiSessionStatus.ForeColor = Color.Green;
        }

        private void btnAiOpenChat_Click(object sender, EventArgs e)
        {
            if (_http?.IsRunning != true)
            {
                Log("Start the HTTP server first (Remote Control tab).");
                return;
            }
            var port = _http.Port;
            var key  = txtApiKey.Text.Trim();
            // Pass the key via the URL fragment so the page can auto-authenticate.
            var url  = $"http://localhost:{port}/chat#{Uri.EscapeDataString(key)}";
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true }); }
            catch (Exception ex) { Log($"Could not open browser: {ex.Message}"); }
        }

        private void btnAiResetChat_Click(object sender, EventArgs e)
        {
            _chatService.ResetSession();
            lblAiSessionStatus.Text      = "Conversation reset.";
            lblAiSessionStatus.ForeColor = Color.Gray;
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

        private void CheckFirstLaunch()
        {
            bool anyMissing = DownloadManager.SetupFiles.Any(f => !File.Exists(f.RelPath));
            if (!anyMissing) return;

            // Switch to Model tab so the user sees the Download All button
            tabMain.SelectedTab = tabPageModel;
            lblDownloadStatus.ForeColor = Color.DarkBlue;
            lblDownloadStatus.Text = "First launch — click \"Download All\" to set up models and tessdata.";
        }

        private void WireDownloader()
        {
            _downloader.Status += (msg, col) => BeginInvoke(() =>
            {
                lblDownloadStatus.Text = msg;
                lblDownloadStatus.ForeColor = col;
            });
            _downloader.Progress += pct => BeginInvoke(() => pbarDownload.Value = pct);
        }

        private async void btnDownloadAll_Click(object sender, EventArgs e)
        {
            if (_downloader.IsRunning) { _downloader.Cancel(); return; }

            btnDownloadAll.Text = "Cancel";
            btnDownload.Enabled = false;
            pbarDownload.Value = 0;

            try
            {
                bool ok = await _downloader.RunSetupAsync();
                if (ok)
                {
                    var files = DownloadManager.SetupFiles;
                    txtModelPath.Text = files[0].RelPath;
                    txtProjPath.Text  = files[1].RelPath;
                    SaveSettings();
                    Log($"Setup complete. Model: {files[0].RelPath}");
                    Log($"Projector: {files[1].RelPath}");
                    Log($"Tessdata:  {files[2].RelPath}");
                }
            }
            finally
            {
                btnDownloadAll.Text = "Download All  (LFM2.5-VL model + projector + tessdata)";
                btnDownload.Enabled = true;
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
            if (_downloader.IsRunning) { _downloader.Cancel(); return; }

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
            btnDownload.Text = "Cancel";
            pbarDownload.Value = 0;

            try
            {
                bool ok = await _downloader.DownloadAsync(url, destPath);
                if (ok)
                {
                    txtProjPath.Text = destPath;
                    Log($"Vision model downloaded to: {destPath}");
                }
            }
            finally
            {
                btnDownload.Text = "Download";
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            SaveSettings();
            _statusTimer.Stop();
            _statusTimer.Dispose();
            _cpuCounter?.Dispose();
            NetworkChange.NetworkAddressChanged -= _nicChangedHandler;
            _downloader.Cancel();

            // Unsubscribe before Stop so any in-flight log callbacks don't race the dispose.
            _processor.OnLog -= _logHandler;
            if (_http     != null) _http.OnLog     -= _logHandler;
            if (_pipe     != null) _pipe.OnLog     -= _logHandler;
            if (_telegram != null) _telegram.OnLog -= _logHandler;

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

            await SafeRun(
                () => MtmdInteractiveModeExecute.RunComputerUseMode(modelDlg.FileName, projDlg.FileName),
                context: "AI Computer Use");
        }

        private void outputUiMapToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var response = _dispatcher.Dispatch(new CommandRequest { Command = "elements" });
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
            var response = _dispatcher.Dispatch(new CommandRequest { Command = "elements" });
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
            _ = new SceneChatAgent(editor, _sceneStore, _chatService);
            editor.Show(this);
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }
    }
}
