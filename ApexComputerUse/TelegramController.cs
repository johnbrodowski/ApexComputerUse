using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace ApexComputerUse
{
    /// <summary>
    /// Telegram bot controller exposing the same command set as the HTTP server.
    ///
    /// Bot commands (send in any chat with the bot):
    ///   /find    window=&lt;title&gt; [id=&lt;automationId&gt;] [name=&lt;name&gt;] [type=&lt;ControlType&gt;]
    ///   /exec    action=&lt;action&gt; [value=&lt;input&gt;]
    ///   /capture [action=screen|window|element|elements] [value=id1,id2,...]
    ///   /ocr     [value=x,y,w,h]
    ///   /ai      action=&lt;init|status|describe|file|ask&gt; ...
    ///   /status
    ///   /windows
    ///   /elements [type=&lt;ControlType&gt;]
    ///   /help
    /// </summary>
    public class TelegramController : IDisposable
    {
        private readonly TelegramBotClient _bot;
        private readonly CommandProcessor  _processor;
        private CancellationTokenSource?   _cts;

        public bool IsRunning { get; private set; }
        public event Action<string>? OnLog;

        public TelegramController(string token, CommandProcessor processor)
        {
            _bot       = new TelegramBotClient(token);
            _processor = processor;
        }

        // ── Lifecycle ─────────────────────────────────────────────────────

        public void Start()
        {
            if (IsRunning) return;
            _cts = new CancellationTokenSource();
            _bot.StartReceiving(
                updateHandler: HandleUpdateAsync,
                errorHandler:  HandleErrorAsync,
                receiverOptions: new ReceiverOptions
                {
                    AllowedUpdates   = [UpdateType.Message],
                    DropPendingUpdates = true
                },
                cancellationToken: _cts.Token);
            IsRunning = true;
            OnLog?.Invoke("Telegram bot started and polling.");
        }

        public void Stop()
        {
            if (!IsRunning) return;
            _cts?.Cancel();
            IsRunning = false;
            OnLog?.Invoke("Telegram bot stopped.");
        }

        // ── Update handler ────────────────────────────────────────────────

        private async Task HandleUpdateAsync(ITelegramBotClient bot, Update update, CancellationToken ct)
        {
            if (update.Message?.Text is not { } text) return;

            long   chatId   = update.Message.Chat.Id;
            string username = update.Message.From?.Username ?? "unknown";
            OnLog?.Invoke($"[Telegram] @{username}: {text}");

            var req = ParseCommand(text);
            if (req == null)
            {
                await bot.SendMessage(chatId, "Send a command starting with /  (try /help)",
                    cancellationToken: ct);
                return;
            }

            var response = _processor.Process(req);

            // Capture commands: send as photo instead of text
            if (response.Success && response.Data != null && req.Command == "capture")
            {
                try
                {
                    var bytes = Convert.FromBase64String(response.Data);
                    using var stream = new MemoryStream(bytes);
                    await bot.SendPhoto(chatId,
                        Telegram.Bot.Types.InputFile.FromStream(stream, "capture.png"),
                        caption: response.Message,
                        cancellationToken: ct);
                    return;
                }
                catch { /* fall through to text on error */ }
            }

            string reply = response.ToText();

            // Telegram caps messages at 4096 characters
            if (reply.Length > 4000)
                reply = reply[..4000] + "\n...(truncated)";

            await bot.SendMessage(chatId, reply, cancellationToken: ct);
        }

        private Task HandleErrorAsync(ITelegramBotClient bot, Exception ex,
            HandleErrorSource source, CancellationToken ct)
        {
            OnLog?.Invoke($"[Telegram Error] {source}: {ex.Message}");
            return Task.CompletedTask;
        }

        // ── Command parser ────────────────────────────────────────────────

        private static CommandRequest? ParseCommand(string text)
        {
            if (!text.StartsWith('/')) return null;

            // Strip leading / and optional @botname suffix
            var parts  = text.TrimStart('/').Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            string cmd = parts[0].Split('@')[0].ToLowerInvariant();
            string args = parts.Length > 1 ? parts[1] : "";

            var kv = ParseKeyValues(args);

            return cmd switch
            {
                "find" => new CommandRequest
                {
                    Command      = "find",
                    Window       = kv.Get("window", "w"),
                    AutomationId = kv.Get("id", "automationid"),
                    ElementName  = kv.Get("name", "n"),
                    SearchType   = kv.Get("type", "t")
                },
                "execute" or "exec" => new CommandRequest
                {
                    Command = "execute",
                    Action  = kv.Get("action", "a"),
                    Value   = kv.Get("value", "v")
                },
                "ocr" => new CommandRequest
                {
                    Command = "ocr",
                    Value   = kv.Get("value", "region") ?? (args.Contains(',') ? args : null)
                },
                "capture" => new CommandRequest
                {
                    Command = "capture",
                    Action  = kv.Get("action", "a"),
                    Value   = kv.Get("value", "v")
                },
                "status"   => new CommandRequest { Command = "status" },
                "windows"  => new CommandRequest { Command = "windows" },
                "elements" => new CommandRequest
                {
                    Command    = "elements",
                    SearchType = kv.Get("type", "t") ?? (args.Length > 0 ? args : null)
                },
                "ai" => new CommandRequest
                {
                    Command      = "ai",
                    Action       = kv.Get("action", "a") ?? (args.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.ToLowerInvariant()),
                    ModelPath    = kv.Get("model"),
                    MmProjPath   = kv.Get("proj"),
                    Value        = kv.Get("value", "path", "v"),
                    Prompt       = kv.Get("prompt", "p")
                },
                "help" or "start" => new CommandRequest { Command = "help" },
                _ => new CommandRequest { Command = cmd, Action = cmd, Value = args }
            };
        }

        /// Parses "key=value key2="multi word value" ..." into a dictionary.
        private static Dictionary<string, string> ParseKeyValues(string input)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            int i = 0;
            while (i < input.Length)
            {
                while (i < input.Length && input[i] == ' ') i++;
                if (i >= input.Length) break;

                int keyStart = i;
                while (i < input.Length && input[i] != '=' && input[i] != ' ') i++;
                string key = input[keyStart..i].Trim();
                if (string.IsNullOrEmpty(key)) { i++; continue; }

                if (i >= input.Length || input[i] != '=') { result[key] = ""; continue; }
                i++; // skip '='

                string value;
                if (i < input.Length && input[i] == '"')
                {
                    i++;
                    int vs = i;
                    while (i < input.Length && input[i] != '"') i++;
                    value = input[vs..i];
                    if (i < input.Length) i++;
                }
                else
                {
                    int vs = i;
                    while (i < input.Length && input[i] != ' ') i++;
                    value = input[vs..i];
                }

                result[key] = value;
            }
            return result;
        }

        public void Dispose() => Stop();
    }

    internal static class DictExtensions
    {
        /// Returns the value for the first key that exists in the dictionary.
        public static string? Get(this Dictionary<string, string> d, params string[] keys)
        {
            foreach (var k in keys)
                if (d.TryGetValue(k, out var v) && !string.IsNullOrWhiteSpace(v)) return v;
            return null;
        }
    }
}
