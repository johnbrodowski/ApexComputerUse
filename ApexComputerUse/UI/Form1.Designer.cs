namespace ApexComputerUse
{
    partial class Form1
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            menuStrip1 = new MenuStrip();
            toolsToolStripMenuItem = new ToolStripMenuItem();
            runAIComputerUseToolStripMenuItem = new ToolStripMenuItem();
            outputUiMapToolStripMenuItem = new ToolStripMenuItem();
            renderTestToolStripMenuItem = new ToolStripMenuItem();
            sceneEditorToolStripMenuItem = new ToolStripMenuItem();
            tabPageChat = new TabPage();
            grpAiSettings = new GroupBox();
            lblAiProvider = new Label();
            cboAiProvider = new ComboBox();
            lblAiModel = new Label();
            txtAiModel = new TextBox();
            lblAiApiKey = new Label();
            txtAiApiKey = new TextBox();
            lblAiSysPrompt = new Label();
            txtAiSystemPrompt = new TextBox();
            btnAiSaveSettings = new Button();
            lblAiSettingsPath = new Label();
            grpAiSession = new GroupBox();
            lblAiSessionStatus = new Label();
            btnAiOpenChat = new Button();
            btnAiResetChat = new Button();
            tabMain = new TabControl();
            tabPageConsole = new TabPage();
            txtCommand = new TextBox();
            btnRun = new Button();
            btnClear = new Button();
            lblStatus = new Label();
            txtStatus = new TextBox();
            tabPageFind = new TabPage();
            lblWindowName = new Label();
            txtWindowName = new TextBox();
            lblElementId = new Label();
            txtElementId = new TextBox();
            lblSearchType = new Label();
            cmbSearchType = new ComboBox();
            lblElementName = new Label();
            txtElementName = new TextBox();
            lblControlType = new Label();
            cmbControlType = new ComboBox();
            lblAction = new Label();
            cmbAction = new ComboBox();
            lblInput = new Label();
            txtInput = new TextBox();
            btnFind = new Button();
            btnExecute = new Button();
            tabPageRemote = new TabPage();
            grpRemote = new GroupBox();
            lblHttpPort = new Label();
            txtHttpPort = new TextBox();
            btnStartHttp = new Button();
            lblHttpStatus = new Label();
            lblApiKey = new Label();
            txtApiKey = new TextBox();
            btnCopyApiKey = new Button();
            lblBotToken = new Label();
            txtBotToken = new TextBox();
            btnStartTelegram = new Button();
            lblTelegramStatus = new Label();
            lblAllowedChatIds = new Label();
            txtAllowedChatIds = new TextBox();
            lblPipeName = new Label();
            txtPipeName = new TextBox();
            btnStartPipe = new Button();
            lblPipeStatus = new Label();
            tabPageModel = new TabPage();
            grpModelPaths = new GroupBox();
            lblModelPath = new Label();
            txtModelPath = new TextBox();
            btnBrowseModel = new Button();
            lblProjPath = new Label();
            txtProjPath = new TextBox();
            btnBrowseProj = new Button();
            btnLoadModel = new Button();
            lblModelStatus = new Label();
            grpDownload = new GroupBox();
            lblDownloadUrl = new Label();
            txtDownloadUrl = new TextBox();
            pbarDownload = new ProgressBar();
            lblDownloadStatus = new Label();
            btnDownload = new Button();
            btnDownloadAll = new Button();
            statusStrip1 = new StatusStrip();
            lblStatCpu = new ToolStripStatusLabel();
            lblStatRam = new ToolStripStatusLabel();
            lblStatModel = new ToolStripStatusLabel();
            lblStatNet = new ToolStripStatusLabel();
            menuStrip1.SuspendLayout();
            tabPageChat.SuspendLayout();
            grpAiSettings.SuspendLayout();
            grpAiSession.SuspendLayout();
            tabMain.SuspendLayout();
            tabPageConsole.SuspendLayout();
            tabPageFind.SuspendLayout();
            tabPageRemote.SuspendLayout();
            grpRemote.SuspendLayout();
            tabPageModel.SuspendLayout();
            grpModelPaths.SuspendLayout();
            grpDownload.SuspendLayout();
            statusStrip1.SuspendLayout();
            SuspendLayout();
            // 
            // menuStrip1
            // 
            menuStrip1.Items.AddRange(new ToolStripItem[] { toolsToolStripMenuItem });
            menuStrip1.Location = new Point(0, 0);
            menuStrip1.Name = "menuStrip1";
            menuStrip1.Size = new Size(577, 25);
            menuStrip1.TabIndex = 0;
            menuStrip1.Text = "menuStrip1";
            // 
            // toolsToolStripMenuItem
            // 
            toolsToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { runAIComputerUseToolStripMenuItem, outputUiMapToolStripMenuItem, renderTestToolStripMenuItem, sceneEditorToolStripMenuItem });
            toolsToolStripMenuItem.Name = "toolsToolStripMenuItem";
            toolsToolStripMenuItem.Size = new Size(51, 21);
            toolsToolStripMenuItem.Text = "Tools";
            // 
            // runAIComputerUseToolStripMenuItem
            // 
            runAIComputerUseToolStripMenuItem.Name = "runAIComputerUseToolStripMenuItem";
            runAIComputerUseToolStripMenuItem.Size = new Size(240, 22);
            runAIComputerUseToolStripMenuItem.Text = "Run AI Computer Use Mode";
            runAIComputerUseToolStripMenuItem.Click += runAIComputerUseToolStripMenuItem_Click;
            // 
            // outputUiMapToolStripMenuItem
            // 
            outputUiMapToolStripMenuItem.Name = "outputUiMapToolStripMenuItem";
            outputUiMapToolStripMenuItem.Size = new Size(240, 22);
            outputUiMapToolStripMenuItem.Text = "Output UI Map";
            outputUiMapToolStripMenuItem.Click += outputUiMapToolStripMenuItem_Click;
            // 
            // renderTestToolStripMenuItem
            // 
            renderTestToolStripMenuItem.Name = "renderTestToolStripMenuItem";
            renderTestToolStripMenuItem.Size = new Size(240, 22);
            renderTestToolStripMenuItem.Text = "RenderTest";
            renderTestToolStripMenuItem.Click += renderTestToolStripMenuItem_Click;
            // 
            // sceneEditorToolStripMenuItem
            // 
            sceneEditorToolStripMenuItem.Name = "sceneEditorToolStripMenuItem";
            sceneEditorToolStripMenuItem.Size = new Size(240, 22);
            sceneEditorToolStripMenuItem.Text = "Scene Editor";
            sceneEditorToolStripMenuItem.Click += sceneEditorToolStripMenuItem_Click;
            // 
            // tabPageChat
            // 
            tabPageChat.Controls.Add(grpAiSettings);
            tabPageChat.Controls.Add(grpAiSession);
            tabPageChat.Location = new Point(4, 26);
            tabPageChat.Name = "tabPageChat";
            tabPageChat.Padding = new Padding(3);
            tabPageChat.Size = new Size(565, 337);
            tabPageChat.TabIndex = 4;
            tabPageChat.Text = "Chat";
            tabPageChat.UseVisualStyleBackColor = true;
            // 
            // grpAiSettings
            // 
            grpAiSettings.Controls.Add(lblAiProvider);
            grpAiSettings.Controls.Add(cboAiProvider);
            grpAiSettings.Controls.Add(lblAiModel);
            grpAiSettings.Controls.Add(txtAiModel);
            grpAiSettings.Controls.Add(lblAiApiKey);
            grpAiSettings.Controls.Add(txtAiApiKey);
            grpAiSettings.Controls.Add(lblAiSysPrompt);
            grpAiSettings.Controls.Add(txtAiSystemPrompt);
            grpAiSettings.Controls.Add(btnAiSaveSettings);
            grpAiSettings.Controls.Add(lblAiSettingsPath);
            grpAiSettings.Location = new Point(8, 7);
            grpAiSettings.Name = "grpAiSettings";
            grpAiSettings.Size = new Size(520, 236);
            grpAiSettings.TabIndex = 0;
            grpAiSettings.TabStop = false;
            grpAiSettings.Text = "Provider Settings";
            // 
            // lblAiProvider
            // 
            lblAiProvider.AutoSize = true;
            lblAiProvider.Location = new Point(8, 29);
            lblAiProvider.Name = "lblAiProvider";
            lblAiProvider.Size = new Size(60, 17);
            lblAiProvider.TabIndex = 0;
            lblAiProvider.Text = "Provider:";
            // 
            // cboAiProvider
            // 
            cboAiProvider.DropDownStyle = ComboBoxStyle.DropDownList;
            cboAiProvider.FormattingEnabled = true;
            cboAiProvider.Location = new Point(90, 25);
            cboAiProvider.Name = "cboAiProvider";
            cboAiProvider.Size = new Size(160, 25);
            cboAiProvider.TabIndex = 1;
            cboAiProvider.SelectedIndexChanged += cboAiProvider_SelectedIndexChanged;
            // 
            // lblAiModel
            // 
            lblAiModel.AutoSize = true;
            lblAiModel.Location = new Point(8, 66);
            lblAiModel.Name = "lblAiModel";
            lblAiModel.Size = new Size(49, 17);
            lblAiModel.TabIndex = 2;
            lblAiModel.Text = "Model:";
            // 
            // txtAiModel
            // 
            txtAiModel.Location = new Point(90, 61);
            txtAiModel.Name = "txtAiModel";
            txtAiModel.Size = new Size(360, 25);
            txtAiModel.TabIndex = 3;
            // 
            // lblAiApiKey
            // 
            lblAiApiKey.AutoSize = true;
            lblAiApiKey.Location = new Point(8, 104);
            lblAiApiKey.Name = "lblAiApiKey";
            lblAiApiKey.Size = new Size(54, 17);
            lblAiApiKey.TabIndex = 4;
            lblAiApiKey.Text = "API Key:";
            // 
            // txtAiApiKey
            // 
            txtAiApiKey.Location = new Point(90, 100);
            txtAiApiKey.Name = "txtAiApiKey";
            txtAiApiKey.PasswordChar = '●';
            txtAiApiKey.Size = new Size(360, 25);
            txtAiApiKey.TabIndex = 5;
            // 
            // lblAiSysPrompt
            // 
            lblAiSysPrompt.AutoSize = true;
            lblAiSysPrompt.Location = new Point(8, 143);
            lblAiSysPrompt.Name = "lblAiSysPrompt";
            lblAiSysPrompt.Size = new Size(52, 17);
            lblAiSysPrompt.TabIndex = 6;
            lblAiSysPrompt.Text = "System:";
            // 
            // txtAiSystemPrompt
            // 
            txtAiSystemPrompt.Location = new Point(90, 138);
            txtAiSystemPrompt.Multiline = true;
            txtAiSystemPrompt.Name = "txtAiSystemPrompt";
            txtAiSystemPrompt.Size = new Size(360, 52);
            txtAiSystemPrompt.TabIndex = 7;
            // 
            // btnAiSaveSettings
            // 
            btnAiSaveSettings.Location = new Point(456, 25);
            btnAiSaveSettings.Name = "btnAiSaveSettings";
            btnAiSaveSettings.Size = new Size(56, 29);
            btnAiSaveSettings.TabIndex = 8;
            btnAiSaveSettings.Text = "Save";
            btnAiSaveSettings.UseVisualStyleBackColor = true;
            btnAiSaveSettings.Click += btnAiSaveSettings_Click;
            // 
            // lblAiSettingsPath
            // 
            lblAiSettingsPath.AutoSize = true;
            lblAiSettingsPath.ForeColor = Color.Gray;
            lblAiSettingsPath.Location = new Point(8, 209);
            lblAiSettingsPath.Name = "lblAiSettingsPath";
            lblAiSettingsPath.Size = new Size(95, 17);
            lblAiSettingsPath.TabIndex = 9;
            lblAiSettingsPath.Text = "ai-settings.json";
            // 
            // grpAiSession
            // 
            grpAiSession.Controls.Add(lblAiSessionStatus);
            grpAiSession.Controls.Add(btnAiOpenChat);
            grpAiSession.Controls.Add(btnAiResetChat);
            grpAiSession.Location = new Point(8, 252);
            grpAiSession.Name = "grpAiSession";
            grpAiSession.Size = new Size(520, 75);
            grpAiSession.TabIndex = 1;
            grpAiSession.TabStop = false;
            grpAiSession.Text = "Chat";
            // 
            // lblAiSessionStatus
            // 
            lblAiSessionStatus.AutoSize = true;
            lblAiSessionStatus.ForeColor = Color.Gray;
            lblAiSessionStatus.Location = new Point(8, 27);
            lblAiSessionStatus.Name = "lblAiSessionStatus";
            lblAiSessionStatus.Size = new Size(110, 17);
            lblAiSessionStatus.TabIndex = 0;
            lblAiSessionStatus.Text = "No active session";
            // 
            // btnAiOpenChat
            // 
            btnAiOpenChat.Location = new Point(276, 23);
            btnAiOpenChat.Name = "btnAiOpenChat";
            btnAiOpenChat.Size = new Size(120, 29);
            btnAiOpenChat.TabIndex = 1;
            btnAiOpenChat.Text = "Open in Browser";
            btnAiOpenChat.UseVisualStyleBackColor = true;
            btnAiOpenChat.Click += btnAiOpenChat_Click;
            // 
            // btnAiResetChat
            // 
            btnAiResetChat.Location = new Point(402, 23);
            btnAiResetChat.Name = "btnAiResetChat";
            btnAiResetChat.Size = new Size(110, 29);
            btnAiResetChat.TabIndex = 2;
            btnAiResetChat.Text = "Reset Conversation";
            btnAiResetChat.UseVisualStyleBackColor = true;
            btnAiResetChat.Click += btnAiResetChat_Click;
            // 
            // tabMain
            // 
            tabMain.Controls.Add(tabPageConsole);
            tabMain.Controls.Add(tabPageFind);
            tabMain.Controls.Add(tabPageRemote);
            tabMain.Controls.Add(tabPageModel);
            tabMain.Controls.Add(tabPageChat);
            tabMain.Location = new Point(0, 27);
            tabMain.Name = "tabMain";
            tabMain.SelectedIndex = 0;
            tabMain.Size = new Size(573, 367);
            tabMain.TabIndex = 1;
            // 
            // tabPageConsole
            // 
            tabPageConsole.Controls.Add(txtCommand);
            tabPageConsole.Controls.Add(btnRun);
            tabPageConsole.Controls.Add(btnClear);
            tabPageConsole.Controls.Add(lblStatus);
            tabPageConsole.Controls.Add(txtStatus);
            tabPageConsole.Location = new Point(4, 26);
            tabPageConsole.Name = "tabPageConsole";
            tabPageConsole.Padding = new Padding(3);
            tabPageConsole.Size = new Size(565, 337);
            tabPageConsole.TabIndex = 0;
            tabPageConsole.Text = "Console";
            tabPageConsole.UseVisualStyleBackColor = true;
            // 
            // txtCommand
            // 
            txtCommand.Location = new Point(8, 9);
            txtCommand.Name = "txtCommand";
            txtCommand.PlaceholderText = "find window=… | exec action=… | ocr | ai … | status | windows | help";
            txtCommand.Size = new Size(447, 25);
            txtCommand.TabIndex = 0;
            txtCommand.KeyDown += txtCommand_KeyDown;
            // 
            // btnRun
            // 
            btnRun.Location = new Point(463, 8);
            btnRun.Name = "btnRun";
            btnRun.Size = new Size(30, 28);
            btnRun.TabIndex = 1;
            btnRun.Text = "▶";
            btnRun.Click += btnRun_Click;
            // 
            // btnClear
            // 
            btnClear.Location = new Point(501, 8);
            btnClear.Name = "btnClear";
            btnClear.Size = new Size(26, 28);
            btnClear.TabIndex = 2;
            btnClear.Text = "×";
            btnClear.Click += btnClear_Click;
            // 
            // lblStatus
            // 
            lblStatus.AutoSize = true;
            lblStatus.Location = new Point(8, 38);
            lblStatus.Name = "lblStatus";
            lblStatus.Size = new Size(51, 17);
            lblStatus.TabIndex = 3;
            lblStatus.Text = "Output:";
            // 
            // txtStatus
            // 
            txtStatus.BackColor = Color.WhiteSmoke;
            txtStatus.Location = new Point(8, 62);
            txtStatus.Multiline = true;
            txtStatus.Name = "txtStatus";
            txtStatus.ReadOnly = true;
            txtStatus.ScrollBars = ScrollBars.Vertical;
            txtStatus.Size = new Size(549, 269);
            txtStatus.TabIndex = 4;
            // 
            // tabPageFind
            // 
            tabPageFind.Controls.Add(lblWindowName);
            tabPageFind.Controls.Add(txtWindowName);
            tabPageFind.Controls.Add(lblElementId);
            tabPageFind.Controls.Add(txtElementId);
            tabPageFind.Controls.Add(lblSearchType);
            tabPageFind.Controls.Add(cmbSearchType);
            tabPageFind.Controls.Add(lblElementName);
            tabPageFind.Controls.Add(txtElementName);
            tabPageFind.Controls.Add(lblControlType);
            tabPageFind.Controls.Add(cmbControlType);
            tabPageFind.Controls.Add(lblAction);
            tabPageFind.Controls.Add(cmbAction);
            tabPageFind.Controls.Add(lblInput);
            tabPageFind.Controls.Add(txtInput);
            tabPageFind.Controls.Add(btnFind);
            tabPageFind.Controls.Add(btnExecute);
            tabPageFind.Location = new Point(4, 26);
            tabPageFind.Name = "tabPageFind";
            tabPageFind.Padding = new Padding(3);
            tabPageFind.Size = new Size(565, 337);
            tabPageFind.TabIndex = 1;
            tabPageFind.Text = "Find & Execute";
            tabPageFind.UseVisualStyleBackColor = true;
            // 
            // lblWindowName
            // 
            lblWindowName.AutoSize = true;
            lblWindowName.Location = new Point(8, 18);
            lblWindowName.Name = "lblWindowName";
            lblWindowName.Size = new Size(97, 17);
            lblWindowName.TabIndex = 0;
            lblWindowName.Text = "Window Name:";
            // 
            // txtWindowName
            // 
            txtWindowName.Location = new Point(130, 15);
            txtWindowName.Name = "txtWindowName";
            txtWindowName.Size = new Size(393, 25);
            txtWindowName.TabIndex = 0;
            // 
            // lblElementId
            // 
            lblElementId.AutoSize = true;
            lblElementId.Location = new Point(8, 57);
            lblElementId.Name = "lblElementId";
            lblElementId.Size = new Size(89, 17);
            lblElementId.TabIndex = 1;
            lblElementId.Text = "AutomationId:";
            // 
            // txtElementId
            // 
            txtElementId.Location = new Point(130, 53);
            txtElementId.Name = "txtElementId";
            txtElementId.Size = new Size(155, 25);
            txtElementId.TabIndex = 1;
            // 
            // lblSearchType
            // 
            lblSearchType.AutoSize = true;
            lblSearchType.Location = new Point(295, 57);
            lblSearchType.Name = "lblSearchType";
            lblSearchType.Size = new Size(81, 17);
            lblSearchType.TabIndex = 2;
            lblSearchType.Text = "Search Type:";
            // 
            // cmbSearchType
            // 
            cmbSearchType.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbSearchType.Location = new Point(378, 53);
            cmbSearchType.Name = "cmbSearchType";
            cmbSearchType.Size = new Size(145, 25);
            cmbSearchType.TabIndex = 2;
            // 
            // lblElementName
            // 
            lblElementName.AutoSize = true;
            lblElementName.Location = new Point(8, 95);
            lblElementName.Name = "lblElementName";
            lblElementName.Size = new Size(96, 17);
            lblElementName.TabIndex = 3;
            lblElementName.Text = "Element Name:";
            // 
            // txtElementName
            // 
            txtElementName.Location = new Point(130, 92);
            txtElementName.Name = "txtElementName";
            txtElementName.Size = new Size(393, 25);
            txtElementName.TabIndex = 3;
            // 
            // lblControlType
            // 
            lblControlType.AutoSize = true;
            lblControlType.Location = new Point(8, 134);
            lblControlType.Name = "lblControlType";
            lblControlType.Size = new Size(85, 17);
            lblControlType.TabIndex = 4;
            lblControlType.Text = "Control Type:";
            // 
            // cmbControlType
            // 
            cmbControlType.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbControlType.Location = new Point(130, 130);
            cmbControlType.Name = "cmbControlType";
            cmbControlType.Size = new Size(155, 25);
            cmbControlType.TabIndex = 4;
            cmbControlType.SelectedIndexChanged += cmbControlType_SelectedIndexChanged;
            // 
            // lblAction
            // 
            lblAction.AutoSize = true;
            lblAction.Location = new Point(295, 134);
            lblAction.Name = "lblAction";
            lblAction.Size = new Size(47, 17);
            lblAction.TabIndex = 5;
            lblAction.Text = "Action:";
            // 
            // cmbAction
            // 
            cmbAction.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbAction.Location = new Point(348, 130);
            cmbAction.Name = "cmbAction";
            cmbAction.Size = new Size(175, 25);
            cmbAction.TabIndex = 5;
            // 
            // lblInput
            // 
            lblInput.AutoSize = true;
            lblInput.Location = new Point(8, 172);
            lblInput.Name = "lblInput";
            lblInput.Size = new Size(86, 17);
            lblInput.TabIndex = 6;
            lblInput.Text = "Value / Index:";
            // 
            // txtInput
            // 
            txtInput.Location = new Point(130, 169);
            txtInput.Name = "txtInput";
            txtInput.Size = new Size(393, 25);
            txtInput.TabIndex = 6;
            // 
            // btnFind
            // 
            btnFind.Location = new Point(130, 211);
            btnFind.Name = "btnFind";
            btnFind.Size = new Size(110, 32);
            btnFind.TabIndex = 7;
            btnFind.Text = "Find Element";
            btnFind.Click += btnFind_Click;
            // 
            // btnExecute
            // 
            btnExecute.Location = new Point(248, 211);
            btnExecute.Name = "btnExecute";
            btnExecute.Size = new Size(120, 32);
            btnExecute.TabIndex = 8;
            btnExecute.Text = "Execute Action";
            btnExecute.Click += btnExecute_Click;
            // 
            // tabPageRemote
            // 
            tabPageRemote.Controls.Add(grpRemote);
            tabPageRemote.Location = new Point(4, 26);
            tabPageRemote.Name = "tabPageRemote";
            tabPageRemote.Padding = new Padding(3);
            tabPageRemote.Size = new Size(565, 337);
            tabPageRemote.TabIndex = 2;
            tabPageRemote.Text = "Remote Control";
            tabPageRemote.UseVisualStyleBackColor = true;
            // 
            // grpRemote
            // 
            grpRemote.Controls.Add(lblHttpPort);
            grpRemote.Controls.Add(txtHttpPort);
            grpRemote.Controls.Add(btnStartHttp);
            grpRemote.Controls.Add(lblHttpStatus);
            grpRemote.Controls.Add(lblApiKey);
            grpRemote.Controls.Add(txtApiKey);
            grpRemote.Controls.Add(btnCopyApiKey);
            grpRemote.Controls.Add(lblBotToken);
            grpRemote.Controls.Add(txtBotToken);
            grpRemote.Controls.Add(btnStartTelegram);
            grpRemote.Controls.Add(lblTelegramStatus);
            grpRemote.Controls.Add(lblAllowedChatIds);
            grpRemote.Controls.Add(txtAllowedChatIds);
            grpRemote.Controls.Add(lblPipeName);
            grpRemote.Controls.Add(txtPipeName);
            grpRemote.Controls.Add(btnStartPipe);
            grpRemote.Controls.Add(lblPipeStatus);
            grpRemote.Location = new Point(8, 9);
            grpRemote.Name = "grpRemote";
            grpRemote.Size = new Size(519, 262);
            grpRemote.TabIndex = 0;
            grpRemote.TabStop = false;
            grpRemote.Text = "Remote Control";
            // 
            // lblHttpPort
            // 
            lblHttpPort.AutoSize = true;
            lblHttpPort.Location = new Point(8, 29);
            lblHttpPort.Name = "lblHttpPort";
            lblHttpPort.Size = new Size(69, 17);
            lblHttpPort.TabIndex = 0;
            lblHttpPort.Text = "HTTP Port:";
            // 
            // txtHttpPort
            // 
            txtHttpPort.Location = new Point(82, 26);
            txtHttpPort.Name = "txtHttpPort";
            txtHttpPort.Size = new Size(55, 25);
            txtHttpPort.TabIndex = 1;
            txtHttpPort.Text = "8081";
            // 
            // btnStartHttp
            // 
            btnStartHttp.Location = new Point(146, 25);
            btnStartHttp.Name = "btnStartHttp";
            btnStartHttp.Size = new Size(90, 29);
            btnStartHttp.TabIndex = 2;
            btnStartHttp.Text = "Start HTTP";
            btnStartHttp.Click += btnStartHttp_Click;
            // 
            // lblHttpStatus
            // 
            lblHttpStatus.AutoSize = true;
            lblHttpStatus.ForeColor = Color.Gray;
            lblHttpStatus.Location = new Point(246, 29);
            lblHttpStatus.Name = "lblHttpStatus";
            lblHttpStatus.Size = new Size(58, 17);
            lblHttpStatus.TabIndex = 3;
            lblHttpStatus.Text = "Stopped";
            // 
            // lblApiKey
            // 
            lblApiKey.AutoSize = true;
            lblApiKey.Location = new Point(8, 66);
            lblApiKey.Name = "lblApiKey";
            lblApiKey.Size = new Size(54, 17);
            lblApiKey.TabIndex = 12;
            lblApiKey.Text = "API Key:";
            // 
            // txtApiKey
            // 
            txtApiKey.Location = new Point(82, 62);
            txtApiKey.Name = "txtApiKey";
            txtApiKey.PlaceholderText = "(leave blank to disable auth)";
            txtApiKey.Size = new Size(349, 25);
            txtApiKey.TabIndex = 13;
            // 
            // btnCopyApiKey
            // 
            btnCopyApiKey.Location = new Point(439, 61);
            btnCopyApiKey.Name = "btnCopyApiKey";
            btnCopyApiKey.Size = new Size(52, 29);
            btnCopyApiKey.TabIndex = 14;
            btnCopyApiKey.Text = "Copy";
            btnCopyApiKey.Click += btnCopyApiKey_Click;
            // 
            // lblBotToken
            // 
            lblBotToken.AutoSize = true;
            lblBotToken.Location = new Point(8, 111);
            lblBotToken.Name = "lblBotToken";
            lblBotToken.Size = new Size(68, 17);
            lblBotToken.TabIndex = 4;
            lblBotToken.Text = "Bot Token:";
            // 
            // txtBotToken
            // 
            txtBotToken.Location = new Point(82, 108);
            txtBotToken.Name = "txtBotToken";
            txtBotToken.PlaceholderText = "123456:ABC-DEF...";
            txtBotToken.Size = new Size(265, 25);
            txtBotToken.TabIndex = 5;
            // 
            // btnStartTelegram
            // 
            btnStartTelegram.Location = new Point(357, 107);
            btnStartTelegram.Name = "btnStartTelegram";
            btnStartTelegram.Size = new Size(120, 29);
            btnStartTelegram.TabIndex = 6;
            btnStartTelegram.Text = "Start Telegram";
            btnStartTelegram.Click += btnStartTelegram_Click;
            // 
            // lblTelegramStatus
            // 
            lblTelegramStatus.AutoSize = true;
            lblTelegramStatus.ForeColor = Color.Gray;
            lblTelegramStatus.Location = new Point(8, 176);
            lblTelegramStatus.Name = "lblTelegramStatus";
            lblTelegramStatus.Size = new Size(119, 17);
            lblTelegramStatus.TabIndex = 7;
            lblTelegramStatus.Text = "Telegram: Stopped";
            // 
            // lblAllowedChatIds
            // 
            lblAllowedChatIds.AutoSize = true;
            lblAllowedChatIds.Location = new Point(8, 145);
            lblAllowedChatIds.Name = "lblAllowedChatIds";
            lblAllowedChatIds.Size = new Size(59, 17);
            lblAllowedChatIds.TabIndex = 15;
            lblAllowedChatIds.Text = "Chat IDs:";
            // 
            // txtAllowedChatIds
            // 
            txtAllowedChatIds.Location = new Point(82, 142);
            txtAllowedChatIds.Name = "txtAllowedChatIds";
            txtAllowedChatIds.PlaceholderText = "123456789,987654321 (leave blank to allow all)";
            txtAllowedChatIds.Size = new Size(395, 25);
            txtAllowedChatIds.TabIndex = 16;
            // 
            // lblPipeName
            // 
            lblPipeName.AutoSize = true;
            lblPipeName.Location = new Point(8, 221);
            lblPipeName.Name = "lblPipeName";
            lblPipeName.Size = new Size(75, 17);
            lblPipeName.TabIndex = 8;
            lblPipeName.Text = "Pipe Name:";
            // 
            // txtPipeName
            // 
            txtPipeName.Location = new Point(82, 218);
            txtPipeName.Name = "txtPipeName";
            txtPipeName.Size = new Size(120, 25);
            txtPipeName.TabIndex = 9;
            txtPipeName.Text = "ApexComputerUse";
            // 
            // btnStartPipe
            // 
            btnStartPipe.Location = new Point(211, 216);
            btnStartPipe.Name = "btnStartPipe";
            btnStartPipe.Size = new Size(90, 29);
            btnStartPipe.TabIndex = 10;
            btnStartPipe.Text = "Start Pipe";
            btnStartPipe.Click += btnStartPipe_Click;
            // 
            // lblPipeStatus
            // 
            lblPipeStatus.AutoSize = true;
            lblPipeStatus.ForeColor = Color.Gray;
            lblPipeStatus.Location = new Point(311, 221);
            lblPipeStatus.Name = "lblPipeStatus";
            lblPipeStatus.Size = new Size(58, 17);
            lblPipeStatus.TabIndex = 11;
            lblPipeStatus.Text = "Stopped";
            // 
            // tabPageModel
            // 
            tabPageModel.Controls.Add(grpModelPaths);
            tabPageModel.Controls.Add(grpDownload);
            tabPageModel.Location = new Point(4, 26);
            tabPageModel.Name = "tabPageModel";
            tabPageModel.Padding = new Padding(3);
            tabPageModel.Size = new Size(565, 337);
            tabPageModel.TabIndex = 3;
            tabPageModel.Text = "Model";
            tabPageModel.UseVisualStyleBackColor = true;
            // 
            // grpModelPaths
            // 
            grpModelPaths.Controls.Add(lblModelPath);
            grpModelPaths.Controls.Add(txtModelPath);
            grpModelPaths.Controls.Add(btnBrowseModel);
            grpModelPaths.Controls.Add(lblProjPath);
            grpModelPaths.Controls.Add(txtProjPath);
            grpModelPaths.Controls.Add(btnBrowseProj);
            grpModelPaths.Controls.Add(btnLoadModel);
            grpModelPaths.Controls.Add(lblModelStatus);
            grpModelPaths.Location = new Point(8, 9);
            grpModelPaths.Name = "grpModelPaths";
            grpModelPaths.Size = new Size(519, 141);
            grpModelPaths.TabIndex = 0;
            grpModelPaths.TabStop = false;
            grpModelPaths.Text = "LLM Model";
            // 
            // lblModelPath
            // 
            lblModelPath.AutoSize = true;
            lblModelPath.Location = new Point(8, 29);
            lblModelPath.Name = "lblModelPath";
            lblModelPath.Size = new Size(49, 17);
            lblModelPath.TabIndex = 0;
            lblModelPath.Text = "Model:";
            // 
            // txtModelPath
            // 
            txtModelPath.Location = new Point(70, 26);
            txtModelPath.Name = "txtModelPath";
            txtModelPath.PlaceholderText = "Path to LLM .gguf file";
            txtModelPath.Size = new Size(367, 25);
            txtModelPath.TabIndex = 1;
            // 
            // btnBrowseModel
            // 
            btnBrowseModel.Location = new Point(442, 25);
            btnBrowseModel.Name = "btnBrowseModel";
            btnBrowseModel.Size = new Size(70, 28);
            btnBrowseModel.TabIndex = 2;
            btnBrowseModel.Text = "Browse...";
            btnBrowseModel.Click += btnBrowseModel_Click;
            // 
            // lblProjPath
            // 
            lblProjPath.AutoSize = true;
            lblProjPath.Location = new Point(8, 68);
            lblProjPath.Name = "lblProjPath";
            lblProjPath.Size = new Size(64, 17);
            lblProjPath.TabIndex = 3;
            lblProjPath.Text = "Projector:";
            // 
            // txtProjPath
            // 
            txtProjPath.Location = new Point(70, 65);
            txtProjPath.Name = "txtProjPath";
            txtProjPath.PlaceholderText = "Path to mmproj .gguf file (vision)";
            txtProjPath.Size = new Size(367, 25);
            txtProjPath.TabIndex = 4;
            // 
            // btnBrowseProj
            // 
            btnBrowseProj.Location = new Point(442, 63);
            btnBrowseProj.Name = "btnBrowseProj";
            btnBrowseProj.Size = new Size(70, 28);
            btnBrowseProj.TabIndex = 5;
            btnBrowseProj.Text = "Browse...";
            btnBrowseProj.Click += btnBrowseProj_Click;
            // 
            // btnLoadModel
            // 
            btnLoadModel.Location = new Point(65, 102);
            btnLoadModel.Name = "btnLoadModel";
            btnLoadModel.Size = new Size(100, 29);
            btnLoadModel.TabIndex = 6;
            btnLoadModel.Text = "Load Model";
            btnLoadModel.Click += btnLoadModel_Click;
            // 
            // lblModelStatus
            // 
            lblModelStatus.AutoSize = true;
            lblModelStatus.ForeColor = Color.Gray;
            lblModelStatus.Location = new Point(175, 108);
            lblModelStatus.Name = "lblModelStatus";
            lblModelStatus.Size = new Size(75, 17);
            lblModelStatus.TabIndex = 7;
            lblModelStatus.Text = "Not loaded";
            // 
            // grpDownload
            // 
            grpDownload.Controls.Add(lblDownloadUrl);
            grpDownload.Controls.Add(txtDownloadUrl);
            grpDownload.Controls.Add(pbarDownload);
            grpDownload.Controls.Add(lblDownloadStatus);
            grpDownload.Controls.Add(btnDownload);
            grpDownload.Controls.Add(btnDownloadAll);
            grpDownload.Location = new Point(8, 161);
            grpDownload.Name = "grpDownload";
            grpDownload.Size = new Size(519, 170);
            grpDownload.TabIndex = 1;
            grpDownload.TabStop = false;
            grpDownload.Text = "Vision Model Download";
            // 
            // lblDownloadUrl
            // 
            lblDownloadUrl.AutoSize = true;
            lblDownloadUrl.Location = new Point(8, 29);
            lblDownloadUrl.Name = "lblDownloadUrl";
            lblDownloadUrl.Size = new Size(34, 17);
            lblDownloadUrl.TabIndex = 0;
            lblDownloadUrl.Text = "URL:";
            // 
            // txtDownloadUrl
            // 
            txtDownloadUrl.Location = new Point(42, 26);
            txtDownloadUrl.Name = "txtDownloadUrl";
            txtDownloadUrl.PlaceholderText = "https://huggingface.co/.../mmproj-model.gguf";
            txtDownloadUrl.Size = new Size(469, 25);
            txtDownloadUrl.TabIndex = 1;
            // 
            // pbarDownload
            // 
            pbarDownload.Location = new Point(8, 66);
            pbarDownload.Name = "pbarDownload";
            pbarDownload.Size = new Size(503, 23);
            pbarDownload.TabIndex = 2;
            // 
            // lblDownloadStatus
            // 
            lblDownloadStatus.AutoSize = true;
            lblDownloadStatus.ForeColor = Color.Gray;
            lblDownloadStatus.Location = new Point(8, 100);
            lblDownloadStatus.Name = "lblDownloadStatus";
            lblDownloadStatus.Size = new Size(47, 17);
            lblDownloadStatus.TabIndex = 3;
            lblDownloadStatus.Text = "Ready.";
            // 
            // btnDownload
            // 
            btnDownload.Location = new Point(430, 95);
            btnDownload.Name = "btnDownload";
            btnDownload.Size = new Size(80, 29);
            btnDownload.TabIndex = 4;
            btnDownload.Text = "Download";
            btnDownload.Click += btnDownload_Click;
            // 
            // btnDownloadAll
            // 
            btnDownloadAll.Location = new Point(8, 131);
            btnDownloadAll.Name = "btnDownloadAll";
            btnDownloadAll.Size = new Size(503, 29);
            btnDownloadAll.TabIndex = 5;
            btnDownloadAll.Text = "Download All  (LFM2.5-VL model + projector + tessdata)";
            btnDownloadAll.Click += btnDownloadAll_Click;
            // 
            // statusStrip1
            // 
            statusStrip1.Items.AddRange(new ToolStripItem[] { lblStatCpu, lblStatRam, lblStatModel, lblStatNet });
            statusStrip1.Location = new Point(0, 397);
            statusStrip1.Name = "statusStrip1";
            statusStrip1.Size = new Size(577, 26);
            statusStrip1.TabIndex = 2;
            statusStrip1.Text = "statusStrip1";
            // 
            // lblStatCpu
            // 
            lblStatCpu.BorderSides = ToolStripStatusLabelBorderSides.Right;
            lblStatCpu.Name = "lblStatCpu";
            lblStatCpu.Size = new Size(64, 21);
            lblStatCpu.Text = "CPU: --%";
            // 
            // lblStatRam
            // 
            lblStatRam.BorderSides = ToolStripStatusLabelBorderSides.Right;
            lblStatRam.Name = "lblStatRam";
            lblStatRam.Size = new Size(80, 21);
            lblStatRam.Text = "RAM: -- MB";
            // 
            // lblStatModel
            // 
            lblStatModel.BorderSides = ToolStripStatusLabelBorderSides.Right;
            lblStatModel.Name = "lblStatModel";
            lblStatModel.Size = new Size(67, 21);
            lblStatModel.Text = "Model: --";
            // 
            // lblStatNet
            // 
            lblStatNet.Name = "lblStatNet";
            lblStatNet.Size = new Size(351, 21);
            lblStatNet.Spring = true;
            lblStatNet.Text = "Net: --";
            lblStatNet.TextAlign = ContentAlignment.MiddleRight;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 17F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(577, 423);
            Controls.Add(tabMain);
            Controls.Add(statusStrip1);
            Controls.Add(menuStrip1);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MainMenuStrip = menuStrip1;
            MaximizeBox = false;
            Name = "Form1";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "ApexComputerUse";
            Load += Form1_Load;
            menuStrip1.ResumeLayout(false);
            menuStrip1.PerformLayout();
            tabPageChat.ResumeLayout(false);
            grpAiSettings.ResumeLayout(false);
            grpAiSettings.PerformLayout();
            grpAiSession.ResumeLayout(false);
            grpAiSession.PerformLayout();
            tabMain.ResumeLayout(false);
            tabPageConsole.ResumeLayout(false);
            tabPageConsole.PerformLayout();
            tabPageFind.ResumeLayout(false);
            tabPageFind.PerformLayout();
            tabPageRemote.ResumeLayout(false);
            grpRemote.ResumeLayout(false);
            grpRemote.PerformLayout();
            tabPageModel.ResumeLayout(false);
            grpModelPaths.ResumeLayout(false);
            grpModelPaths.PerformLayout();
            grpDownload.ResumeLayout(false);
            grpDownload.PerformLayout();
            statusStrip1.ResumeLayout(false);
            statusStrip1.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        // ── Always visible ────────────────────────────────────────────────
        private MenuStrip                      menuStrip1;
        private ToolStripMenuItem              toolsToolStripMenuItem;
        private ToolStripMenuItem              runAIComputerUseToolStripMenuItem;
        private ToolStripMenuItem              outputUiMapToolStripMenuItem;
        private System.Windows.Forms.TabControl tabMain;
        private System.Windows.Forms.TabPage    tabPageConsole;
        private System.Windows.Forms.TabPage    tabPageFind;
        private System.Windows.Forms.TabPage    tabPageRemote;

        // ── Console tab ───────────────────────────────────────────────────
        private System.Windows.Forms.TextBox   txtCommand;
        private System.Windows.Forms.Button    btnRun;
        private System.Windows.Forms.Button    btnClear;
        private System.Windows.Forms.Label     lblStatus;
        private System.Windows.Forms.TextBox   txtStatus;

        // ── Find & Execute tab ────────────────────────────────────────────
        private System.Windows.Forms.Label     lblWindowName;
        private System.Windows.Forms.TextBox   txtWindowName;
        private System.Windows.Forms.Label     lblElementId;
        private System.Windows.Forms.TextBox   txtElementId;
        private System.Windows.Forms.Label     lblElementName;
        private System.Windows.Forms.TextBox   txtElementName;
        private System.Windows.Forms.Label     lblSearchType;
        private System.Windows.Forms.ComboBox  cmbSearchType;
        private System.Windows.Forms.Label     lblControlType;
        private System.Windows.Forms.ComboBox  cmbControlType;
        private System.Windows.Forms.Label     lblAction;
        private System.Windows.Forms.ComboBox  cmbAction;
        private System.Windows.Forms.Label     lblInput;
        private System.Windows.Forms.TextBox   txtInput;
        private System.Windows.Forms.Button    btnFind;
        private System.Windows.Forms.Button    btnExecute;

        // ── Model tab ─────────────────────────────────────────────────────
        private System.Windows.Forms.TabPage   tabPageModel;
        private System.Windows.Forms.GroupBox  grpModelPaths;
        private System.Windows.Forms.Label     lblModelPath;
        private System.Windows.Forms.TextBox   txtModelPath;
        private System.Windows.Forms.Button    btnBrowseModel;
        private System.Windows.Forms.Label     lblProjPath;
        private System.Windows.Forms.TextBox   txtProjPath;
        private System.Windows.Forms.Button    btnBrowseProj;
        private System.Windows.Forms.Button    btnLoadModel;
        private System.Windows.Forms.Label     lblModelStatus;
        private System.Windows.Forms.GroupBox  grpDownload;
        private System.Windows.Forms.Label     lblDownloadUrl;
        private System.Windows.Forms.TextBox   txtDownloadUrl;
        private System.Windows.Forms.ProgressBar pbarDownload;
        private System.Windows.Forms.Label     lblDownloadStatus;
        private System.Windows.Forms.Button    btnDownload;
        private System.Windows.Forms.Button    btnDownloadAll;

        // ── Status strip ──────────────────────────────────────────────────
        private System.Windows.Forms.StatusStrip statusStrip1;
        private System.Windows.Forms.ToolStripStatusLabel lblStatCpu;
        private System.Windows.Forms.ToolStripStatusLabel lblStatRam;
        private System.Windows.Forms.ToolStripStatusLabel lblStatModel;
        private System.Windows.Forms.ToolStripStatusLabel lblStatNet;

        // ── Remote Control tab ────────────────────────────────────────────
        private System.Windows.Forms.GroupBox  grpRemote;
        private System.Windows.Forms.Label     lblHttpPort;
        private System.Windows.Forms.TextBox   txtHttpPort;
        private System.Windows.Forms.Button    btnStartHttp;
        private System.Windows.Forms.Label     lblHttpStatus;
        private System.Windows.Forms.Label     lblApiKey;
        private System.Windows.Forms.TextBox   txtApiKey;
        private System.Windows.Forms.Button    btnCopyApiKey;
        private System.Windows.Forms.Label     lblBotToken;
        private System.Windows.Forms.TextBox   txtBotToken;
        private System.Windows.Forms.Button    btnStartTelegram;
        private System.Windows.Forms.Label     lblTelegramStatus;
        private System.Windows.Forms.Label     lblAllowedChatIds;
        private System.Windows.Forms.TextBox   txtAllowedChatIds;
        private System.Windows.Forms.Label     lblPipeName;
        private System.Windows.Forms.TextBox   txtPipeName;
        private System.Windows.Forms.Button    btnStartPipe;
        private System.Windows.Forms.Label     lblPipeStatus;
        private ToolStripMenuItem renderTestToolStripMenuItem;
        private ToolStripMenuItem sceneEditorToolStripMenuItem;
        private TabPage tabPageChat;
        private GroupBox grpAiSettings;
        private Label lblAiProvider;
        private ComboBox cboAiProvider;
        private Label lblAiModel;
        private TextBox txtAiModel;
        private Label lblAiApiKey;
        private TextBox txtAiApiKey;
        private Label lblAiSysPrompt;
        private TextBox txtAiSystemPrompt;
        private Button btnAiSaveSettings;
        private Label lblAiSettingsPath;
        private GroupBox grpAiSession;
        private Label lblAiSessionStatus;
        private Button btnAiOpenChat;
        private Button btnAiResetChat;
    }
}
