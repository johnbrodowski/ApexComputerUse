using ApexUIBridge.TestRunner;
using System.Diagnostics;
using System.Text.Json;

// ── Config ─────────────────────────────────────────────────────────────────────
//var configPath = args.FirstOrDefault() ?? "runner-config.json";


var configPath =  Path.Combine(AppContext.BaseDirectory, "runner-config.json");


if (!File.Exists(configPath))
{
    Console.Error.WriteLine($"Config not found: {configPath}");
    Console.Error.WriteLine("Usage: TestRunner [path-to-runner-config.json]");
    return 1;
}

var config = JsonSerializer.Deserialize<RunnerConfig>(
    File.ReadAllText(configPath),
    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

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

var ct       = cts.Token;
var telegram = new TelegramNotifier(config.TelegramBotToken, config.TelegramChatId);
var builder  = new BuildRunner(config.SolutionPath, config.BuildConfiguration);

var normalizedSpeedProfile = (config.SpeedProfile ?? "Normal").Trim();
var (defaultActionDelayMs, defaultUiSettleDelayMs) = normalizedSpeedProfile.ToLowerInvariant() switch
{
    "fast" => (50, 120),
    "human" => (350, 900),
    _ => (120, 300)
};
var actionDelayMs = config.ActionDelayMs > 0 ? config.ActionDelayMs : defaultActionDelayMs;
var uiSettleDelayMs = config.UiSettleDelayMs > 0 ? config.UiSettleDelayMs : defaultUiSettleDelayMs;
Console.WriteLine($"[Runner] Speed profile: {normalizedSpeedProfile} (ActionDelayMs={actionDelayMs}, UiSettleDelayMs={uiSettleDelayMs})");

// ── Load previous test results for skip-passed logic ────────────────────────
var previouslyPassed = new HashSet<string>();
if (config.RunOnlyFailed && File.Exists(config.TestResultsPath))
{
    try
    {
        var saved = JsonSerializer.Deserialize<Dictionary<string, bool>>(
            File.ReadAllText(config.TestResultsPath),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (saved != null)
            foreach (var kv in saved.Where(kv => kv.Value))
                previouslyPassed.Add(kv.Key);
        Console.WriteLine($"[Runner] RunOnlyFailed=true — {previouslyPassed.Count} previously-passed tests will be skipped.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Runner] Warning: could not load {config.TestResultsPath}: {ex.Message}");
    }
}
else if (config.RunOnlyFailed)
{
    Console.WriteLine("[Runner] RunOnlyFailed=true but no previous results file found — running all tests.");
}

// ── Launch the 3rd-party test-target apps once — they stay running the whole time
await using var winFormsApp = new ProcessManager("WinForms Test App", config.WinFormsExePath, isGui: true);
await using var wpfApp      = new ProcessManager("WPF Test App",      config.WpfExePath,      isGui: true);

Console.WriteLine("[Runner] Starting test-target applications...");
await winFormsApp.StartAsync(ct);
await wpfApp.StartAsync(ct);
await Task.Delay(3000, ct);  // give GUIs time to render before scanning

await telegram.SendAsync(
    $"🚀 <b>TestRunner started</b>\n" +
    $"Cycles: <b>{config.MaxCycles}</b>   Config: <b>{config.BuildConfiguration}</b>\n" +
    $"Reports every: <b>{config.ReportEveryN}</b> cycle(s)\n" +
    $"Send /stop-tests to cancel at any time.", ct);

// ── Main loop ──────────────────────────────────────────────────────────────────
var history = new List<(int Cycle, CycleResult Result)>();
int cycle   = 0;

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
            config.SpeedProfile,
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
        Console.WriteLine($"[Runner] Warning: could not append benchmark record: {ex.Message}");
    }
}

