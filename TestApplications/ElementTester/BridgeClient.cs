namespace ApexUIBridge.ElementTester;

using System.Net.Http.Json;
using System.Text.Json.Nodes;

public sealed class BridgeClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly string     _base;

    public BridgeClient(string baseUrl = "http://localhost:8765")
    {
        _base = baseUrl.TrimEnd('/');
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
    }

    public async Task<BridgeResponse> SendAsync(string command, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync($"{_base}/command", new { command }, ct);
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

    public async Task<bool> IsReadyAsync(CancellationToken ct = default)
    {
        try
        {
            var r = await _http.GetAsync($"{_base}/status", ct);
            return r.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public void Dispose() => _http.Dispose();
}

public sealed record BridgeResponse(bool Success, string Message, string? Data);
