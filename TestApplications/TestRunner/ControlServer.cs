namespace ApexUIBridge.TestRunner;

using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;

/// <summary>
/// HTTP control surface for TestRunner. Exposes the test catalog, per-test
/// execution, bridge lifecycle, and a /shutdown endpoint so an external agent
/// (Claude, CI, a dashboard) can drive the runner without touching the CLI.
///
/// All state access is serialized through a single semaphore - the underlying
/// BridgeClient and TestSuite are not built for concurrent callers.
/// </summary>
public sealed class ControlServer : IAsyncDisposable
{
    private readonly HttpListener _listener = new();
    private readonly string _prefix;
    private readonly string? _apiKey;
    private readonly SemaphoreSlim _gate = new(1, 1);

    // Shared state owned by the caller and mutated by this server.
    private readonly RunnerConfig _config;
    private readonly string _configPath;
    private readonly Func<ProcessManager?> _getBridge;
    private readonly Func<ProcessManager, Task> _setBridge;
    private readonly Func<CancellationToken, Task<ProcessManager>> _startBridge;
    private readonly Func<BridgeClient> _getClient;
    private readonly Func<BridgeClient, Task> _setClient;
    private readonly Func<TestSuite> _getSuite;
    private readonly Func<TestSuite, Task> _setSuite;
    private readonly CancellationTokenSource _shutdownCts;

    private CycleResult? _latestCycle;
    private string? _latestCyclePath;
    private CancellationTokenSource? _loopCts;
    private Task? _loop;

    public string Prefix => _prefix;

    public ControlServer(
        string bindHost,
        int port,
        string? apiKey,
        RunnerConfig config,
        string configPath,
        Func<ProcessManager?> getBridge,
        Func<ProcessManager, Task> setBridge,
        Func<CancellationToken, Task<ProcessManager>> startBridge,
        Func<BridgeClient> getClient,
        Func<BridgeClient, Task> setClient,
        Func<TestSuite> getSuite,
        Func<TestSuite, Task> setSuite,
        CancellationTokenSource shutdownCts)
    {
        _prefix = $"http://{bindHost}:{port}/";
        _listener.Prefixes.Add(_prefix);
        _apiKey = string.IsNullOrWhiteSpace(apiKey) ? null : apiKey.Trim();

        _config        = config;
        _configPath    = configPath;
        _getBridge     = getBridge;
        _setBridge     = setBridge;
        _startBridge   = startBridge;
        _getClient     = getClient;
        _setClient     = setClient;
        _getSuite      = getSuite;
        _setSuite      = setSuite;
        _shutdownCts   = shutdownCts;

        _latestCyclePath = ResolveLatestCyclePath(config.TestResultsPath);
    }

    public void Start()
    {
        _listener.Start();
        _loopCts = new CancellationTokenSource();
        _loop = Task.Run(() => AcceptLoop(_loopCts.Token));
        Console.WriteLine($"[ControlServer] Listening on {_prefix}");
    }

