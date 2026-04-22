namespace ApexUIBridge.TestRunner;

using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

/// <summary>
/// HTTP client for the ApexComputerUse bridge. Uses the same real routes an AI
/// coding agent would hit via curl — GET /windows, POST /find, GET /elements,
/// POST /execute, GET /help — with ?format=json so responses always come back
/// as canonical {success, action, data, error} JSON.
/// </summary>
public sealed class BridgeClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly string     _base;

    /// <summary>Optional per-request logger. Set by the runner to surface every
    /// HTTP call (find / click / type / setrange / scan / …) to the console so
    /// test failures can be diagnosed from the transcript alone.</summary>
    public Action<string>? Logger { get; set; }

    public BridgeClient(string baseUrl, string? apiKey = null, Action<string>? logger = null)
    {
        _base = baseUrl.TrimEnd('/');
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        _http.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        if (!string.IsNullOrWhiteSpace(apiKey))
            _http.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
        Logger = logger;
    }

    // ── Low-level: the exact curl equivalents ────────────────────────────────

    /// <summary>GET {route}?format=json — mirrors `curl -H 'X-Api-Key: …' {route}?format=json`.</summary>
    public async Task<ApiResponse> GetAsync(string route, CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var resp = await _http.GetAsync($"{_base}{WithJson(route)}", ct);
            var parsed = await ParseAsync(resp, ct);
            sw.Stop();
            LogCall("GET", route, null, parsed, sw.ElapsedMilliseconds);
            return parsed;
        }
        catch (Exception ex)
        {
            sw.Stop();
            var fail = ApiResponse.Fail(ex.Message);
            LogCall("GET", route, null, fail, sw.ElapsedMilliseconds);
            return fail;
        }
    }

    /// <summary>POST {route}?format=json with a JSON body — mirrors `curl -X POST -d '{…}'`.</summary>
    public async Task<ApiResponse> PostJsonAsync(string route, object body, CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var resp = await _http.PostAsJsonAsync($"{_base}{WithJson(route)}", body, ct);
            var parsed = await ParseAsync(resp, ct);
            sw.Stop();
            LogCall("POST", route, body, parsed, sw.ElapsedMilliseconds);
            return parsed;
        }
        catch (Exception ex)
        {
            sw.Stop();
            var fail = ApiResponse.Fail(ex.Message);
            LogCall("POST", route, body, fail, sw.ElapsedMilliseconds);
            return fail;
        }
    }

    private void LogCall(string method, string route, object? body, ApiResponse r, long ms)
    {
        var log = Logger;
        if (log == null) return;
        string bodyStr = "";
        if (body != null)
        {
            try { bodyStr = " " + JsonSerializer.Serialize(body); }
            catch { bodyStr = " <unserializable>"; }
        }
        string outcome;
        if (!r.Success)
        {
            outcome = $"FAIL {r.Error ?? r.Message ?? "<no error>"}";
        }
        else
        {
            var msg = r.Message;
            var snippet = r.Result;
            if (!string.IsNullOrEmpty(snippet) && snippet!.Length > 160) snippet = snippet[..160] + "…";
            outcome = "OK" + (string.IsNullOrEmpty(msg) ? "" : $" {msg}")
                           + (string.IsNullOrEmpty(snippet) ? "" : $" | {snippet}");
        }
        log($"[API] {method} {route}{bodyStr} → {outcome} ({ms}ms)");
    }

    // ── High-level: what tests actually want to say ──────────────────────────

    public Task<ApiResponse> ListWindowsAsync(CancellationToken ct)        => GetAsync("/windows", ct);
    public Task<ApiResponse> FindWindowAsync(string title, CancellationToken ct) => PostJsonAsync("/find", new { window = title }, ct);
    public Task<ApiResponse> ScanElementsAsync(CancellationToken ct)      => GetAsync("/elements", ct);
    public Task<ApiResponse> HelpAsync(CancellationToken ct)              => GetAsync("/help", ct);

    /// <summary>POST /execute — e.g. ExecuteAsync(42, "click") or ExecuteAsync(7, "type", "hello").</summary>
    public Task<ApiResponse> ExecuteAsync(long elementId, string action, string? value, CancellationToken ct) =>
        PostJsonAsync("/execute", new { id = elementId.ToString(), action, value = value ?? "" }, ct);

    /// <summary>find+elements in one call — the "scan a window" convenience.</summary>
    public async Task<ApiResponse> ScanWindowAsync(string title, CancellationToken ct)
    {
        var find = await FindWindowAsync(title, ct);
        if (!find.Success) return find;
        return await ScanElementsAsync(ct);
    }

    /// <summary>Poll /health (unauthenticated) until it responds 200 or the timeout elapses.</summary>
    public async Task<bool> WaitForReadyAsync(int timeoutSeconds, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        while (DateTime.UtcNow < deadline && !ct.IsCancellationRequested)
        {
            try
            {
                var r = await _http.GetAsync($"{_base}/health", ct);
                if (r.IsSuccessStatusCode) return true;
            }
            catch { /* not up yet */ }
            await Task.Delay(500, ct).ConfigureAwait(false);
        }
        return false;
    }

    public void Dispose() => _http.Dispose();

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string WithJson(string route) =>
        route.Contains('?') ? $"{route}&format=json" : $"{route}?format=json";

    private static async Task<ApiResponse> ParseAsync(HttpResponseMessage resp, CancellationToken ct)
    {
        string body = await resp.Content.ReadAsStringAsync(ct);
        if (string.IsNullOrWhiteSpace(body))
            return new ApiResponse(resp.IsSuccessStatusCode, null, resp.IsSuccessStatusCode ? null : $"HTTP {(int)resp.StatusCode}", null, null);

        JsonNode? json;
        try { json = JsonNode.Parse(body); }
        catch (JsonException ex)
        {
            return ApiResponse.Fail($"Non-JSON response (HTTP {(int)resp.StatusCode}): {ex.Message}");
        }

        bool    success = json?["success"]?.GetValue<bool>() ?? resp.IsSuccessStatusCode;
        string? action  = json?["action"]?.GetValue<string>();
        string? error   = json?["error"]?.GetValue<string>();
        var     data    = json?["data"];
        // ApexResult flattens CommandResponse into {data:{result, message}} — surface both.
        string? result  = data?["result"]?.GetValue<string>();
        string? message = data?["message"]?.GetValue<string>();
        return new ApiResponse(success, action, error, result, message);
    }
}

/// <summary>Parsed bridge response — mirrors the server's {success, action, data:{result,message}, error} JSON.</summary>
public sealed record ApiResponse(
    bool    Success,
    string? Action,
    string? Error,
    string? Result,   // ← server put the payload (element tree JSON, help text, etc.) here
    string? Message)  // ← human-readable summary ("3 element(s)", "'click' executed.", …)
{
    public static ApiResponse Fail(string error) => new(false, null, error, null, null);

    /// <summary>The best human-readable description — error, message, or truncated result.</summary>
    public string Detail => Error ?? Message ?? Result ?? "";
}
