using System.ServiceProcess;

namespace ApexComputerUse
{
    /// <summary>
    /// Windows Service hosting mode - runs the HTTP and named-pipe servers headlessly,
    /// without the WinForms GUI.
    ///
    /// Installation (run as Administrator):
    ///   sc create ApexComputerUse binPath= "\"C:\path\to\ApexComputerUse.exe\" --service"
    ///   sc description ApexComputerUse "Apex Computer-Use automation server"
    ///   sc start ApexComputerUse
    ///
    /// All configuration is read from appsettings.json and APEX_* environment variables
    /// (see AppConfig for the full list).  The %APPDATA% user settings file is ignored in
    /// service mode - use appsettings.json instead.
    ///
    /// Logs are written to %LOCALAPPDATA%\ApexComputerUse\Logs\ via Serilog (same path as GUI mode).
    /// </summary>
    internal sealed class ApexService : ServiceBase
    {
        private HttpCommandServer? _http;
        private PipeCommandServer? _pipe;
        private CommandProcessor?  _processor;
        private SceneStore?        _store;

        public ApexService()
        {
            ServiceName        = "ApexComputerUse";
            CanStop            = true;
            CanPauseAndContinue = false;
            AutoLog            = false;   // we write to Serilog, not Windows Event Log
        }

        protected override void OnStart(string[] args)
        {
            var cfg = AppConfig.Current;

            if (cfg.HttpBindAll && string.IsNullOrWhiteSpace(cfg.ApiKey))
            {
                AppLog.Error("[Service] FATAL: HttpBindAll=true with no API key - refusing to start network-exposed with no authentication. Set APEX_API_KEY or ApiKey in appsettings.json.");
                ExitCode = 1;
                Stop();
                return;
            }

            AppLog.Info($"[Service] Starting - HTTP port {cfg.HttpPort}, pipe '{cfg.PipeName}'");

            _store     = new SceneStore();
            _processor = new CommandProcessor
            {
                SceneStore         = _store,
                ElementAnnotations = new ElementAnnotationStore(),
                RegionMaps         = new RegionMapStore(),
                RegionMonitors     = new RegionMonitorStore()
            };
            _processor.OnLog += AppLog.FromOnLog;

            _http = new HttpCommandServer(cfg.HttpPort, _processor, _store,
                        apiKey:        cfg.ApiKey,
                        enableShellRun: cfg.EnableShellRun,
                        bindAll:        cfg.HttpBindAll,
                        testRunnerExePath:    cfg.TestRunnerExePath,
                        testRunnerConfigPath: cfg.TestRunnerConfigPath);
            _http.OnLog += AppLog.FromOnLog;
            _http.OnShutdownRequested += () =>
            {
                // Request a clean SCM stop so OnStop runs and logs flush.
                try { Stop(); }
                catch { Environment.Exit(0); }
            };

            _pipe = new PipeCommandServer(cfg.PipeName, _processor);
            _pipe.OnLog += AppLog.FromOnLog;

            _http.Start();
            _pipe.Start();

            AppLog.Info("[Service] Started.");
        }

        protected override void OnStop()
        {
            AppLog.Info("[Service] Stopping...");
            _http?.Stop();
            _pipe?.Stop();
            _processor?.Dispose();
            AppLog.Info("[Service] Stopped.");
            AppLog.CloseAndFlush();
        }
    }
}

