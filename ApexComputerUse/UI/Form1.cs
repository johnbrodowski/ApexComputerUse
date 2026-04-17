using System.Text.Json;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;

namespace ApexComputerUse
{
    public partial class Form1 : Form
    {
        private readonly ApexHelper _helper = new();
        private readonly CommandProcessor _processor = new();
        private readonly CommandDispatcher _dispatcher;
        private readonly SceneStore _sceneStore = new();
        private readonly AiChatService _chatService = new();
        private readonly DownloadManager _downloader = new();
        private readonly ActionExecutor _executor;
        private readonly StatusMonitor _statusMonitor;
        private readonly ServerTabController _servers;
        private readonly ChatTabController _chat;
        private readonly ModelTabController _model;

        /// <summary>
        /// Shared log-forwarding delegate for processor + all I/O servers.
        /// Captured as a field so it can be unsubscribed symmetrically on Stop / FormClosed —
        /// if a server outlives the form and fires OnLog, BeginInvoke on a disposed form would throw.
        /// Self-guards against a disposed form as a belt-and-suspenders against race windows.
        /// </summary>
        private readonly Action<string> _logHandler;

        // ── Persistent settings ───────────────────────────────────────────
        private static readonly string SettingsDir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ApexComputerUse");
        private static readonly string SettingsFile = Path.Combine(SettingsDir, "settings.json");

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
            _dispatcher = new CommandDispatcher(_processor);

            InitializeComponent();

            _logHandler = msg =>
            {
                AppLog.FromOnLog(msg);
                if (IsDisposed || Disposing || !IsHandleCreated) return;
                try { BeginInvoke(() => Log(msg)); }
                catch (ObjectDisposedException) { /* form closed between check and call */ }
                catch (InvalidOperationException) { /* handle destroyed between check and call */ }
            };

            _processor.OnLog += _logHandler;

            _executor      = new ActionExecutor(_helper, _processor);
            _statusMonitor = new StatusMonitor(lblStatCpu, lblStatRam, lblStatModel, lblStatNet, _processor);

            _servers = new ServerTabController(
                _processor, _sceneStore, _chatService, _logHandler, Log,
                txtHttpPort, txtApiKey, txtPipeName, txtBotToken, txtAllowedChatIds,
                btnStartHttp, btnStartPipe, btnStartTelegram,
                lblHttpStatus, lblPipeStatus, lblTelegramStatus);

            _chat = new ChatTabController(
                _chatService, () => _servers.Http, () => txtApiKey.Text, Log,
                cboAiProvider, txtAiModel, txtAiSystemPrompt, txtAiApiKey,
                lblAiSettingsPath, lblAiSessionStatus);

            _model = new ModelTabController(
                _processor, _downloader, SaveSettings, Log,
                txtModelPath, txtProjPath, txtDownloadUrl,
                btnDownloadAll, btnDownload, btnLoadModel,
                lblDownloadStatus, lblModelStatus,
                pbarDownload, tabMain, tabPageModel);

            // Action control-type picker
            cmbControlType.Items.AddRange(ControlActions.Keys.ToArray<object>());
            cmbControlType.SelectedIndex = 0;

            // Search-type filter: "All" + every ControlType except Unknown
            cmbSearchType.Items.Add("All");
            foreach (ControlType ct in Enum.GetValues<ControlType>())
                if (ct != ControlType.Unknown)
                    cmbSearchType.Items.Add(ct.ToString());
            cmbSearchType.SelectedIndex = 0;

            _processor.SceneStore = _sceneStore;

            LoadSettings();
            _chat.Init();

            this.Load += (_, _) => { _model.WireDownloader(); _model.CheckFirstLaunch(); };
            this.Load += (_, _) => _servers.ToggleHttp();
        }

