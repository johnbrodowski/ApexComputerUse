using AiMessagingCore.Configuration;
using AiMessagingCore.Events;
using AiMessagingCore.Models;

namespace AiMessagingCore.Abstractions;

/// <summary>
/// Represents a stateful chat session that preserves provider-neutral context across turns.
/// </summary>
public interface IChatSession
{
    string SessionId { get; }

    string ProviderName { get; }

    string Model { get; }

    IReadOnlyList<ChatMessage> Messages { get; }

    // -- Events (standard EventHandler pattern) -----------------------------

    event EventHandler<ResponseStartedEventArgs>?   OnResponseStarted;

    event EventHandler<TokenReceivedEventArgs>?     OnTokenReceived;

    event EventHandler<ResponseCompletedEventArgs>? OnResponseCompleted;

    event EventHandler<AiErrorEventArgs>?             OnError;

    event EventHandler?                             OnCancelled;

    // -- Operations ----------------------------------------------------------

    /// <summary>Sends a user message and returns the final assembled assistant reply.</summary>
    ValueTask<ChatMessage> SendAsync(
        string userMessage,
        RequestOverrides? overrides = null,
        CancellationToken cancellationToken = default);

    /// <summary>Streams the response token-by-token as an async sequence.</summary>
    IAsyncEnumerable<ChatMessage> StreamAsync(
        string userMessage,
        RequestOverrides? overrides = null,
        CancellationToken cancellationToken = default);

    /// <summary>Switches the active model within the same session, preserving history.</summary>
    ValueTask SwitchModelAsync(string model, CancellationToken cancellationToken = default);

    /// <summary>Switches to a different provider while preserving message history.</summary>
    ValueTask SwitchProviderAsync(string providerName, CancellationToken cancellationToken = default);
}

