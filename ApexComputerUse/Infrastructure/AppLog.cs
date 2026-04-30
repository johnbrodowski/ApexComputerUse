using Serilog;
using Serilog.Events;

namespace ApexComputerUse
{
    /// <summary>
    /// Static structured-logging facade backed by Serilog.
    ///
    /// Call <see cref="Configure"/> once at startup (before Application.Run).
    /// All other methods are safe to call from any thread at any time.
    ///
    /// Existing <c>OnLog?.Invoke</c> events should be forwarded here via
    /// <see cref="FromOnLog"/> so that they also appear in the log file.
    /// </summary>
    internal static class AppLog
    {
        // -- Configuration -------------------------------------------------

        /// <summary>
        /// Configures Serilog with a rolling daily file sink under
        /// <paramref name="logDirectory"/>.  Retains the last 7 daily log files.
        /// </summary>
        /// <param name="logDirectory">Directory to write log files into.</param>
        /// <param name="debug">When true, Debug-level events are emitted (verbose).</param>
        public static void Configure(string logDirectory, bool debug = false)
        {
            Directory.CreateDirectory(logDirectory);
            string logPath = Path.Combine(logDirectory, "apex-.log");

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Is(debug ? LogEventLevel.Debug : LogEventLevel.Information)
                .WriteTo.File(
                    path:                   logPath,
                    rollingInterval:         RollingInterval.Day,
                    retainedFileCountLimit:  7,
                    outputTemplate:          "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();
        }

        // -- Logging methods ------------------------------------------------

        public static void Debug(string msg)                      => Log.Debug(msg);
        public static void Info(string msg)                       => Log.Information(msg);
        public static void Warning(string msg)                    => Log.Warning(msg);
        public static void Error(string msg, Exception? ex = null)
        {
            if (ex != null) Log.Error(ex, msg);
            else            Log.Error(msg);
        }

        /// <summary>
        /// Routes a message from an existing <c>OnLog</c> event into the structured
        /// log at Information level.  Use this to bridge legacy event-based logging.
        /// </summary>
        public static void FromOnLog(string msg) => Log.Information(msg);

        // -- Lifecycle -----------------------------------------------------

        /// <summary>Flushes buffered log entries and closes the file sink.</summary>
        public static void CloseAndFlush() => Log.CloseAndFlush();
    }
}

