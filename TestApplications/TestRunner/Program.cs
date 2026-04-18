using ApexUIBridge.TestRunner;
using System.Text.Json;

// ── Config ─────────────────────────────────────────────────────────────────────
string? cliMode = null;
string? cliConfigPath = null;
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

var mode = (cliMode ?? config.Mode ?? "demo").Trim().ToLowerInvariant();
if (mode != "demo" && mode != "benchmark")
{
    Console.Error.WriteLine($"Invalid mode '{mode}'. Use 'demo' or 'benchmark'.");
    return 1;
}

var maxCycles = mode == "benchmark" ? Math.Max(config.MaxCycles, 10) : Math.Min(config.MaxCycles, 3);
var reportEveryN = mode == "benchmark"
    ? (config.ReportEveryN <= 0 ? 0 : Math.Max(config.ReportEveryN, 10))
    : (config.ReportEveryN <= 0 ? 1 : Math.Min(config.ReportEveryN, 1));
var runOnlyFailed = mode == "benchmark" ? true : config.RunOnlyFailed;
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
        Console.WriteLine($"[Runner] RunOnlyFailed=true — {previouslyPassed.Count} previously-passed tests will be skipped.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Runner] Warning: could not load {config.TestResultsPath}: {ex.Message}");
    }
}
else if (runOnlyFailed)
{
    Console.WriteLine("[Runner] RunOnlyFailed=true but no previous results file found — running all tests.");
}

// ── Launch the 3rd-party test-target apps once — they stay running the whole time
await using var winFormsApp = new ProcessManager("WinForms Test App", config.WinFormsExePath, isGui: true);
await using var wpfApp = new ProcessManager("WPF Test App", config.WpfExePath, isGui: true);

if (richConsole) Console.WriteLine("[Runner] Starting test-target applications...");
await winFormsApp.StartAsync(ct);
await wpfApp.StartAsync(ct);
await Task.Delay(3000, ct);  // give GUIs time to render before scanning

await telegram.SendAsync(
    $"🚀 <b>TestRunner started</b>\n" +
    $"Mode: <b>{mode}</b>   Speed profile: <b>{speedProfile}</b>\n" +
    $"Cycles: <b>{maxCycles}</b>   Config: <b>{config.BuildConfiguration}</b>\n" +
    $"Reports every: <b>{reportEveryN}</b> cycle(s)\n" +
    $"Send /stop-tests to cancel at any time.", ct);

// ── Main loop ──────────────────────────────────────────────────────────────────
var history = new List<(int Cycle, CycleResult Result)>();
int cycle = 0;

while (cycle < maxCycles && !ct.IsCancellationRequested)
{
    cycle++;
    if (richConsole)
    {
        Console.WriteLine($"\n{"─",60}");
        Console.WriteLine($"[Runner] Cycle {cycle}/{maxCycles}");
        Console.WriteLine($"{"─",60}");
    }

    // 1. Build ─────────────────────────────────────────────────────────────────
    if (richConsole) Console.WriteLine("[Runner] Building ApexUIBridge...");
    var build = await builder.BuildAsync(ct);
    if (!build.Success)
    {
        var snippet = build.Output.Length > 600
            ? build.Output[..600] + "…"
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
        // A build failure is fatal — no point retrying without a code change
        break;
    }
    if (richConsole) Console.WriteLine("[Runner] Build OK.");

    // 2. Launch ApexUIBridge ───────────────────────────────────────────────────
    await using var bridge = new ProcessManager("ApexUIBridge", config.BridgeExePath, isGui: true);
    await bridge.StartAsync(ct);

    using var client = new BridgeClient(config.BridgeBaseUrl);
    if (richConsole) Console.WriteLine("[Runner] Waiting for Bridge API...");
    var ready = await client.WaitForReadyAsync(config.ApiReadyTimeoutSec, ct);
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
        continue;
    }
    if (richConsole) Console.WriteLine("[Runner] Bridge API ready.");

    // 3. Run test suite ────────────────────────────────────────────────────────
    var result = await new TestSuite(client, runOnlyFailed ? previouslyPassed : null).RunAsync(ct);
    history.Add((cycle, result));

    // Print to console
    if (richConsole)
    {
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
                r.Detail
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
        Console.WriteLine($"[Runner] Warning: could not save results: {ex.Message}");
    }

    // 4. Stop Bridge before next cycle (fresh build will produce new binary) ───
    await bridge.StopAsync();
    await Task.Delay(500, ct);  // brief gap — no blocking, just lets OS release ports

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
            var sk = h.Result.Skipped > 0 ? $" ⏭️{h.Result.Skipped}" : "";
            return $"  Cycle {h.Cycle}: ✅{h.Result.Passed} ❌{h.Result.Failed}{sk}";
        }));

    var skipSummary = totalSkipped > 0 ? $"   ⏭️ {totalSkipped} skipped" : "";
    var summary =
        $"{(allGreen ? "🏆" : "⚠️")} <b>TestRunner Complete</b>\n" +
        $"Mode: <b>{mode}</b>   Speed profile: <b>{speedProfile}</b>\n" +
        $"Cycles run: <b>{history.Count}/{maxCycles}</b>\n" +
        $"Total: ✅ {totalPass} passed   ❌ {totalFail} failed{skipSummary}\n\n" +
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

if (richConsole) Console.WriteLine("[Runner] Done. Test-target apps will be closed.");
return 0;