        private void LoadSettings()
        {
            var cfg = AppConfig.Current;
            if (cfg.HttpPort != 8081) txtHttpPort.Text = cfg.HttpPort.ToString();
            if (!string.IsNullOrWhiteSpace(cfg.PipeName) &&
                cfg.PipeName != "ApexComputerUse") txtPipeName.Text = cfg.PipeName;
            if (!string.IsNullOrWhiteSpace(cfg.ModelPath)) txtModelPath.Text = cfg.ModelPath;
            if (!string.IsNullOrWhiteSpace(cfg.MmProjPath)) txtProjPath.Text = cfg.MmProjPath;
            if (!string.IsNullOrWhiteSpace(cfg.ApiKey)) txtApiKey.Text = cfg.ApiKey;
            if (!string.IsNullOrWhiteSpace(cfg.AllowedChatIds)) txtAllowedChatIds.Text = cfg.AllowedChatIds;
            if (!string.IsNullOrWhiteSpace(cfg.TelegramToken)) txtBotToken.Text = cfg.TelegramToken;

            try
            {
                if (!File.Exists(SettingsFile))
                {
                    EnsureApiKey();
                    return;
                }
                var s = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsFile));
                if (s == null) return;
                if (!string.IsNullOrWhiteSpace(s.ModelPath)) txtModelPath.Text = s.ModelPath;
                if (!string.IsNullOrWhiteSpace(s.ProjPath)) txtProjPath.Text = s.ProjPath;
                if (!string.IsNullOrWhiteSpace(s.ApiKey)) txtApiKey.Text = s.ApiKey;
                else EnsureApiKey();
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
                    ModelPath      = txtModelPath.Text,
                    ProjPath       = txtProjPath.Text,
                    ApiKey         = txtApiKey.Text,
                    AllowedChatIds = txtAllowedChatIds.Text
                };
                File.WriteAllText(SettingsFile,
                    JsonSerializer.Serialize(s, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception ex) { AppLog.Warning($"SaveSettings: failed to write settings — {ex.Message}"); }
        }

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

        // ── Control-type picker ───────────────────────────────────────────

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
            if (e.KeyCode == Keys.Return) { RunCommand(); e.SuppressKeyPress = true; }
        }

        private void btnRun_Click(object sender, EventArgs e) => RunCommand();

        private void RunCommand()
        {
            string input = txtCommand.Text.Trim();
            if (string.IsNullOrEmpty(input)) return;
            var req = CommandLineParser.Parse(input);
            if (req == null) { Log($"Unknown command. Type 'help' for a list."); return; }
            try { Log(_dispatcher.Dispatch(req).ToText()); }
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
                string name   = txtElementName.Text.Trim();

                if (string.IsNullOrEmpty(autoId) && string.IsNullOrEmpty(name))
                {
                    _processor.SetCurrentTarget(window, window);
                    Log($"Targeting window: {_helper.Describe(window)}");
                    return;
                }

                ControlType? filterType = null;
                if (cmbSearchType.SelectedItem is string st && st != "All")
                    filterType = Enum.Parse<ControlType>(st);

                bool searchById  = !string.IsNullOrEmpty(autoId);
                string searchVal = searchById ? autoId : name;

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
            string input  = txtInput.Text.Trim();
            try
            {
                string result = _executor.Execute(element, action, input);
                Log(string.IsNullOrEmpty(result) ? $"'{action}' done." : $"Result: {result}");
            }
            catch (Exception ex) { Log($"Error: {ex.Message}"); }
        }

        // ── Server tab ────────────────────────────────────────────────────

        private void btnStartHttp_Click(object sender, EventArgs e)      => _servers.ToggleHttp();
        private void btnStartPipe_Click(object sender, EventArgs e)      => _servers.TogglePipe();
        private void btnStartTelegram_Click(object sender, EventArgs e)  => _servers.ToggleTelegram();
        private void btnCopyApiKey_Click(object sender, EventArgs e)     => _servers.CopyApiKey();

        // ── Chat tab ──────────────────────────────────────────────────────

        private void cboAiProvider_SelectedIndexChanged(object sender, EventArgs e) => _chat.ProviderChanged();
        private void btnAiSaveSettings_Click(object sender, EventArgs e) => _chat.SaveSettings();
        private void btnAiOpenChat_Click(object sender, EventArgs e)     => _chat.OpenChat();
        private void btnAiResetChat_Click(object sender, EventArgs e)    => _chat.ResetChat();

        // ── Model tab ─────────────────────────────────────────────────────

        private void btnBrowseModel_Click(object sender, EventArgs e)    => _model.BrowseModel();
        private void btnBrowseProj_Click(object sender, EventArgs e)     => _model.BrowseProj();
        private async void btnDownloadAll_Click(object sender, EventArgs e) => await _model.DownloadAll();
        private async void btnLoadModel_Click(object sender, EventArgs e)   => await _model.LoadModel();
        private async void btnDownload_Click(object sender, EventArgs e)    => await _model.Download();

        // ── Misc ──────────────────────────────────────────────────────────

        private void btnClear_Click(object sender, EventArgs e) => txtStatus.Clear();

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

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            SaveSettings();
            _processor.OnLog -= _logHandler;
            _servers.Dispose();   // unsubscribes log handlers and stops all servers
            _statusMonitor.Dispose();
            _downloader.Cancel();
            _processor.Dispose();
            _helper.Dispose();
            _executor.Dispose();
            base.OnFormClosed(e);
        }

        // ── Menu items ────────────────────────────────────────────────────

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

            string json     = response.Data;
            var renderer    = CreateUiMapRenderer();
            string winName  = _processor.CurrentWindow?.Properties.Name.ValueOrDefault ?? "window";
            string safeName = string.Concat(winName.Split(Path.GetInvalidFileNameChars()));

            using var dlg = new SaveFileDialog
            {
                Title    = "Save UI Map Image",
                Filter   = "PNG Image (*.png)|*.png",
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

        private void Form1_Load(object sender, EventArgs e) { }
    }
}
