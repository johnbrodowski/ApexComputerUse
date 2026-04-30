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
        private readonly CommandDispatcher _dispatcher;
        private readonly HashSet<long>?    _allowedChatIds;
        private CancellationTokenSource?   _cts;

        public bool IsRunning { get; private set; }
        public event Action<string>? OnLog;

        /// <summary>
        /// Creates the Telegram bot controller.
        /// When <paramref name="allowedChatIds"/> is non-empty, only messages from those
        /// chat IDs are processed; all others receive an "Unauthorized." reply and are logged.
        /// Pass null or an empty collection to disable the whitelist (dev/local use only).
        /// </summary>
        public TelegramController(string token, CommandProcessor processor,
                                  IReadOnlyCollection<long>? allowedChatIds = null)
        {
            _bot            = new TelegramBotClient(token);
            _dispatcher     = new CommandDispatcher(processor);
            _allowedChatIds = allowedChatIds?.Count > 0
                                ? new HashSet<long>(allowedChatIds)
                                : null;
        }

        // -- Lifecycle -----------------------------------------------------

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

        // -- Update handler ------------------------------------------------

        private async Task HandleUpdateAsync(ITelegramBotClient bot, Update update, CancellationToken ct)
        {
            if (update.Message?.Text is not { } text) return;

            long   chatId   = update.Message.Chat.Id;
            string username = update.Message.From?.Username ?? "unknown";
            OnLog?.Invoke($"[Telegram] @{username} (chat {chatId}): {text}");

            // Authorization: reject messages from unlisted chat IDs when a whitelist is set.
            if (_allowedChatIds != null && !_allowedChatIds.Contains(chatId))
            {
                OnLog?.Invoke($"[Telegram] Rejected unauthorized chat {chatId} (@{username})");
                await bot.SendMessage(chatId, "Unauthorized.", cancellationToken: ct);
                return;
            }

            var req = ParseCommand(text);
            if (req == null)
            {
                await bot.SendMessage(chatId, "Send a command starting with /  (try /help)",
                    cancellationToken: ct);
                return;
            }

            var response = _dispatcher.Dispatch(req);

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
                catch (Exception ex) { OnLog?.Invoke($"[Telegram] Failed to send photo, falling back to text: {ex.Message}"); }
            }

            string reply = response.ToText();

            // Telegram caps messages at 4096 characters - split into numbered pages if needed.
            const int MaxLen = 4000;
            if (reply.Length <= MaxLen)
            {
                await bot.SendMessage(chatId, reply, cancellationToken: ct);
            }
            else
            {
                int pageCount = (reply.Length + MaxLen - 1) / MaxLen;
                for (int page = 0; page < pageCount; page++)
                {
                    int  start  = page * MaxLen;
                    int  length = Math.Min(MaxLen, reply.Length - start);
                    string part = $"[{page + 1}/{pageCount}]\n" + reply.Substring(start, length);
                    await bot.SendMessage(chatId, part, cancellationToken: ct);
                }
            }
        }

        private Task HandleErrorAsync(ITelegramBotClient bot, Exception ex,
            HandleErrorSource source, CancellationToken ct)
        {
            OnLog?.Invoke($"[Telegram Error] {source}: {ex.Message}");
            return Task.CompletedTask;
        }

        // -- Command parser ------------------------------------------------

        internal static CommandRequest? ParseCommand(string text)
        {
            if (!text.StartsWith('/')) return null;

            // Strip leading / and optional @botname suffix
            var parts  = text.TrimStart('/').Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            string cmd = parts[0].Split('@')[0].ToLowerInvariant();
            string args = parts.Length > 1 ? parts[1] : "";

            // Telegram falls back to passthrough on unknown verbs so the processor can emit
            // "Unknown command 'foo'. Try 'help'." rather than silently swallowing the message.
            return CommandLineParser.Build(cmd, args)
                   ?? new CommandRequest { Command = cmd, Action = cmd, Value = args };
        }

        public void Dispose() => Stop();
    }
}

