namespace ApexUIBridge.TestRunner;

using System.Net.Http.Json;

/// <summary>
/// Sends messages to a Telegram chat via the Bot API.
/// If token/chatId are not configured the message is written to stdout instead.
/// All sends are fire-and-forget safe — errors are logged, never thrown.
/// </summary>
public sealed class TelegramNotifier : IDisposable
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(10) };
    private readonly string _token;
    private readonly long   _chatId;
    private readonly bool   _enabled;

    public TelegramNotifier(string token, long chatId)
    {
        _token   = token;
        _chatId  = chatId;
        _enabled = !string.IsNullOrWhiteSpace(token) && chatId != 0;
    }

    public async Task SendAsync(string message, CancellationToken ct = default)
    {
        if (!_enabled)
        {
            Console.WriteLine($"[Telegram→stdout] {message}");
            return;
        }
        try
        {
            await _http.PostAsJsonAsync(
                $"https://api.telegram.org/bot{_token}/sendMessage",
                new { chat_id = _chatId, text = message, parse_mode = "HTML" },
                ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Never let a Telegram failure crash the test loop
            Console.WriteLine($"[Telegram] Send failed: {ex.Message}");
        }
    }

    public void Dispose() => _http.Dispose();
}
