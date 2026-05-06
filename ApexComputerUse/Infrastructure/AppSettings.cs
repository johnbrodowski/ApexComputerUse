namespace ApexComputerUse
{
    internal sealed class AppSettings
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
        public bool NetshConfigured { get; set; } = false;
        public int NetshPort { get; set; } = 0;

        /// <summary>
        /// When true, GET /help is reachable without an API key (rate-limited per IP).
        /// Toggled from the Remote Control tab; persisted across sessions.
        /// </summary>
        public bool PublicHelpPage { get; set; } = false;

        /// <summary>
        /// Maximum unauthenticated requests per minute per IP for /help when PublicHelpPage is true.
        /// </summary>
        public int PublicHelpRateLimit { get; set; } = 30;
    }
}
