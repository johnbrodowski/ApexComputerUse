namespace ApexComputerUse
{
    /// <summary>
    /// Mutable runtime values that the GUI can change without restarting the server.
    /// Seeded at startup from AppConfig (appsettings.json + APEX_* env), then overlaid
    /// by Form1 from %APPDATA%\ApexComputerUse\settings.json. HttpCommandServer reads
    /// from here so changes take effect immediately on the next request.
    /// </summary>
    internal static class RuntimeFlags
    {
        public static volatile bool PublicHelpPage      = AppConfig.Current.PublicHelpPage;
        public static          int  PublicHelpRateLimit = AppConfig.Current.PublicHelpRateLimit;
    }
}
