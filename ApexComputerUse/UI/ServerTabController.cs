namespace ApexComputerUse
{
    internal sealed class ServerTabController : IDisposable
    {
        private readonly CommandProcessor _processor;
        private readonly SceneStore _sceneStore;
        private readonly AiChatService _chatService;
        private readonly Action<string> _logHandler;
        private readonly Action<string> _log;

        private readonly TextBox _txtHttpPort, _txtApiKey, _txtPipeName, _txtBotToken, _txtAllowedChatIds;
        private readonly Button _btnStartHttp, _btnStartPipe, _btnStartTelegram;
        private readonly Label _lblHttpStatus, _lblPipeStatus, _lblTelegramStatus;

        public HttpCommandServer? Http { get; private set; }
        public PipeCommandServer? Pipe { get; private set; }
        public TelegramController? Telegram { get; private set; }

        internal ServerTabController(
            CommandProcessor processor, SceneStore sceneStore, AiChatService chatService,
            Action<string> logHandler, Action<string> log,
            TextBox txtHttpPort, TextBox txtApiKey,
            TextBox txtPipeName, TextBox txtBotToken, TextBox txtAllowedChatIds,
            Button btnStartHttp, Button btnStartPipe, Button btnStartTelegram,
            Label lblHttpStatus, Label lblPipeStatus, Label lblTelegramStatus)
        {
            _processor = processor; _sceneStore = sceneStore; _chatService = chatService;
            _logHandler = logHandler; _log = log;
            _txtHttpPort = txtHttpPort; _txtApiKey = txtApiKey;
            _txtPipeName = txtPipeName; _txtBotToken = txtBotToken; _txtAllowedChatIds = txtAllowedChatIds;
            _btnStartHttp = btnStartHttp; _btnStartPipe = btnStartPipe; _btnStartTelegram = btnStartTelegram;
            _lblHttpStatus = lblHttpStatus; _lblPipeStatus = lblPipeStatus; _lblTelegramStatus = lblTelegramStatus;
        }

        internal void ToggleHttp()
        {
            if (Http?.IsRunning == true)
            {
                Http.Stop();
                _btnStartHttp.Text = "Start HTTP";
                _lblHttpStatus.Text = "Stopped";
                _lblHttpStatus.ForeColor = Color.Gray;
                return;
            }

            if (!int.TryParse(_txtHttpPort.Text.Trim(), out int port) || port < 1 || port > 65535)
            {
                _log("Invalid port number.");
                return;
            }

            try
            {
                string apiKey = _txtApiKey.Text.Trim();
                var cfg = AppConfig.Current;
                Http = new HttpCommandServer(port, _processor, _sceneStore, _chatService, apiKey,
                           enableShellRun: cfg.EnableShellRun, bindAll: cfg.HttpBindAll,
                           testRunnerExePath: cfg.TestRunnerExePath,
                           testRunnerConfigPath: cfg.TestRunnerConfigPath);
                Http.OnLog += _logHandler;
                Http.OnShutdownRequested += () =>
                {
                    // Marshal to the UI thread so Application.Exit is called from the message loop.
                    try
                    {
                        if (System.Windows.Forms.Application.OpenForms.Count > 0)
                        {
                            var form = System.Windows.Forms.Application.OpenForms[0]!;
                            form.BeginInvoke(() => System.Windows.Forms.Application.Exit());
                        }
                        else
                        {
                            System.Windows.Forms.Application.Exit();
                        }
                    }
                    catch { Environment.Exit(0); }
                };
                Http.Start();
                _btnStartHttp.Text = "Stop HTTP";
                string authNote = string.IsNullOrWhiteSpace(apiKey) ? " (no auth)" : " (auth enabled)";
                _lblHttpStatus.Text = $"Listening :{port}{authNote}";
                _lblHttpStatus.ForeColor = string.IsNullOrWhiteSpace(apiKey) ? Color.DarkOrange : Color.Green;
            }
            catch (Exception ex) { _log($"HTTP start error: {ex.Message}"); }
        }

        internal void TogglePipe()
        {
            if (Pipe?.IsRunning == true)
            {
                Pipe.Stop();
                _btnStartPipe.Text = "Start Pipe";
                _lblPipeStatus.Text = "Stopped";
                _lblPipeStatus.ForeColor = Color.Gray;
                return;
            }

            string name = _txtPipeName.Text.Trim();
            if (string.IsNullOrWhiteSpace(name)) { _log("Enter a pipe name."); return; }

            try
            {
                Pipe = new PipeCommandServer(name, _processor);
                Pipe.OnLog += _logHandler;
                Pipe.Start();
                _btnStartPipe.Text = "Stop Pipe";
                _lblPipeStatus.Text = $"Running: {name}";
                _lblPipeStatus.ForeColor = Color.Green;
            }
            catch (Exception ex) { _log($"Pipe start error: {ex.Message}"); }
        }

        internal void ToggleTelegram()
        {
            if (Telegram?.IsRunning == true)
            {
                Telegram.Stop();
                _btnStartTelegram.Text = "Start Telegram";
                _lblTelegramStatus.Text = "Telegram: Stopped";
                _lblTelegramStatus.ForeColor = Color.Gray;
                return;
            }

            string token = _txtBotToken.Text.Trim();
            if (string.IsNullOrWhiteSpace(token)) { _log("Enter a Telegram bot token."); return; }

            try
            {
                var allowedIds = _txtAllowedChatIds.Text
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(s => long.TryParse(s, out long id) ? (long?)id : null)
                    .Where(id => id.HasValue)
                    .Select(id => id!.Value)
                    .ToList();

                Telegram = new TelegramController(token, _processor, allowedIds.Count > 0 ? allowedIds : null);
                Telegram.OnLog += _logHandler;
                Telegram.Start();
                _btnStartTelegram.Text = "Stop Telegram";
                string authNote = allowedIds.Count > 0
                    ? $" ({allowedIds.Count} allowed chat(s))"
                    : " (no chat ID filter)";
                _lblTelegramStatus.Text = $"Telegram: Running{authNote}";
                _lblTelegramStatus.ForeColor = allowedIds.Count > 0 ? Color.Green : Color.DarkOrange;
            }
            catch (Exception ex) { _log($"Telegram start error: {ex.Message}"); }
        }

        internal void CopyApiKey()
        {
            string key = _txtApiKey.Text.Trim();
            if (string.IsNullOrWhiteSpace(key)) { _log("API key is empty."); return; }
            Clipboard.SetText(key);
            _log("API key copied to clipboard.");
        }

        public void Dispose()
        {
            // Unsubscribe before Stop to avoid in-flight log callbacks racing the dispose.
            if (Http     != null) { Http.OnLog     -= _logHandler; Http.Stop(); }
            if (Pipe     != null) { Pipe.OnLog     -= _logHandler; Pipe.Stop(); }
            if (Telegram != null) { Telegram.OnLog -= _logHandler; Telegram.Stop(); }
        }
    }
}
