namespace ApexUIBridge.TestRunner;

using System.Net.Http.Json;
using System.Text.Json.Nodes;

/// <summary>Thin non-blocking wrapper around the ApexUIBridge local HTTP API.</summary>
public sealed class BridgeClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly string     _base;

    public BridgeClient(string baseUrl)
    {
        _base = baseUrl.TrimEnd('/');
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
    }

    /// <summary>POST a bridge command and return the parsed response.</summary>
    public async Task<BridgeResponse> SendAsync(string command, CancellationToken ct)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync($"{_base}/command",
                new { command }, ct);
            var json = JsonNode.Parse(await resp.Content.ReadAsStringAsync(ct));
            return new BridgeResponse(
                json?["success"]?.GetValue<bool>()  ?? false,
                json?["message"]?.GetValue<string>() ?? string.Empty,
                json?["data"]?.GetValue<string>());
        }
        catch (Exception ex)
        {
            return new BridgeResponse(false, ex.Message, null);
        }
    }

    /// <summary>Poll /status until it responds 200 or the timeout elapses.</summary>
    public async Task<bool> WaitForReadyAsync(int timeoutSeconds, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        while (DateTime.UtcNow < deadline && !ct.IsCancellationRequested)
        {
            try
            {
                var r = await _http.GetAsync($"{_base}/status", ct);
                if (r.IsSuccessStatusCode) return true;
            }
            catch { /* not up yet */ }
            await Task.Delay(500, ct).ConfigureAwait(false);
        }
        return false;
    }

    public void Dispose() => _http.Dispose();
}

public sealed record BridgeResponse(bool Success, string Message, string? Data);
