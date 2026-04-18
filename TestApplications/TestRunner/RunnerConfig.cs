namespace ApexUIBridge.TestRunner;

public sealed class RunnerConfig
{
    // ── Loop control ──────────────────────────────────────────────────────────

    // ── Runner mode ───────────────────────────────────────────────────────────
    /// <summary>
    /// Optional runner mode: "demo" or "benchmark".
    /// CLI --mode overrides this value.
    /// </summary>
    public string? Mode { get; init; }

    /// <summary>Total number of build → launch → test → stop cycles to run.</summary>
    public int MaxCycles { get; init; } = 10;

    /// <summary>Send a Telegram progress report every N cycles. 0 = only on completion.</summary>
    public int ReportEveryN { get; init; } = 3;

    // ── Paths ─────────────────────────────────────────────────────────────────
    /// <summary>Path to the ApexUIBridge .sln file (used for dotnet build).</summary>
    public string SolutionPath { get; init; } = "";

    /// <summary>Path to the built ApexUIBridge.exe.</summary>
    public string BridgeExePath { get; init; } = "";

    /// <summary>Path to the WinForms test app exe. Launched once, left running.</summary>
    public string WinFormsExePath { get; init; } = "";

    /// <summary>Path to the WPF test app exe. Launched once, left running.</summary>
    public string WpfExePath { get; init; } = "";

    // ── Bridge API ────────────────────────────────────────────────────────────
    public string BridgeBaseUrl      { get; init; } = "http://localhost:8765";
    public int    ApiReadyTimeoutSec { get; init; } = 30;

    // ── Build ─────────────────────────────────────────────────────────────────
    public string BuildConfiguration { get; init; } = "Debug";

    // ── Telegram ──────────────────────────────────────────────────────────────
    public string TelegramBotToken { get; init; } = "";
    public long   TelegramChatId   { get; init; } = 0;

    // ── Test filtering ───────────────────────────────────────────────────────
    /// <summary>When true, skip tests that passed in the previous run.</summary>
    public bool RunOnlyFailed { get; init; } = false;

    /// <summary>Path to the JSON file where per-test pass/fail results are persisted.</summary>
    public string TestResultsPath { get; init; } =
        Path.Combine(Path.GetTempPath(), "ApexUIBridge_test_results.json");

    // ── Stop coordination ─────────────────────────────────────────────────────
    /// <summary>
    /// If this file exists the runner cancels immediately.
    /// ApexUIBridge writes it when /stop-tests is received via Telegram.
    /// </summary>
    public string StopFlagPath { get; init; } =
        Path.Combine(Path.GetTempPath(), "ApexUIBridge_stop.flag");
}