    private async Task AcceptLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _listener.IsListening)
        {
            HttpListenerContext ctx;
            try { ctx = await _listener.GetContextAsync().ConfigureAwait(false); }
            catch (HttpListenerException) { return; }
            catch (ObjectDisposedException) { return; }

            _ = Task.Run(() => HandleAsync(ctx));
        }
    }

    private async Task HandleAsync(HttpListenerContext ctx)
    {
        var req = ctx.Request;
        var res = ctx.Response;
        try
        {
            if (!IsAuthed(req))
            {
                await WriteJson(res, 401, new { ok = false, error = "unauthorized" });
                return;
            }

            string method = req.HttpMethod.ToUpperInvariant();
            string path = (req.Url?.AbsolutePath ?? "/").TrimEnd('/').ToLowerInvariant();
            if (path == "") path = "/";

            switch ((method, path))
            {
                case ("GET",  "/health"):          await WriteJson(res, 200, new { ok = true }); return;
                case ("GET",  "/tests"):           await HandleListTests(res); return;
                case ("POST", "/tests/run"):       await HandleRunOne(req, res); return;
                case ("POST", "/tests/run-all"):   await HandleRunAll(res); return;
                case ("GET",  "/results/latest"):  await HandleLatestResults(res); return;
                case ("GET",  "/status"):          await HandleStatus(res); return;
                case ("POST", "/bridge/stop"):     await HandleBridgeStop(res); return;
                case ("POST", "/bridge/start"):    await HandleBridgeStart(res); return;
                case ("POST", "/bridge/restart"):  await HandleBridgeRestart(res); return;
                case ("POST", "/shutdown"):        await HandleShutdown(res); return;
                default:
                    await WriteJson(res, 404, new { ok = false, error = $"no route {method} {path}" });
                    return;
            }
        }
        catch (Exception ex)
        {
            try { await WriteJson(res, 500, new { ok = false, error = ex.Message }); }
            catch { /* client gone */ }
        }
    }

    // -- Handlers --------------------------------------------------------------

    private async Task HandleListTests(HttpListenerResponse res)
    {
        var suite = _getSuite();
        var payload = suite.Catalog.Select(t => new { id = t.Id, name = t.Name, group = t.Group }).ToArray();
        await WriteJson(res, 200, payload);
    }

    private async Task HandleRunOne(HttpListenerRequest req, HttpListenerResponse res)
    {
        string? id = req.QueryString["id"];
        if (string.IsNullOrWhiteSpace(id) && req.HasEntityBody)
        {
            using var sr = new StreamReader(req.InputStream, req.ContentEncoding);
            var body = await sr.ReadToEndAsync();
            if (!string.IsNullOrWhiteSpace(body))
            {
                try
                {
                    using var doc = JsonDocument.Parse(body);
                    if (doc.RootElement.TryGetProperty("id", out var idEl))
                        id = idEl.GetString();
                }
                catch { /* fall through - empty id handled below */ }
            }
        }

        if (string.IsNullOrWhiteSpace(id))
        {
            await WriteJson(res, 400, new { ok = false, error = "missing test id (?id= or {\"id\":...})" });
            return;
        }

        await _gate.WaitAsync();
        try
        {
            var suite = _getSuite();
            var result = await suite.RunOneAsync(id, _shutdownCts.Token);
            PersistSingleResult(result);
            await WriteJson(res, 200, ResultShape(result));
        }
        catch (KeyNotFoundException)
        {
            await WriteJson(res, 404, new { ok = false, error = $"no test with id '{id}'" });
        }
        finally { _gate.Release(); }
    }

    private async Task HandleRunAll(HttpListenerResponse res)
    {
        await _gate.WaitAsync();
        try
        {
            var suite = _getSuite();
            var sw = Stopwatch.StartNew();
            var cycle = await suite.RunAllAsync(_shutdownCts.Token);
            sw.Stop();
            _latestCycle = cycle;
            PersistCycle(cycle, sw.ElapsedMilliseconds);
            await WriteJson(res, 200, CycleShape(cycle, sw.ElapsedMilliseconds));
        }
        finally { _gate.Release(); }
    }

    private async Task HandleLatestResults(HttpListenerResponse res)
    {
        if (_latestCycle == null)
        {
            await WriteJson(res, 404, new { ok = false, error = "no cycle has been run yet" });
            return;
        }
        await WriteJson(res, 200, CycleShape(_latestCycle, null));
    }

    private async Task HandleStatus(HttpListenerResponse res)
    {
        var bridge = _getBridge();
        var payload = new
        {
            bridgeAlive = bridge?.IsRunning ?? false,
            lastCycleAt = _latestCycle?.RunAt,
            lastPassed = _latestCycle?.Passed,
            lastFailed = _latestCycle?.Failed,
            lastSkipped = _latestCycle?.Skipped,
            configPath = _configPath,
            bridgeBaseUrl = _config.BridgeBaseUrl
        };
        await WriteJson(res, 200, payload);
    }

    private async Task HandleBridgeStop(HttpListenerResponse res)
    {
        await _gate.WaitAsync();
        try
        {
            var bridge = _getBridge();
            if (bridge == null || !bridge.IsRunning)
            {
                await WriteJson(res, 200, new { ok = true, stopped = false, reason = "not running" });
                return;
            }
            await bridge.StopAsync();
            await WriteJson(res, 200, new { ok = true, stopped = true });
        }
        finally { _gate.Release(); }
    }

    private async Task HandleBridgeStart(HttpListenerResponse res)
    {
        await _gate.WaitAsync();
        try
        {
            var existing = _getBridge();
            if (existing is { IsRunning: true })
            {
                await WriteJson(res, 200, new { ok = true, started = false, reason = "already running" });
                return;
            }
            var pm = await _startBridge(_shutdownCts.Token);
            await _setBridge(pm);
            // Rebuild client + suite against the fresh bridge.
            var client = new BridgeClient(_config.BridgeBaseUrl, _config.BridgeApiKey);
            var ready = await client.WaitForReadyAsync(_config.ApiReadyTimeoutSec, _shutdownCts.Token);
            await _setClient(client);
            await _setSuite(new TestSuite(client));
            await WriteJson(res, 200, new { ok = true, started = true, ready });
        }
        finally { _gate.Release(); }
    }

    private async Task HandleBridgeRestart(HttpListenerResponse res)
    {
        await _gate.WaitAsync();
        try
        {
            var existing = _getBridge();
            if (existing is { IsRunning: true })
                await existing.StopAsync();

            var pm = await _startBridge(_shutdownCts.Token);
            await _setBridge(pm);
            var client = new BridgeClient(_config.BridgeBaseUrl, _config.BridgeApiKey);
            var ready = await client.WaitForReadyAsync(_config.ApiReadyTimeoutSec, _shutdownCts.Token);
            await _setClient(client);
            await _setSuite(new TestSuite(client));
            await WriteJson(res, 200, new { ok = true, restarted = true, ready });
        }
        finally { _gate.Release(); }
    }

    private async Task HandleShutdown(HttpListenerResponse res)
    {
        await WriteJson(res, 200, new { ok = true, message = "shutting down" });
        // Fire-and-forget so the response flushes first.
        _ = Task.Run(async () =>
        {
            await Task.Delay(200);
            _shutdownCts.Cancel();
        });
    }

    // -- Persistence -----------------------------------------------------------

    private void PersistCycle(CycleResult cycle, long elapsedMs)
    {
        if (string.IsNullOrWhiteSpace(_latestCyclePath)) return;
        try
        {
            var json = JsonSerializer.Serialize(
                CycleShape(cycle, elapsedMs),
                new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_latestCyclePath!, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ControlServer] Warning: could not persist latest-cycle: {ex.Message}");
        }

        // Merge into TestResultsPath so RunOnlyFailed works across sessions.
        TryMergeTestResults(cycle);
    }

    private void PersistSingleResult(TestResult result)
    {
        // A single-test run still updates TestResultsPath so RunOnlyFailed stays accurate.
        TryMergeTestResults(new CycleResult(new[] { result }));
    }

    private void TryMergeTestResults(CycleResult cycle)
    {
        try
        {
            var saved = new Dictionary<string, bool>();
            if (File.Exists(_config.TestResultsPath))
            {
                var existing = JsonSerializer.Deserialize<Dictionary<string, bool>>(
                    File.ReadAllText(_config.TestResultsPath));
                if (existing != null) saved = existing;
            }
            foreach (var r in cycle.Results.Where(r => !r.Skipped))
                saved[r.Name] = r.Passed;
            File.WriteAllText(_config.TestResultsPath,
                JsonSerializer.Serialize(saved, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ControlServer] Warning: could not persist test results: {ex.Message}");
        }
    }

    private static string? ResolveLatestCyclePath(string testResultsPath)
    {
        if (string.IsNullOrWhiteSpace(testResultsPath)) return null;
        var dir = Path.GetDirectoryName(testResultsPath);
        if (string.IsNullOrWhiteSpace(dir)) dir = Path.GetTempPath();
        return Path.Combine(dir!, "ApexUIBridge_latest_cycle.json");
    }

    // -- Shape helpers ---------------------------------------------------------

    private static object ResultShape(TestResult r) => new
    {
        r.Name,
        r.Passed,
        r.Skipped,
        r.Detail,
        r.Command,
        r.ElapsedMs
    };

    private static object CycleShape(CycleResult c, long? elapsedMs) => new
    {
        runAt = c.RunAt,
        passed = c.Passed,
        failed = c.Failed,
        skipped = c.Skipped,
        allPassed = c.AllPassed,
        totalMs = elapsedMs,
        results = c.Results.Select(r => new
        {
            r.Name, r.Passed, r.Skipped, r.Detail, r.Command, r.ElapsedMs
        }).ToArray()
    };

    // -- Plumbing --------------------------------------------------------------

    private bool IsAuthed(HttpListenerRequest req)
    {
        if (_apiKey == null) return true;
        string? key = req.Headers["X-Api-Key"];
        if (string.IsNullOrEmpty(key))
        {
            string? auth = req.Headers["Authorization"];
            if (!string.IsNullOrEmpty(auth) && auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                key = auth["Bearer ".Length..].Trim();
        }
        if (string.IsNullOrEmpty(key))
            key = req.QueryString["apiKey"];
        return string.Equals(key, _apiKey, StringComparison.Ordinal);
    }

    private static async Task WriteJson(HttpListenerResponse res, int status, object payload)
    {
        res.StatusCode = status;
        res.ContentType = "application/json; charset=utf-8";
        var json = JsonSerializer.Serialize(payload);
        var bytes = Encoding.UTF8.GetBytes(json);
        res.ContentLength64 = bytes.Length;
        await res.OutputStream.WriteAsync(bytes);
        res.OutputStream.Close();
    }

    public async ValueTask DisposeAsync()
    {
        try { _loopCts?.Cancel(); } catch { }
        try { _listener.Stop(); } catch { }
        if (_loop is not null)
        {
            try { await _loop.ConfigureAwait(false); } catch { }
        }
        _listener.Close();
        _gate.Dispose();
        Console.WriteLine("[ControlServer] Stopped.");
    }
}

