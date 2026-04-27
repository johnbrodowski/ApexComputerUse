using ApexUIBridge.TestRunner;
using System.Diagnostics;
using System.Text.Json;

int _MaxCycles = 1;
// ── Config ─────────────────────────────────────────────────────────────────────
string? cliMode = null;
string? cliConfigPath = null;
bool    cliServe = false;
int     cliServePort = 8765;
string  cliServeHost = "127.0.0.1";
bool    cliList   = false;
string? cliOnly   = null;
string? cliFilter = null;
for (var i = 0; i < args.Length; i++)
{
    var arg = args[i];
    if (arg.StartsWith("--mode=", StringComparison.OrdinalIgnoreCase))
    {
        cliMode = arg[("--mode=".Length)..];
        continue;
    }

    if (string.Equals(arg, "--mode", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
    {
        cliMode = args[++i];
        continue;
    }

    if (string.Equals(arg, "--serve", StringComparison.OrdinalIgnoreCase))
    {
        cliServe = true;
        continue;
    }

    if (arg.StartsWith("--serve-port=", StringComparison.OrdinalIgnoreCase))
    {
        cliServe = true;
        int.TryParse(arg[("--serve-port=".Length)..], out cliServePort);
        continue;
    }

    if (string.Equals(arg, "--serve-port", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
    {
        cliServe = true;
        int.TryParse(args[++i], out cliServePort);
        continue;
    }

    if (arg.StartsWith("--serve-host=", StringComparison.OrdinalIgnoreCase))
    {
        cliServe = true;
        cliServeHost = arg[("--serve-host=".Length)..];
        continue;
    }

    if (string.Equals(arg, "--serve-host", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
    {
        cliServe = true;
        cliServeHost = args[++i];
        continue;
    }

    if (string.Equals(arg, "--list", StringComparison.OrdinalIgnoreCase))
    {
        cliList = true;
        continue;
    }

    if (arg.StartsWith("--only=", StringComparison.OrdinalIgnoreCase) ||
        arg.StartsWith("--tests=", StringComparison.OrdinalIgnoreCase))
    {
        cliOnly = arg[(arg.IndexOf('=') + 1)..];
        continue;
    }
    if ((string.Equals(arg, "--only", StringComparison.OrdinalIgnoreCase) ||
         string.Equals(arg, "--tests", StringComparison.OrdinalIgnoreCase)) && i + 1 < args.Length)
    {
        cliOnly = args[++i];
        continue;
    }

    if (arg.StartsWith("--filter=", StringComparison.OrdinalIgnoreCase))
    {
        cliFilter = arg[("--filter=".Length)..];
        continue;
    }
    if (string.Equals(arg, "--filter", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
    {
        cliFilter = args[++i];
        continue;
    }

    if (!arg.StartsWith("-"))
    {
        cliConfigPath = arg;
    }
}

var configPath = cliConfigPath ?? Path.Combine(AppContext.BaseDirectory, "runner-config.json");

if (!File.Exists(configPath))
{
    Console.Error.WriteLine($"Config not found: {configPath}");
    Console.Error.WriteLine("Usage: TestRunner [path-to-runner-config.json] [--mode demo|benchmark]");
    return 1;
}

var config = JsonSerializer.Deserialize<RunnerConfig>(
    File.ReadAllText(configPath),
    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

// --list: print catalog and exit. No bridge, no test apps, no web server.
if (cliList)
{
    using var listClient = new BridgeClient(config.BridgeBaseUrl, config.BridgeApiKey);
    var listSuite = new TestSuite(listClient);
    Console.WriteLine($"{"ID",-55} {"GROUP",-18} NAME");
    Console.WriteLine(new string('─', 120));
    foreach (var tc in listSuite.Catalog)
        Console.WriteLine($"{tc.Id,-55} {tc.Group,-18} {tc.Name}");
    return 0;
}

bool filtered = !string.IsNullOrWhiteSpace(cliOnly) || !string.IsNullOrWhiteSpace(cliFilter);

var mode = (cliMode ?? config.Mode ?? "demo").Trim().ToLowerInvariant();
if (mode != "demo" && mode != "benchmark")
{
    Console.Error.WriteLine($"Invalid mode '{mode}'. Use 'demo' or 'benchmark'.");
    return 1;
}

var maxCycles = mode == "benchmark" ? Math.Max(config.MaxCycles, 10) : Math.Min(config.MaxCycles, _MaxCycles);
var reportEveryN = mode == "benchmark"
    ? (config.ReportEveryN <= 0 ? 0 : Math.Max(config.ReportEveryN, 10))
    : (config.ReportEveryN <= 0 ? 1 : Math.Min(config.ReportEveryN, 1));
var runOnlyFailed = mode == "benchmark" ? true : config.RunOnlyFailed;
if (filtered)
{
    // Targeted runs: always execute the selected tests, exactly once, regardless
    // of mode/config. The "previously passed" skip logic would otherwise silently
    // skip the very tests the user asked for.
    maxCycles     = 1;
    runOnlyFailed = false;
}
var speedProfile = mode == "benchmark" ? "fast" : "human";
var richConsole = mode == "demo";

// ── Cancellation: Ctrl+C OR stop-flag file written by Telegram /stop-tests ────
var cts = new CancellationTokenSource();

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    Console.WriteLine("\n[Runner] Ctrl+C — cancelling...");
    cts.Cancel();
};

// Background task: poll stop-flag every second, no blocking
_ = Task.Run(async () =>
{
    while (!cts.IsCancellationRequested)
    {
        if (File.Exists(config.StopFlagPath))
        {
            Console.WriteLine("[Runner] Stop flag detected — cancelling.");
            try { File.Delete(config.StopFlagPath); } catch { }
            cts.Cancel();
            return;
        }
        try { await Task.Delay(1000, cts.Token); }
        catch (OperationCanceledException) { return; }
    }
});

var ct = cts.Token;
var telegram = new TelegramNotifier(config.TelegramBotToken, config.TelegramChatId);
var builder = new BuildRunner(config.SolutionPath, config.BuildConfiguration);

if (richConsole)
{
    Console.WriteLine($"[Runner] Mode: {mode} (speed profile: {speedProfile})");
    Console.WriteLine($"[Runner] Effective settings: MaxCycles={maxCycles}, ReportEveryN={reportEveryN}, RunOnlyFailed={runOnlyFailed}");
}
else
{
    Console.WriteLine(JsonSerializer.Serialize(new
    {
        Event = "runner_settings",
        Mode = mode,
        SpeedProfile = speedProfile,
        MaxCycles = maxCycles,
        ReportEveryN = reportEveryN,
        RunOnlyFailed = runOnlyFailed
    }));
}

// Mode (demo/benchmark) takes precedence over the config's SpeedProfile label.
var effectiveSpeedProfile = speedProfile;
var (defaultActionDelayMs, defaultUiSettleDelayMs) = effectiveSpeedProfile.ToLowerInvariant() switch
{
    "fast" => (50, 120),
    //"human" => (350, 900),
    "human" => (150, 350),
    _ => (120, 300)
};
var actionDelayMs = config.ActionDelayMs > 0 ? config.ActionDelayMs : defaultActionDelayMs;
var uiSettleDelayMs = config.UiSettleDelayMs > 0 ? config.UiSettleDelayMs : defaultUiSettleDelayMs;
if (richConsole)
    Console.WriteLine($"[Runner] Speed profile: {effectiveSpeedProfile} (ActionDelayMs={actionDelayMs}, UiSettleDelayMs={uiSettleDelayMs})");

// ── Load previous test results for skip-passed logic ────────────────────────
var previouslyPassed = new HashSet<string>();
if (runOnlyFailed && File.Exists(config.TestResultsPath))
{
    try
    {
        var saved = JsonSerializer.Deserialize<Dictionary<string, bool>>(
            File.ReadAllText(config.TestResultsPath),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (saved != null)
            foreach (var kv in saved.Where(kv => kv.Value))
                previouslyPassed.Add(kv.Key);
        if (richConsole)
            Console.WriteLine($"[Runner] RunOnlyFailed=true — {previouslyPassed.Count} previously-passed tests will be skipped.");
    }
    catch (Exception ex)
    {
        if (richConsole)
            Console.WriteLine($"[Runner] Warning: could not load {config.TestResultsPath}: {ex.Message}");
    }
}
else if (runOnlyFailed && richConsole)
{
    Console.WriteLine("[Runner] RunOnlyFailed=true but no previous results file found — running all tests.");
}

// ── Launch the 3rd-party test-target apps once — they stay running the whole time
await using var winFormsApp = new ProcessManager("WinForms Test App", config.WinFormsExePath, isGui: true);
await using var wpfApp = new ProcessManager("WPF Test App", config.WpfExePath, isGui: true);

if (richConsole) Console.WriteLine("[Runner] Starting test-target applications...");
await winFormsApp.StartAsync(ct);
await wpfApp.StartAsync(ct);

// Optional: start built-in static file server and open each configured page in a browser
WebServer? webServer = null;
if (!string.IsNullOrWhiteSpace(config.WebBaseUrl) && !string.IsNullOrWhiteSpace(config.WebRootPath))
{
    webServer = new WebServer(config.WebBaseUrl, config.WebRootPath);
    webServer.Start();
}
if (!string.IsNullOrWhiteSpace(config.WebBaseUrl))
{
    var pages = config.WebPagePaths.Length > 0 ? config.WebPagePaths : new[] { "/" };
    // Resolve a Chromium-based browser so each page can be opened in its own
    // window (--new-window). Without this, default-shell launches collapse all
    // pages into tabs of the same browser window — meaning /find by tab title
    // only ever activates whichever tab is foreground, and type/keys land on
    // the wrong document (or the address bar).
    var browserExe = ResolveBrowserExe(config.WebBrowserExe);
    bool isChromium = browserExe != null && IsChromiumExe(browserExe);
    foreach (var page in pages)
    {
        var url = BuildWebUrl(config.WebBaseUrl, page);
        try
        {
            ProcessStartInfo psi;
            if (isChromium)
            {
                psi = new ProcessStartInfo
                {
                    FileName = browserExe!,
                    Arguments = $"--new-window \"{url}\"",
                    UseShellExecute = false,
                };
            }
            else if (browserExe != null)
            {
                psi = new ProcessStartInfo { FileName = browserExe, Arguments = url, UseShellExecute = false };
            }
            else
            {
                psi = new ProcessStartInfo { FileName = url, UseShellExecute = true };
            }
            Process.Start(psi);
            if (richConsole) Console.WriteLine($"[Runner] Opened browser → {url}{(isChromium ? " (--new-window)" : "")}");
            // Stagger window creation so the OS gives each one a distinct HWND
            // and the browser doesn't merge them into a single window.
            Thread.Sleep(800);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[Runner] Failed to open browser for {url}: {ex.Message}");
        }
    }
}

await Task.Delay(3000, ct);  // give GUIs time to render before scanning

// ── Serve mode ────────────────────────────────────────────────────────────────
// When --serve is set, replace the cycle loop with a long-running HTTP control
// surface (see ControlServer.cs). External callers drive per-test execution,
// bridge lifecycle, and shutdown.
if (cliServe)
{
    Console.WriteLine($"[Runner] Serve mode on {cliServeHost}:{cliServePort}");

    if (!config.SkipBuild)
    {
        Console.WriteLine("[Runner] Building ApexComputerUse (one-shot)...");
        var build = await builder.BuildAsync(ct);
        if (!build.Success)
        {
            var snippet = build.Output.Length > 600 ? "…" + build.Output[^600..] : build.Output;
            Console.Error.WriteLine($"[Runner] Build FAILED (exit {build.ExitCode}):\n{snippet}");
            return 1;
        }
        Console.WriteLine("[Runner] Build OK.");
    }

    ProcessManager? currentBridge = null;
    BridgeClient?   currentClient = null;
    TestSuite?      currentSuite  = null;

var bridgeEnvServe = string.IsNullOrWhiteSpace(config.BridgeApiKey)
    ? new Dictionary<string, string> { ["APEX_HTTP_AUTOSTART"] = "true" }
    : new Dictionary<string, string>
      {
          ["APEX_API_KEY"] = config.BridgeApiKey,
          ["APEX_HTTP_AUTOSTART"] = "true"
      };

    async Task<ProcessManager> StartBridge(CancellationToken innerCt)
    {
        var pm = new ProcessManager("ApexComputerUse", config.BridgeExePath, isGui: true);
        await pm.StartAsync(innerCt, bridgeEnvServe);
        return pm;
    }

    // Initial bridge + client + suite
    currentBridge = await StartBridge(ct);
    currentClient = new BridgeClient(config.BridgeBaseUrl, config.BridgeApiKey, Console.WriteLine);
    Console.WriteLine("[Runner] Waiting for Bridge API...");
    var servedReady = await currentClient.WaitForReadyAsync(config.ApiReadyTimeoutSec, ct);
    if (!servedReady)
        Console.WriteLine("[Runner] Bridge did not become ready in time — continuing anyway; /bridge/restart can recover.");
    currentSuite = new TestSuite(currentClient, actionDelayMs, uiSettleDelayMs);

    var control = new ControlServer(
        bindHost:     cliServeHost,
        port:         cliServePort,
        apiKey:       config.BridgeApiKey,
        config:       config,
        configPath:   configPath,
        getBridge:    () => currentBridge,
        setBridge:    pm  => { currentBridge = pm; return Task.CompletedTask; },
        startBridge:  async innerCt => await StartBridge(innerCt),
        getClient:    () => currentClient!,
        setClient:    c   => { currentClient?.Dispose(); currentClient = c; return Task.CompletedTask; },
        getSuite:     () => currentSuite!,
        setSuite:     s   => { currentSuite = s; return Task.CompletedTask; },
        shutdownCts:  cts);
    control.Start();

    Console.WriteLine($"[Runner] Ready. POST /shutdown or press Ctrl+C to exit.");
    Console.WriteLine($"[Runner]   Example: curl -H 'X-Api-Key: {(string.IsNullOrEmpty(config.BridgeApiKey) ? "<none>" : "***")}' http://{cliServeHost}:{cliServePort}/tests");

    try { await Task.Delay(Timeout.Infinite, ct); }
    catch (OperationCanceledException) { /* shutdown */ }

    Console.WriteLine("[Runner] Shutting down control server...");
    await control.DisposeAsync();
    if (currentBridge is { IsRunning: true }) await currentBridge.StopAsync();
    currentClient?.Dispose();
    if (webServer is not null) await webServer.DisposeAsync();
    return 0;
}

await telegram.SendAsync(
    $"🚀 <b>TestRunner started</b>\n" +
    $"Mode: <b>{mode}</b>   Speed profile: <b>{speedProfile}</b>\n" +
    $"Cycles: <b>{maxCycles}</b>   Config: <b>{config.BuildConfiguration}</b>\n" +
    $"Reports every: <b>{reportEveryN}</b> cycle(s)\n" +
    $"Send /stop-tests to cancel at any time.", ct);

// ── Main loop ──────────────────────────────────────────────────────────────────
var history = new List<(int Cycle, CycleResult Result)>();
int cycle = 0;

void AppendBenchmarkRecord(
    int cycleNumber,
    int passed,
    int failed,
    int skipped,
    DateTimeOffset startedAtUtc,
    long cycleMs,
    long buildMs,
    long bridgeReadyMs,
    long suiteMs)
{
    try
    {
        var benchmarkRecord = new
        {
            TimestampUtc = startedAtUtc,
            Mode = mode,
            SpeedProfile = effectiveSpeedProfile,
            Cycle = cycleNumber,
            Passed = passed,
            Failed = failed,
            Skipped = skipped,
            Timing = new
            {
                TotalMs = cycleMs,
                BuildMs = buildMs,
                BridgeReadyMs = bridgeReadyMs,
                TestSuiteMs = suiteMs
            }
        };

        var benchmarkDir = Path.GetDirectoryName(config.BenchmarkResultsPath);
        if (!string.IsNullOrWhiteSpace(benchmarkDir))
            Directory.CreateDirectory(benchmarkDir);

        File.AppendAllText(
            config.BenchmarkResultsPath,
            JsonSerializer.Serialize(benchmarkRecord) + Environment.NewLine);
    }
    catch (Exception ex)
    {
        if (richConsole)
            Console.WriteLine($"[Runner] Warning: could not append benchmark record: {ex.Message}");
    }
}

while (cycle < maxCycles && !ct.IsCancellationRequested)
{
    cycle++;
    var cycleStartedAtUtc = DateTimeOffset.UtcNow;
    var cycleTimer = Stopwatch.StartNew();
    long buildMs = 0;
    long bridgeReadyMs = 0;
    long suiteMs = 0;
    if (richConsole)
    {
        var divider = new string('─', 60);
        Console.WriteLine($"\n{divider}");
        Console.WriteLine($"[Runner] Cycle {cycle}/{maxCycles}");
        Console.WriteLine(divider);
    }

    if (!config.SkipBuild)
    {
    if (richConsole) Console.WriteLine("[Runner] Building ApexComputerUse...");
    var buildTimer = Stopwatch.StartNew();
    var build = await builder.BuildAsync(ct);
    buildTimer.Stop();
    buildMs = buildTimer.ElapsedMilliseconds;
    if (!build.Success)
    {
        var snippet = build.Output.Length > 600
            ? "…" + build.Output[^600..]
            : build.Output;
        if (richConsole)
        {
            Console.WriteLine($"[Runner] Build FAILED (exit {build.ExitCode}):\n{snippet}");
        }
        else
        {
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                Event = "build_failed",
                Cycle = cycle,
                build.ExitCode,
                OutputSnippet = snippet
            }));
        }
        await telegram.SendAsync(
            $"❌ <b>Cycle {cycle} — Build FAILED</b>\n<pre>{snippet}</pre>", ct);
        cycleTimer.Stop();
        AppendBenchmarkRecord(
            cycleNumber: cycle,
            passed: 0,
            failed: 1,
            skipped: 0,
            startedAtUtc: cycleStartedAtUtc,
            cycleMs: cycleTimer.ElapsedMilliseconds,
            buildMs: buildMs,
            bridgeReadyMs: bridgeReadyMs,
            suiteMs: suiteMs);
        // A build failure is fatal — no point retrying without a code change
        break;
    }
    if (richConsole) Console.WriteLine("[Runner] Build OK.");
    } // end if (!config.SkipBuild)

    // 2. Launch ApexComputerUse ───────────────────────────────────────────────────
    await using var bridge = new ProcessManager("ApexComputerUse", config.BridgeExePath, isGui: true);
    var bridgeEnv = string.IsNullOrWhiteSpace(config.BridgeApiKey)
        ? new Dictionary<string, string> { ["APEX_HTTP_AUTOSTART"] = "true" }
        : new Dictionary<string, string>
          {
              ["APEX_API_KEY"] = config.BridgeApiKey,
              ["APEX_HTTP_AUTOSTART"] = "true"
          };
    await bridge.StartAsync(ct, bridgeEnv);

    using var client = new BridgeClient(config.BridgeBaseUrl, config.BridgeApiKey,
        logger: richConsole ? Console.WriteLine : null);
    if (richConsole) Console.WriteLine("[Runner] Waiting for Bridge API...");

    var readyTimer = Stopwatch.StartNew();
    var ready = await client.WaitForReadyAsync(config.ApiReadyTimeoutSec, ct);
    readyTimer.Stop();
    bridgeReadyMs = readyTimer.ElapsedMilliseconds;
    if (!ready)
    {
        if (richConsole)
        {
            Console.WriteLine("[Runner] Bridge API did not become ready in time — skipping cycle.");
        }
        else
        {
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                Event = "bridge_not_ready",
                Cycle = cycle,
                TimeoutSeconds = config.ApiReadyTimeoutSec
            }));
        }
        await telegram.SendAsync(
            $"⚠️ <b>Cycle {cycle}</b> — Bridge API not ready after {config.ApiReadyTimeoutSec}s", ct);
        await bridge.StopAsync();
        cycleTimer.Stop();
        AppendBenchmarkRecord(
            cycleNumber: cycle,
            passed: 0,
            failed: 0,
            skipped: 0,
            startedAtUtc: cycleStartedAtUtc,
            cycleMs: cycleTimer.ElapsedMilliseconds,
            buildMs: buildMs,
            bridgeReadyMs: bridgeReadyMs,
            suiteMs: suiteMs);
        continue;
    }
    if (richConsole) Console.WriteLine("[Runner] Bridge API ready.");

    // 2b. Ensure web targets are reachable before discovery/interactions ───────
    if (!string.IsNullOrWhiteSpace(config.WebBaseUrl))
    {
        if (richConsole) Console.WriteLine("[Runner] Waiting for web target pages...");
        var webReady = await WaitForWebTargetsAsync(config, ct);
        if (!webReady)
        {
            if (richConsole)
            {
                Console.WriteLine("[Runner] Web target did not become ready in time — skipping cycle.");
            }
            else
            {
                Console.WriteLine(JsonSerializer.Serialize(new
                {
                    Event = "web_not_ready",
                    Cycle = cycle,
                    TimeoutSeconds = config.ApiReadyTimeoutSec
                }));
            }
            await telegram.SendAsync(
                $"⚠️ <b>Cycle {cycle}</b> — Web target not ready after {config.ApiReadyTimeoutSec}s", ct);
            await bridge.StopAsync();
            cycleTimer.Stop();
            AppendBenchmarkRecord(
                cycleNumber: cycle,
                passed: 0,
                failed: 0,
                skipped: 0,
                startedAtUtc: cycleStartedAtUtc,
                cycleMs: cycleTimer.ElapsedMilliseconds,
                buildMs: buildMs,
                bridgeReadyMs: bridgeReadyMs,
                suiteMs: suiteMs);
            continue;
        }
        if (richConsole) Console.WriteLine("[Runner] Web target pages ready.");
    }

    // 3. Run test suite ────────────────────────────────────────────────────────
    var suiteTimer = Stopwatch.StartNew();
    var currentCycle = cycle;
    Action<TestResult> onResult = richConsole
        ? r =>
        {
            if (r.Skipped)
                Console.WriteLine($"  SKIP {r.Name} (previously passed)");
            else if (r.Passed)
                Console.WriteLine($"  PASS {r.Name}" + (r.ElapsedMs.HasValue ? $"  ({r.ElapsedMs}ms)" : ""));
            else
            {
                var cmd = string.IsNullOrEmpty(r.Command) ? "" : $"  cmd={r.Command}";
                Console.WriteLine($"  FAIL {r.Name}{cmd}");
                Console.WriteLine($"       {r.Detail}");
            }
        }
        : r =>
        {
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                Event = "test_result",
                Cycle = currentCycle,
                r.Name,
                r.Passed,
                r.Skipped,
                r.Command,
                r.Detail,
                r.ElapsedMs
            }));
        };

    var suite = new TestSuite(
        client,
        actionDelayMs,
        uiSettleDelayMs,
        runOnlyFailed ? previouslyPassed : null,
        config.WebBaseUrl,
        config.WebPagePaths,
        onResult);

    CycleResult result;
    if (!string.IsNullOrWhiteSpace(cliOnly))
    {
        var ids = cliOnly.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (richConsole) Console.WriteLine($"[Runner] --only: running {ids.Length} test(s)");
        result = await suite.RunSelectedAsync(ids, ct);
    }
    else if (!string.IsNullOrWhiteSpace(cliFilter))
    {
        if (richConsole) Console.WriteLine($"[Runner] --filter: '{cliFilter}'");
        result = await suite.RunFilteredAsync(cliFilter!, ct);
    }
    else
    {
        result = await suite.RunAsync(ct);
    }
    suiteTimer.Stop();
    suiteMs = suiteTimer.ElapsedMilliseconds;
    history.Add((cycle, result));

    // Print to console
    if (richConsole)
    {
        var skippedMsg = result.Skipped > 0 ? $", {result.Skipped} skipped" : "";
        Console.WriteLine($"\n[Results] {result.Passed} passed, {result.Failed} failed{skippedMsg}");
        foreach (var r in result.Results.Where(r => !r.Passed && !r.Skipped))
        {
            var cmd = string.IsNullOrEmpty(r.Command) ? "" : $"  cmd={r.Command}";
            Console.WriteLine($"  FAIL {r.Name}{cmd}");
            Console.WriteLine($"       {r.Detail}");
        }
    }
    else
    {
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            Event = "cycle_result",
            Cycle = cycle,
            Passed = result.Passed,
            Failed = result.Failed,
            Skipped = result.Skipped,
            AllPassed = result.AllPassed,
            Results = result.Results.Select(r => new
            {
                r.Name,
                r.Passed,
                r.Skipped,
                r.Command,
                r.Detail,
                r.ElapsedMs
            })
        }));
    }

    // Persist test results — merge with existing so passed tests stay recorded
    try
    {
        var savedResults = new Dictionary<string, bool>();
        if (File.Exists(config.TestResultsPath))
        {
            var existing = JsonSerializer.Deserialize<Dictionary<string, bool>>(
                File.ReadAllText(config.TestResultsPath));
            if (existing != null) savedResults = existing;
        }
        foreach (var r in result.Results.Where(r => !r.Skipped))
            savedResults[r.Name] = r.Passed;
        File.WriteAllText(config.TestResultsPath,
            JsonSerializer.Serialize(savedResults, new JsonSerializerOptions { WriteIndented = true }));
    }
    catch (Exception ex)
    {
        if (richConsole)
            Console.WriteLine($"[Runner] Warning: could not save results: {ex.Message}");
    }

    // 4. Stop Bridge before next cycle (fresh build will produce new binary) ───
    await bridge.StopAsync();
    await Task.Delay(500, ct);  // brief gap — no blocking, just lets OS release ports

    cycleTimer.Stop();
    AppendBenchmarkRecord(
        cycleNumber: cycle,
        passed: result.Passed,
        failed: result.Failed,
        skipped: result.Skipped,
        startedAtUtc: cycleStartedAtUtc,
        cycleMs: cycleTimer.ElapsedMilliseconds,
        buildMs: buildMs,
        bridgeReadyMs: bridgeReadyMs,
        suiteMs: suiteMs);

    // 5. Telegram progress report ──────────────────────────────────────────────
    bool isLast = cycle == maxCycles;
    bool reportNow = reportEveryN > 0 && (cycle % reportEveryN == 0 || isLast);
    if (reportNow)
    {
        await telegram.SendAsync(
            $"📊 <b>Cycle {cycle}/{maxCycles}</b>\n{result.Summary()}", ct);
    }
}