while (cycle < config.MaxCycles && !ct.IsCancellationRequested)
{
    cycle++;
    var cycleStartedAtUtc = DateTimeOffset.UtcNow;
    var cycleTimer = Stopwatch.StartNew();
    long buildMs = 0;
    long bridgeReadyMs = 0;
    long suiteMs = 0;
    Console.WriteLine($"\n{"─",60}");
    Console.WriteLine($"[Runner] Cycle {cycle}/{config.MaxCycles}");
    Console.WriteLine($"{"─",60}");

    // 1. Build ─────────────────────────────────────────────────────────────────
    Console.WriteLine("[Runner] Building ApexUIBridge...");
    var buildTimer = Stopwatch.StartNew();
    var build = await builder.BuildAsync(ct);
    buildTimer.Stop();
    buildMs = buildTimer.ElapsedMilliseconds;
    if (!build.Success)
    {
        var snippet = build.Output.Length > 600
            ? build.Output[..600] + "…"
            : build.Output;
        Console.WriteLine($"[Runner] Build FAILED (exit {build.ExitCode}):\n{snippet}");
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
    Console.WriteLine("[Runner] Build OK.");

    // 2. Launch ApexUIBridge ───────────────────────────────────────────────────
    await using var bridge = new ProcessManager("ApexUIBridge", config.BridgeExePath, isGui: true);
    await bridge.StartAsync(ct);

    using var client = new BridgeClient(config.BridgeBaseUrl);
    Console.WriteLine("[Runner] Waiting for Bridge API...");
    var readyTimer = Stopwatch.StartNew();
    var ready = await client.WaitForReadyAsync(config.ApiReadyTimeoutSec, ct);
    readyTimer.Stop();
    bridgeReadyMs = readyTimer.ElapsedMilliseconds;
    if (!ready)
    {
        Console.WriteLine("[Runner] Bridge API did not become ready in time — skipping cycle.");
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
    Console.WriteLine("[Runner] Bridge API ready.");

    // 2b. Ensure web targets are reachable before discovery/interactions ───────
    if (!string.IsNullOrWhiteSpace(config.WebBaseUrl))
    {
        Console.WriteLine("[Runner] Waiting for web target pages...");
        var webReady = await WaitForWebTargetsAsync(config, ct);
        if (!webReady)
        {
            Console.WriteLine("[Runner] Web target did not become ready in time — skipping cycle.");
            await telegram.SendAsync(
                $"⚠️ <b>Cycle {cycle}</b> — Web target not ready after {config.ApiReadyTimeoutSec}s", ct);
            await bridge.StopAsync();
            continue;
        }
        Console.WriteLine("[Runner] Web target pages ready.");
    }

    // 3. Run test suite ────────────────────────────────────────────────────────
    var suiteTimer = Stopwatch.StartNew();
    var result = await new TestSuite(client, config.RunOnlyFailed ? previouslyPassed : null).RunAsync(ct);
    suiteTimer.Stop();
    suiteMs = suiteTimer.ElapsedMilliseconds;
    history.Add((cycle, result));

    // Print to console
    var skippedMsg = result.Skipped > 0 ? $", {result.Skipped} skipped" : "";
    Console.WriteLine($"\n[Results] {result.Passed} passed, {result.Failed} failed{skippedMsg}");
    foreach (var r in result.Results)
    {
        if (r.Skipped)
            Console.WriteLine($"  ⏭️ {r.Name} (skipped — previously passed)");
        else
            Console.WriteLine($"  {(r.Passed ? "✅" : "❌")} {r.Name}" +
                              (r.Passed ? "" : $"\n       ↳ {r.Detail}"));
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
    bool isLast    = cycle == config.MaxCycles;
    bool reportNow = config.ReportEveryN > 0 && (cycle % config.ReportEveryN == 0 || isLast);
    if (reportNow)
    {
        await telegram.SendAsync(
            $"📊 <b>Cycle {cycle}/{config.MaxCycles}</b>\n{result.Summary()}", ct);
    }
}

// ── Final summary ──────────────────────────────────────────────────────────────
if (history.Count > 0)
{
    var totalPass    = history.Sum(h => h.Result.Passed);
    var totalFail    = history.Sum(h => h.Result.Failed);
    var totalSkipped = history.Sum(h => h.Result.Skipped);
    var allGreen     = history.All(h => h.Result.AllPassed);
    var cycleLines   = string.Join("\n",
        history.Select(h =>
        {
            var sk = h.Result.Skipped > 0 ? $" ⏭️{h.Result.Skipped}" : "";
            return $"  Cycle {h.Cycle}: ✅{h.Result.Passed} ❌{h.Result.Failed}{sk}";
        }));

    var skipSummary = totalSkipped > 0 ? $"   ⏭️ {totalSkipped} skipped" : "";
    var summary =
        $"{(allGreen ? "🏆" : "⚠️")} <b>TestRunner Complete</b>\n" +
        $"Cycles run: <b>{history.Count}/{config.MaxCycles}</b>\n" +
        $"Total: ✅ {totalPass} passed   ❌ {totalFail} failed{skipSummary}\n\n" +
        cycleLines;

    Console.WriteLine("\n" + summary
        .Replace("<b>", "").Replace("</b>", "")
        .Replace("<pre>", "").Replace("</pre>", ""));

    // Use CancellationToken.None for the final send — we always want this to go through
    await telegram.SendAsync(summary, CancellationToken.None);
}

if (ct.IsCancellationRequested && history.Count < config.MaxCycles)
{
    await telegram.SendAsync(
        $"🛑 <b>TestRunner cancelled</b> after {history.Count}/{config.MaxCycles} cycles.",
        CancellationToken.None);
}

Console.WriteLine("[Runner] Done. Test-target apps will be closed.");
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

static string BuildWebUrl(string webBaseUrl, string pagePath)
{
    if (Uri.TryCreate(pagePath, UriKind.Absolute, out var absolute))
        return absolute.ToString();

    var baseUrl = webBaseUrl.TrimEnd('/');
    if (string.IsNullOrWhiteSpace(pagePath) || pagePath == "/")
        return baseUrl;

    return $"{baseUrl}/{pagePath.TrimStart('/')}";
}
