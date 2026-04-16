using System.Diagnostics;
using AiMessagingCore.Abstractions;
using AiMessagingCore.Configuration;
using AiMessagingCore.Events;
using AiMessagingCore.Models;

namespace AiMessagingCore.Core;

/// <summary>
/// Base implementation for provider-specific chat sessions.
/// Handles message history, streaming orchestration, metrics, and event dispatch.
/// Provider subclasses implement only <see cref="ExecuteStreamAsync"/>.
/// </summary>
public abstract class ChatSessionBase : IChatSession
{
    private readonly List<ChatMessage> _messages = [];
    private readonly ChatSessionOptions _options;

    protected ChatSessionBase(ChatSessionOptions options)
    {
        _options = options;
        ProviderName = options.ProviderName;
        Model = options.Model;
        SessionId = Guid.NewGuid().ToString("N");

        if (!string.IsNullOrWhiteSpace(options.SystemMessage))
            _messages.Add(new ChatMessage(ChatRole.System, options.SystemMessage, DateTimeOffset.UtcNow));
    }

    // ── IChatSession identity ───────────────────────────────────────────────

    public string SessionId { get; }

    public string ProviderName { get; }

    public string Model { get; protected set; }

    public IReadOnlyList<ChatMessage> Messages => _messages;

    // ── Events ──────────────────────────────────────────────────────────────

    public event EventHandler<ResponseStartedEventArgs>?   OnResponseStarted;
    public event EventHandler<TokenReceivedEventArgs>?     OnTokenReceived;
    public event EventHandler<ResponseCompletedEventArgs>? OnResponseCompleted;
    public event EventHandler<AiErrorEventArgs>?             OnError;
    public event EventHandler?                             OnCancelled;

    // ── Operations ──────────────────────────────────────────────────────────

    public async ValueTask<ChatMessage> SendAsync(
        string userMessage,
        RequestOverrides? overrides = null,
        CancellationToken cancellationToken = default)
    {
        await foreach (var chunk in StreamAsync(userMessage, overrides, cancellationToken))
        {
            if (chunk.Metadata?.ContainsKey("final") == true)
                return chunk;
        }

        throw new InvalidOperationException("No final response was produced by the provider session.");
    }

    public async IAsyncEnumerable<ChatMessage> StreamAsync(
        string userMessage,
        RequestOverrides? overrides = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var user = new ChatMessage(ChatRole.User, userMessage, DateTimeOffset.UtcNow);
        _messages.Add(user);

        var sw = Stopwatch.StartNew();
        var firstTokenAt = TimeSpan.Zero;
        var completionTokens = 0;
        var full = string.Empty;

        OnResponseStarted?.Invoke(this, new ResponseStartedEventArgs(SessionId, ProviderName, Model));

        Exception? streamError = null;
        var merged = MergeWithSessionDefaults(overrides);

        await using var enumerator = ExecuteStreamAsync(_messages, merged, cancellationToken)
            .GetAsyncEnumerator(cancellationToken);

        while (true)
        {
            ChatMessage chunk;
            try
            {
                if (!await enumerator.MoveNextAsync())
                    break;

                chunk = enumerator.Current;
            }
            catch (Exception ex)
            {
                streamError = ex;
                break;
            }

            if (firstTokenAt == TimeSpan.Zero)
                firstTokenAt = sw.Elapsed;

            completionTokens += Math.Max(1, chunk.Content.Length / 4);
            full += chunk.Content;

            OnTokenReceived?.Invoke(this, new TokenReceivedEventArgs(chunk.Content, chunk));

            yield return chunk;
        }

        if (streamError is not null)
        {
            if (streamError is OperationCanceledException)
                OnCancelled?.Invoke(this, EventArgs.Empty);
            else
                OnError?.Invoke(this, new AiErrorEventArgs(streamError));

            throw streamError;
        }

        var final = new ChatMessage(
            ChatRole.Assistant,
            full,
            DateTimeOffset.UtcNow,
            new Dictionary<string, string> { ["final"] = "true" },
            new TokenUsage(Math.Max(1, userMessage.Length / 4), completionTokens),
            Model);

        _messages.Add(final);

        var metrics = BuildMetrics(final.TokenUsage!, sw.Elapsed, firstTokenAt);
        OnResponseCompleted?.Invoke(this, new ResponseCompletedEventArgs(metrics));

        yield return final;
    }

    public virtual ValueTask SwitchModelAsync(string model, CancellationToken cancellationToken = default)
    {
        Model = model;
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Provider switching is implemented by <see cref="AiSession"/> which wraps this base.
    /// Direct subclasses throw by default.
    /// </summary>
    public virtual ValueTask SwitchProviderAsync(string providerName, CancellationToken cancellationToken = default)
        => throw new NotSupportedException(
            "SwitchProviderAsync requires a session created via AiSessionBuilder.Build().");

    // ── Helpers ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Seeds the session history from an existing message list.
    /// Used internally when provider-switching to preserve context.
    /// </summary>
    internal void SeedHistory(IEnumerable<ChatMessage> history)
    {
        _messages.Clear();
        _messages.AddRange(history);
    }

    private RequestOverrides MergeWithSessionDefaults(RequestOverrides? overrides) => new()
    {
        Model            = overrides?.Model,
        Temperature      = overrides?.Temperature      ?? _options.Temperature,
        MaxTokens        = overrides?.MaxTokens        ?? _options.MaxTokens,
        TopP             = overrides?.TopP             ?? _options.TopP,
        StreamingEnabled = overrides?.StreamingEnabled ?? _options.StreamingEnabled,
        Timeout          = overrides?.Timeout          ?? _options.Timeout,
        EnableReasoning  = overrides?.EnableReasoning  ?? _options.EnableReasoning
    };

    private AiUsageMetrics BuildMetrics(TokenUsage usage, TimeSpan total, TimeSpan firstToken)
    {
        var tps = total.TotalSeconds <= 0 ? 0 : usage.CompletionTokens / total.TotalSeconds;
        return new AiUsageMetrics(ProviderName, Model, usage.PromptTokens, usage.CompletionTokens,
            usage.TotalTokens, tps, firstToken, total);
    }

    /// <summary>
    /// Provider-specific streaming implementation. Yield one <see cref="ChatMessage"/>
    /// per token/chunk. The base class assembles, stores, and raises events.
    /// </summary>
    protected abstract IAsyncEnumerable<ChatMessage> ExecuteStreamAsync(
        IReadOnlyList<ChatMessage> messages,
        RequestOverrides? overrides,
        CancellationToken cancellationToken);
}