// ── Final summary ──────────────────────────────────────────────────────────────
if (history.Count > 0)
{
    var totalPass = history.Sum(h => h.Result.Passed);
    var totalFail = history.Sum(h => h.Result.Failed);
    var totalSkipped = history.Sum(h => h.Result.Skipped);
    var allGreen = history.All(h => h.Result.AllPassed);
    var cycleLines = string.Join("\n",
        history.Select(h =>
        {
            var sk = h.Result.Skipped > 0 ? $" SKIP:{h.Result.Skipped}" : "";
            return $"  Cycle {h.Cycle}: PASS:{h.Result.Passed} FAIL:{h.Result.Failed}{sk}";
        }));

    var skipSummary = totalSkipped > 0 ? $"   SKIP: {totalSkipped}" : "";
    var summary =
        $"{(allGreen ? "[PASS]" : "[FAIL]")} <b>TestRunner Complete</b>\n" +
        $"Mode: <b>{mode}</b>   Speed profile: <b>{speedProfile}</b>\n" +
        $"Cycles run: <b>{history.Count}/{maxCycles}</b>\n" +
        $"Total: PASS:{totalPass}  FAIL:{totalFail}{skipSummary}\n\n" +
        cycleLines;

    if (richConsole)
    {
        Console.WriteLine("\n" + summary
            .Replace("<b>", "").Replace("</b>", "")
            .Replace("<pre>", "").Replace("</pre>", ""));
    }
    else
    {
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            Event = "run_complete",
            Mode = mode,
            SpeedProfile = speedProfile,
            CyclesRun = history.Count,
            MaxCycles = maxCycles,
            TotalPassed = totalPass,
            TotalFailed = totalFail,
            TotalSkipped = totalSkipped,
            AllPassed = allGreen,
            Cycles = history.Select(h => new
            {
                h.Cycle,
                Passed = h.Result.Passed,
                Failed = h.Result.Failed,
                Skipped = h.Result.Skipped
            })
        }));
    }

    // Use CancellationToken.None for the final send — we always want this to go through
    await telegram.SendAsync(summary, CancellationToken.None);
}

