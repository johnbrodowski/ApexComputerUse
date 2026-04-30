namespace ApexUIBridge.TestRunner;

public sealed class RunnerConfig
{
    // -- Loop control ----------------------------------------------------------

    // -- Runner mode -----------------------------------------------------------
    /// <summary>
    /// Optional runner mode: "demo" or "benchmark".
    /// CLI --mode overrides this value.
    /// </summary>
    public string? Mode { get; init; }

    /// <summary>Total number of build -> launch -> test -> stop cycles to run.</summary>
    public int MaxCycles { get; init; } = 10;

    /// <summary>Send a Telegram progress report every N cycles. 0 = only on completion.</summary>
    public int ReportEveryN { get; init; } = 3;

    // -- Build -----------------------------------------------------------------
    /// <summary>
    /// When true, skip the dotnet build step and use the already-compiled executables.
    /// Useful for iterative test runs where the code hasn't changed.
    /// </summary>
    public bool SkipBuild { get; init; } = false;

    // -- Paths -----------------------------------------------------------------
    /// <summary>Path to the ApexUIBridge .sln file (used for dotnet build).</summary>
    public string SolutionPath { get; init; } = "";

    /// <summary>Path to the built ApexUIBridge.exe.</summary>
    public string BridgeExePath { get; init; } = "";

    /// <summary>Path to the WinForms test app exe. Launched once, left running.</summary>
    public string WinFormsExePath { get; init; } = "";

    /// <summary>Path to the WPF test app exe. Launched once, left running.</summary>
    public string WpfExePath { get; init; } = "";

    /// <summary>Base URL for the web test target (optional).</summary>
    public string WebBaseUrl { get; init; } = "";

    /// <summary>Optional web test page paths (for example: "/form", "/table").</summary>
    public string[] WebPagePaths { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Folder to serve statically via a built-in HttpListener at WebBaseUrl.
    /// Leave empty to point WebBaseUrl at an already-running external server.
    /// </summary>
    public string WebRootPath { get; init; } = "";

    /// <summary>
    /// Optional path to the browser executable used to open web pages.
    /// Leave empty to open with the OS default browser (shell-associated handler).
    /// </summary>
    public string WebBrowserExe { get; init; } = "";

    // -- Bridge API ------------------------------------------------------------
    public string BridgeBaseUrl      { get; init; } = "http://localhost:8765";
    public int    ApiReadyTimeoutSec { get; init; } = 30;
    /// <summary>
    /// API key injected into the bridge process as APEX_API_KEY and sent with every
    /// BridgeClient request. Leave empty only if the bridge has auth disabled.
    /// </summary>
    public string BridgeApiKey       { get; init; } = "";

    public string BuildConfiguration { get; init; } = "Debug";

    // -- Telegram --------------------------------------------------------------
    public string TelegramBotToken { get; init; } = "";
    public long   TelegramChatId   { get; init; } = 0;

    // -- Test filtering -------------------------------------------------------
    /// <summary>When true, skip tests that passed in the previous run.</summary>
    public bool RunOnlyFailed { get; init; } = false;

    /// <summary>Path to the JSON file where per-test pass/fail results are persisted.</summary>
    public string TestResultsPath { get; init; } =
        Path.Combine(Path.GetTempPath(), "ApexUIBridge_test_results.json");

    /// <summary>
    /// Path to append-friendly benchmark output (JSON Lines: one compact JSON record per cycle).
    /// </summary>
    public string BenchmarkResultsPath { get; init; } =
        Path.Combine(Path.GetTempPath(), "ApexUIBridge_benchmark_results.jsonl");

    // -- Pacing ----------------------------------------------------------------
    /// <summary>
    /// Pacing preset: "fast", "normal", or "human".
    /// Explicit delay values override the selected profile when greater than 0.
    /// In demo/benchmark mode the --mode flag picks a profile regardless of this value.
    /// </summary>
    public string SpeedProfile { get; init; } = "human";

    // -- Stop coordination -----------------------------------------------------
    /// <summary>
    /// If this file exists the runner cancels immediately.
    /// ApexUIBridge writes it when /stop-tests is received via Telegram.
    /// </summary>
    public string StopFlagPath { get; init; } =
        Path.Combine(Path.GetTempPath(), "ApexUIBridge_stop.flag");

    /// <summary>
    /// Delay between action steps in the test suite (milliseconds). 0 = use profile default.
    /// </summary>
    public int ActionDelayMs { get; init; } = 0;

    /// <summary>
    /// Delay after UI-changing operations before reading state (milliseconds). 0 = use profile default.
    /// </summary>
    public int UiSettleDelayMs { get; init; } = 0;
}

