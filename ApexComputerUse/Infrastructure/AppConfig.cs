using System.Diagnostics;
using System.Text.Json;

namespace ApexComputerUse
{
    /// <summary>
    /// Deployment-level configuration loaded from (in priority order):
    ///   1. Hardcoded defaults (below)
    ///   2. <c>appsettings.json</c> in the application directory
    ///   3. Environment variables prefixed with <c>APEX_</c>
    ///
    /// Environment variable names:
    ///   APEX_HTTP_PORT         int    HTTP listen port (default 8081)
    ///   APEX_HTTP_BIND_ALL     bool   Bind to all interfaces instead of localhost (default false)
    ///   APEX_HTTP_AUTOSTART    bool   Auto-start HTTP server in GUI mode (default false)
    ///   APEX_PIPE_NAME         string Named-pipe name (default ApexComputerUse)
    ///   APEX_LOG_LEVEL         string Serilog minimum level: Debug/Information/Warning/Error
    ///   APEX_ENABLE_SHELL_RUN  bool   Enable the /run shell-execution endpoint (default false)
    ///   APEX_MODEL_PATH        string Default LLM model .gguf path
    ///   APEX_MMPROJ_PATH       string Default multimodal projector .gguf path
    ///   APEX_API_KEY           string HTTP API key (overrides auto-generated key)
    ///   APEX_ALLOWED_CHAT_IDS  string Comma-separated Telegram chat IDs whitelist
    ///   APEX_TELEGRAM_TOKEN    string Telegram bot token
    ///
    /// User preference overrides (model paths, API key, etc.) are stored separately
    /// in <c>%APPDATA%\ApexComputerUse\settings.json</c> and take highest priority in the GUI.
    /// </summary>
    internal sealed record AppConfig
    {
        public int    HttpPort        { get; init; } = 8081;
        public bool   HttpBindAll     { get; init; } = false;
        public bool   HttpAutoStart   { get; init; } = false;
        public string PipeName        { get; init; } = "ApexComputerUse";
        public string LogLevel        { get; init; } = "Information";
        public bool   EnableShellRun  { get; init; } = false;
        public string ModelPath       { get; init; } = "";
        public string MmProjPath      { get; init; } = "";
        public string ApiKey          { get; init; } = "";
        public string AllowedChatIds  { get; init; } = "";
        public string TelegramToken   { get; init; } = "";
        public string TestRunnerExePath    { get; init; } = "";
        public string TestRunnerConfigPath { get; init; } = "";

        // ── Singleton ─────────────────────────────────────────────────────

        /// <summary>Loaded once at startup; available application-wide.</summary>
        public static AppConfig Current { get; } = Load();

        // ── Loading ───────────────────────────────────────────────────────

        public static AppConfig Load()
        {
            var cfg = new AppConfig();  // start with compiled defaults

            // Layer 1: appsettings.json from app directory
            string jsonPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            if (File.Exists(jsonPath))
            {
                try
                {
                    var opts = new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip };
                    using var doc = JsonDocument.Parse(File.ReadAllText(jsonPath), opts);
                    cfg = ApplyJson(cfg, doc.RootElement);
                }
                catch (Exception ex)
                {
                    // Logged after AppLog is configured; writing to stderr as fallback.
                    Debug.WriteLine($"[AppConfig] appsettings.json parse error: {ex.Message}");
                }
            }

            // Layer 2: APEX_* environment variables
            cfg = ApplyEnv(cfg);

            return cfg;
        }

        // ── JSON overlay ──────────────────────────────────────────────────

        private static AppConfig ApplyJson(AppConfig cfg, JsonElement root)
        {
            static string? Str(JsonElement e, string key) =>
                e.TryGetProperty(key, out var p) && p.ValueKind == JsonValueKind.String
                    ? p.GetString() : null;
            static int? Int(JsonElement e, string key) =>
                e.TryGetProperty(key, out var p) && p.TryGetInt32(out int v) ? v : null;
            static bool? Bool(JsonElement e, string key) =>
                e.TryGetProperty(key, out var p) &&
                p.ValueKind is JsonValueKind.True or JsonValueKind.False
                    ? p.GetBoolean() : null;

            return cfg with
            {
                HttpPort       = Int(root,  "HttpPort")       ?? cfg.HttpPort,
                HttpBindAll    = Bool(root, "HttpBindAll")    ?? cfg.HttpBindAll,
                HttpAutoStart  = Bool(root, "HttpAutoStart")  ?? cfg.HttpAutoStart,
                PipeName       = Str(root,  "PipeName")       ?? cfg.PipeName,
                LogLevel       = Str(root,  "LogLevel")       ?? cfg.LogLevel,
                EnableShellRun = Bool(root, "EnableShellRun") ?? cfg.EnableShellRun,
                ModelPath      = Str(root,  "ModelPath")      ?? cfg.ModelPath,
                MmProjPath     = Str(root,  "MmProjPath")     ?? cfg.MmProjPath,
                ApiKey         = Str(root,  "ApiKey")         ?? cfg.ApiKey,
                AllowedChatIds = Str(root,  "AllowedChatIds") ?? cfg.AllowedChatIds,
                TelegramToken  = Str(root,  "TelegramToken")  ?? cfg.TelegramToken,
                TestRunnerExePath    = Str(root, "TestRunnerExePath")    ?? cfg.TestRunnerExePath,
                TestRunnerConfigPath = Str(root, "TestRunnerConfigPath") ?? cfg.TestRunnerConfigPath,
            };
        }

        // ── Environment variable overlay ──────────────────────────────────

        private static AppConfig ApplyEnv(AppConfig cfg)
        {
            static string? E(string name) =>
                Environment.GetEnvironmentVariable("APEX_" + name) is { Length: > 0 } v ? v : null;
            static bool ParseBool(string s) =>
                s.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                s.Equals("1",    StringComparison.Ordinal);

            return cfg with
            {
                HttpPort       = E("HTTP_PORT") is { } p  && int.TryParse(p, out int port) ? port : cfg.HttpPort,
                HttpBindAll    = E("HTTP_BIND_ALL")     is { } b ? ParseBool(b)             : cfg.HttpBindAll,
                HttpAutoStart  = E("HTTP_AUTOSTART")    is { } a ? ParseBool(a)             : cfg.HttpAutoStart,
                PipeName       = E("PIPE_NAME")         ?? cfg.PipeName,
                LogLevel       = E("LOG_LEVEL")         ?? cfg.LogLevel,
                EnableShellRun = E("ENABLE_SHELL_RUN")  is { } s ? ParseBool(s)             : cfg.EnableShellRun,
                ModelPath      = E("MODEL_PATH")        ?? cfg.ModelPath,
                MmProjPath     = E("MMPROJ_PATH")       ?? cfg.MmProjPath,
                ApiKey         = E("API_KEY")           ?? cfg.ApiKey,
                AllowedChatIds = E("ALLOWED_CHAT_IDS")  ?? cfg.AllowedChatIds,
                TelegramToken  = E("TELEGRAM_TOKEN")    ?? cfg.TelegramToken,
                TestRunnerExePath    = E("TEST_RUNNER_EXE_PATH")    ?? cfg.TestRunnerExePath,
                TestRunnerConfigPath = E("TEST_RUNNER_CONFIG_PATH") ?? cfg.TestRunnerConfigPath,
            };
        }
    }
}
