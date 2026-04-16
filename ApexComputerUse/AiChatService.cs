using AiMessagingCore.Abstractions;
using AiMessagingCore.Configuration;
using AiMessagingCore.Core;
using AiMessagingCore.Events;
using AiMessagingCore.Providers.Anthropic;
using AiMessagingCore.Providers.DeepSeek;
using AiMessagingCore.Providers.Duck;
using AiMessagingCore.Providers.Grok;
using AiMessagingCore.Providers.Groq;
using AiMessagingCore.Providers.Local;
using AiMessagingCore.Providers.OpenAI;

namespace ApexComputerUse;

/// <summary>
/// Manages an AI chat session backed by AiMessagingCore.
/// Holds provider settings, persists them to ai-settings.json, and
/// exposes a streaming SendAsync used by both the browser /chat endpoint
/// and any future UI consumer.
/// </summary>
public sealed class AiChatService
{
    private readonly IAiProviderFactory _factory;
    private readonly object             _lock    = new();
    private          IChatSession?      _session;
    private          AiLibrarySettings  _settings;

    public IReadOnlyList<string> RegisteredProviders { get; }
    public string  CurrentProvider { get; private set; } = "OpenAI";
    public string  CurrentModel    { get; private set; } = "";
    public string  SystemPrompt    { get; private set; } = "";
    public bool    SessionActive   => _session != null;

    public AiChatService()
    {
        var localModels = new InMemoryLocalModelManager();
        IAiProvider[] providers =
        [
            new OpenAiProvider(),
            new AnthropicProvider(),
            new DeepSeekProvider(),
            new GrokProvider(),
            new GroqProvider(),
            new DuckProvider(),
            new LmStudioProvider(localModels),
            new LlamaSharpProvider(localModels)
        ];
        _factory           = new AiProviderFactory(providers);
        RegisteredProviders = [.. _factory.RegisteredProviders.OrderBy(x => x)];

        _settings = AiSettings.Load();
        AiSettings.ApplyToEnvironment(_settings);

        CurrentProvider = _settings.DefaultProvider;
        if (_settings.Providers.TryGetValue(CurrentProvider, out var ps))
        {
            CurrentModel = ps.Defaults.Model;
            SystemPrompt = ps.Defaults.SystemPrompt;
        }
    }

    /// <summary>Returns the saved defaults for a provider (model, system prompt, sample query).</summary>
    public ModelDefaults? GetProviderDefaults(string provider) =>
        _settings.Providers.TryGetValue(provider, out var ps) ? ps.Defaults : null;

    /// <summary>Returns the saved API key for a provider (may contain placeholder text).</summary>
    public string GetApiKey(string provider) =>
        _settings.Providers.TryGetValue(provider, out var ps) ? ps.ApiKey ?? "" : "";

    /// <summary>
    /// Persists new settings to ai-settings.json, applies them to environment variables,
    /// and resets the active session so the next send creates a fresh one.
    /// </summary>
    public void ApplySettings(string provider, string model, string systemPrompt, string apiKey)
    {
        lock (_lock)
        {
            CurrentProvider = provider;
            CurrentModel    = model;
            SystemPrompt    = systemPrompt;

            if (_settings.Providers.TryGetValue(provider, out var ps))
            {
                ps.Defaults.Model        = model;
                ps.Defaults.SystemPrompt = systemPrompt;
                // Only overwrite the key if the user typed a real value (not a placeholder).
                if (!string.IsNullOrWhiteSpace(apiKey) &&
                    !apiKey.Contains("YOUR_", StringComparison.OrdinalIgnoreCase))
                    ps.ApiKey = apiKey;
            }
            _settings.DefaultProvider = provider;
            AiSettings.Save(_settings);
            AiSettings.ApplyToEnvironment(_settings);
            _session = null;   // stale session — recreate on next send
        }
    }

    /// <summary>Clears the conversation history so the next send starts fresh.</summary>
    public void ResetSession()
    {
        lock (_lock) { _session = null; }
    }

    /// <summary>
    /// Sends <paramref name="message"/> to the configured AI provider, calling
    /// <paramref name="onToken"/> for each streamed token and
    /// <paramref name="onComplete"/> with a metrics summary when the response finishes.
    /// </summary>
    public async Task SendAsync(
        string           message,
        Action<string>   onToken,
        Action<string>   onComplete,
        Action<string>   onError,
        CancellationToken ct = default)
    {
        IChatSession session;
        lock (_lock)
        {
            _session ??= CreateSession();
            session   = _session;
        }

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        void HandleToken(object? _, TokenReceivedEventArgs e)
        {
            try { onToken(e.Token); } catch { }
        }
        void HandleCompleted(object? _, ResponseCompletedEventArgs e)
        {
            try { onComplete($"{e.ProviderName}/{e.ModelName} · {e.TotalTokens} tokens · {e.TokensPerSecond:F1} t/s"); }
            catch { }
            tcs.TrySetResult();
        }
        void HandleError(object? _, AiErrorEventArgs e)
        {
            try { onError(e.Message); } catch { }
            tcs.TrySetResult();
        }
        void HandleCancelled(object? _, EventArgs e) => tcs.TrySetResult();

        session.OnTokenReceived     += HandleToken;
        session.OnResponseCompleted += HandleCompleted;
        session.OnError             += HandleError;
        session.OnCancelled         += HandleCancelled;
        try
        {
            await session.SendAsync(message, cancellationToken: ct);
            await tcs.Task;
        }
        finally
        {
            session.OnTokenReceived     -= HandleToken;
            session.OnResponseCompleted -= HandleCompleted;
            session.OnError             -= HandleError;
            session.OnCancelled         -= HandleCancelled;
        }
    }

    private IChatSession CreateSession()
    {
        var builder = new AiSessionBuilder(_factory, CurrentProvider)
            .WithModel(CurrentModel)
            .WithStreaming();
        if (!string.IsNullOrWhiteSpace(SystemPrompt))
            builder = builder.WithSystemMessage(SystemPrompt);
        return builder.Build();
    }
}