if (ct.IsCancellationRequested && history.Count < maxCycles)
{
    await telegram.SendAsync(
        $"🛑 <b>TestRunner cancelled</b> after {history.Count}/{maxCycles} cycles.",
        CancellationToken.None);
}

if (webServer is not null) await webServer.DisposeAsync();

if (richConsole)
{
    Console.WriteLine("[Runner] Done. Test-target apps will be closed.");
    Console.WriteLine("\nPress any key to close...");
    if (!Console.IsInputRedirected)
        Console.ReadKey(intercept: true);
}
return 0;

static async Task<bool> WaitForWebTargetsAsync(RunnerConfig config, CancellationToken ct)
{
    using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
    var deadline = DateTime.UtcNow.AddSeconds(config.ApiReadyTimeoutSec);
    var pages = config.WebPagePaths.Length > 0 ? config.WebPagePaths : ["/"];

    while (DateTime.UtcNow < deadline && !ct.IsCancellationRequested)
    {
        var allReady = true;
        foreach (var page in pages)
        {
            var targetUrl = BuildWebUrl(config.WebBaseUrl, page);
            try
            {
                using var response = await http.GetAsync(targetUrl, ct);
                if (!response.IsSuccessStatusCode)
                {
                    allReady = false;
                    break;
                }
            }
            catch
            {
                allReady = false;
                break;
            }
        }

        if (allReady) return true;
        await Task.Delay(500, ct).ConfigureAwait(false);
    }

    return false;
}

static string? ResolveBrowserExe(string configured)
{
    if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured)) return configured;
    string[] candidates =
    {
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft", "Edge", "Application", "msedge.exe"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),    "Microsoft", "Edge", "Application", "msedge.exe"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),    "Google", "Chrome", "Application", "chrome.exe"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Google", "Chrome", "Application", "chrome.exe"),
    };
    foreach (var c in candidates)
        if (!string.IsNullOrEmpty(c) && File.Exists(c)) return c;
    return null;
}

static bool IsChromiumExe(string path)
{
    var name = Path.GetFileName(path).ToLowerInvariant();
    return name is "msedge.exe" or "chrome.exe" or "brave.exe" or "vivaldi.exe";
}

static string BuildWebUrl(string webBaseUrl, string pagePath)
{
    if (Uri.TryCreate(pagePath, UriKind.Absolute, out var absolute))
        return absolute.ToString();

    var baseUrl = webBaseUrl.TrimEnd('/');
    if (string.IsNullOrWhiteSpace(pagePath) || pagePath == "/")
        return baseUrl;

    return $"{baseUrl}/{pagePath.TrimStart('/')}";
}
