using System.Diagnostics;

namespace ApexComputerUse
{
    internal sealed class ChatTabController
    {
        private const string DefaultSystemPrompt =
            "You are an AI assistant that controls Windows applications through the ApexComputerUse HTTP API.\n\n" +
            "To perform an action, write the curl command in your response text. " +
            "It will be detected, executed against the real running server, and the actual result shown to you. " +
            "Wait for results before continuing. Never invent or guess results.\n\n" +
            "Available commands (omit the API key header — it is added automatically):\n\n" +
            "  curl http://localhost:8080/windows.json\n" +
            "  curl http://localhost:8080/status.json\n" +
            "  curl \"http://localhost:8080/elements.json?onscreen=true\"\n" +
            "  curl -X POST http://localhost:8080/find -d '{\"window\":\"Title\",\"name\":\"Name\",\"type\":\"Edit\"}'\n" +
            "  curl -X POST http://localhost:8080/exec -d '{\"action\":\"click\"}'\n" +
            "  curl -X POST http://localhost:8080/exec -d '{\"action\":\"type\",\"value\":\"text\"}'\n" +
            "  curl -X POST http://localhost:8080/exec -d '{\"action\":\"gettext\"}'\n" +
            "  curl -X POST http://localhost:8080/exec -d '{\"action\":\"keys\",\"value\":\"{ENTER}\"}'\n" +
            "  curl -X POST http://localhost:8080/exec -d '{\"action\":\"highlight\"}'\n" +
            "  curl -X POST http://localhost:8080/exec -d '{\"action\":\"describe\"}'\n" +
            "  curl -X POST http://localhost:8080/capture -d '{\"action\":\"screen\"}'\n\n" +
            "Start every new task with: curl http://localhost:8080/windows.json";

        private readonly AiChatService _chatService;
        private readonly Func<HttpCommandServer?> _getHttp;
        private readonly Func<string> _getApiKey;
        private readonly Action<string> _log;

        private readonly ComboBox _cboAiProvider;
        private readonly TextBox _txtAiModel, _txtAiSystemPrompt, _txtAiApiKey;
        private readonly Label _lblAiSettingsPath;

        internal ChatTabController(
            AiChatService chatService,
            CommandProcessor processor,
            Func<HttpCommandServer?> getHttp,
            Func<string> getApiKey,
            Action<string> log,
            ComboBox cboAiProvider,
            TextBox txtAiModel, TextBox txtAiSystemPrompt, TextBox txtAiApiKey,
            Label lblAiSettingsPath)
        {
            _chatService       = chatService;
            _getHttp           = getHttp;
            _getApiKey         = getApiKey;
            _log               = log;
            _cboAiProvider     = cboAiProvider;
            _txtAiModel        = txtAiModel;
            _txtAiSystemPrompt = txtAiSystemPrompt;
            _txtAiApiKey       = txtAiApiKey;
            _lblAiSettingsPath = lblAiSettingsPath;
        }

        internal void Init()
        {
            _cboAiProvider.Items.AddRange(_chatService.RegisteredProviders.Cast<object>().ToArray());

            var idx = _cboAiProvider.Items.IndexOf(_chatService.CurrentProvider);
            _cboAiProvider.SelectedIndex = idx >= 0 ? idx : 0;

            LoadProviderFields(_chatService.CurrentProvider);
            _lblAiSettingsPath.Text = $"Settings: {AiMessagingCore.Configuration.AiSettings.DefaultFilePath}";

            // Always apply the Apex system prompt so the AI knows the available commands.
            // The textbox shows only the first line (single-line); the full prompt is kept in-memory.
            _txtAiSystemPrompt.Text = DefaultSystemPrompt.Split('\n')[0];
            if (_cboAiProvider.SelectedItem?.ToString() is string p)
                _chatService.ApplySettings(p, _txtAiModel.Text.Trim(), DefaultSystemPrompt, _txtAiApiKey.Text.Trim());
        }

        internal void ProviderChanged()
        {
            if (_cboAiProvider.SelectedItem?.ToString() is string p)
                LoadProviderFields(p);
        }

        internal void SaveSettings()
        {
            if (_cboAiProvider.SelectedItem?.ToString() is not string provider) return;
            // System prompt is always the full DefaultSystemPrompt (textbox is single-line display only).
            _chatService.ApplySettings(
                provider,
                _txtAiModel.Text.Trim(),
                DefaultSystemPrompt,
                _txtAiApiKey.Text.Trim());
        }

        internal void OpenChat()
        {
            var http = _getHttp();
            if (http?.IsRunning != true) { _log("Start the HTTP server first (Remote Control tab)."); return; }
            var url = $"http://localhost:{http.Port}/chat?apiKey={Uri.EscapeDataString(_getApiKey())}";
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                _log($"Failed to open browser: {ex.Message}");
            }
        }

        internal void ResetChat()
        {
            _chatService.ResetSession();
            _log("Chat session reset. Re-open chat in your default browser.");
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
