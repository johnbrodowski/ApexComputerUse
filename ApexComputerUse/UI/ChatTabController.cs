namespace ApexComputerUse
{
    internal sealed class ChatTabController
    {
        private readonly AiChatService _chatService;
        private readonly Func<HttpCommandServer?> _getHttp;
        private readonly Func<string> _getApiKey;
        private readonly Action<string> _log;

        private readonly ComboBox _cboAiProvider;
        private readonly TextBox _txtAiModel, _txtAiSystemPrompt, _txtAiApiKey;
        private readonly Label _lblAiSettingsPath, _lblAiSessionStatus;

        internal ChatTabController(
            AiChatService chatService,
            Func<HttpCommandServer?> getHttp,
            Func<string> getApiKey,
            Action<string> log,
            ComboBox cboAiProvider,
            TextBox txtAiModel, TextBox txtAiSystemPrompt, TextBox txtAiApiKey,
            Label lblAiSettingsPath, Label lblAiSessionStatus)
        {
            _chatService = chatService;
            _getHttp = getHttp;
            _getApiKey = getApiKey;
            _log = log;
            _cboAiProvider = cboAiProvider;
            _txtAiModel = txtAiModel; _txtAiSystemPrompt = txtAiSystemPrompt; _txtAiApiKey = txtAiApiKey;
            _lblAiSettingsPath = lblAiSettingsPath; _lblAiSessionStatus = lblAiSessionStatus;
        }

        internal void Init()
        {
            _cboAiProvider.Items.AddRange(_chatService.RegisteredProviders.Cast<object>().ToArray());

            var idx = _cboAiProvider.Items.IndexOf(_chatService.CurrentProvider);
            _cboAiProvider.SelectedIndex = idx >= 0 ? idx : 0;

            LoadProviderFields(_chatService.CurrentProvider);
            _lblAiSettingsPath.Text = $"Settings: {AiMessagingCore.Configuration.AiSettings.DefaultFilePath}";
        }

        internal void ProviderChanged()
        {
            if (_cboAiProvider.SelectedItem?.ToString() is string p)
                LoadProviderFields(p);
        }

        internal void SaveSettings()
        {
            if (_cboAiProvider.SelectedItem?.ToString() is not string provider) return;
            _chatService.ApplySettings(
                provider,
                _txtAiModel.Text.Trim(),
                _txtAiSystemPrompt.Text.Trim(),
                _txtAiApiKey.Text.Trim());
            _lblAiSessionStatus.Text      = "Settings saved — next message starts a new session.";
            _lblAiSessionStatus.ForeColor = Color.Green;
        }

        internal void OpenChat()
        {
            var http = _getHttp();
            if (http?.IsRunning != true) { _log("Start the HTTP server first (Remote Control tab)."); return; }
            var url = $"http://localhost:{http.Port}/chat#{Uri.EscapeDataString(_getApiKey())}";
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true }); }
            catch (Exception ex) { _log($"Could not open browser: {ex.Message}"); }
        }

        internal void ResetChat()
        {
            _chatService.ResetSession();
            _lblAiSessionStatus.Text      = "Conversation reset.";
            _lblAiSessionStatus.ForeColor = Color.Gray;
        }

        private void LoadProviderFields(string provider)
        {
            var defaults = _chatService.GetProviderDefaults(provider);
            _txtAiModel.Text        = defaults?.Model ?? "";
            _txtAiSystemPrompt.Text = defaults?.SystemPrompt ?? "";
            _txtAiApiKey.Text       = _chatService.GetApiKey(provider);
        }
    }
}
