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
    }
}
