using System.ServiceProcess;
using LLama.Native;

namespace ApexComputerUse
{
    internal static class Program
    {
        public static bool IsClientInstance { get; private set; }

        /// <summary>
        /// Entry point.  Run without arguments for the WinForms GUI.
        /// Run with <c>--service</c> (or register via sc.exe) for headless Windows Service mode.
        /// </summary>
        [STAThread]
        private static void Main(string[] args)
        {
            // ── Command-line overrides (must precede AppConfig.Current) ────
            // --port <n>   overrides the HTTP listen port (useful for running multiple instances)
            // --pipe <name> overrides the named-pipe name
            // --client      marks this instance as a subordinate client (disables Launch Instance)
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].Equals("--client", StringComparison.OrdinalIgnoreCase))
                {
                    IsClientInstance = true;
                }
                else if (i < args.Length - 1)
                {
                    if (args[i].Equals("--port", StringComparison.OrdinalIgnoreCase) &&
                        int.TryParse(args[i + 1], out _))
                        Environment.SetEnvironmentVariable("APEX_HTTP_PORT", args[i + 1]);
                    else if (args[i].Equals("--pipe", StringComparison.OrdinalIgnoreCase) &&
                        !string.IsNullOrWhiteSpace(args[i + 1]))
                        Environment.SetEnvironmentVariable("APEX_PIPE_NAME", args[i + 1]);
                }
            }

            // ── Configuration + structured logging ────────────────────────
            var cfg = AppConfig.Current;   // loads appsettings.json + APEX_* env vars
            AppLog.Configure(
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "ApexComputerUse", "Logs"),
                debug: cfg.LogLevel.Equals("Debug", StringComparison.OrdinalIgnoreCase));

            // ── Windows Service mode ──────────────────────────────────────
            bool isService = args.Contains("--service", StringComparer.OrdinalIgnoreCase)
                          || !Environment.UserInteractive;

            if (isService)
            {
                AppLog.Info("ApexComputerUse starting in service mode.");
                InitNativeLibs(logToConsole: false);
                ServiceBase.Run(new ApexService());
                return;
            }

            // ── GUI mode ──────────────────────────────────────────────────
            AppLog.Info($"ApexComputerUse starting (GUI). HTTP={cfg.HttpPort} BindAll={cfg.HttpBindAll} Pipe={cfg.PipeName}");
            InitNativeLibs(logToConsole: true);

            ApplicationConfiguration.Initialize();
            try
            {
                Application.Run(new Form1());
            }
            finally
            {
                AppLog.Info("ApexComputerUse exiting.");
                AppLog.CloseAndFlush();
            }
        }

        private static void InitNativeLibs(bool logToConsole)
        {
            NativeLibraryConfig
               .All
               .WithLogCallback((level, message) =>
               {
                   string trimmed = message.TrimEnd('\n');
                   AppLog.Debug($"[llama {level}] {trimmed}");
                   if (logToConsole)
                       Console.WriteLine($"[llama {level}]: {trimmed}");
               });

            // Configure native library to use. This must be done before any other llama.cpp methods are called!
            NativeLibraryConfig
               .All
               .WithCuda(false)
               .WithVulkan(false);

            // Force native library loading now so failures surface early.
            NativeApi.llama_empty_call();
        }
    }
}
