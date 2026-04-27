using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
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
    private static readonly HttpClient _http = new();
    private static readonly Regex _apexBlockRe = new(
        @"```apex\s+(GET|POST|PUT|DELETE|PATCH)\s+(/\S+)\s*(.*?)```",
        RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase);

    private readonly IAiProviderFactory _factory;
    private readonly object             _lock    = new();
    private          IChatSession?      _session;
    private          AiLibrarySettings  _settings;
    private          int?               _serverPort;
    private          string?            _serverApiKey;

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
    /// Tells the chat service that a local ApexComputerUse HTTP server is running so the AI
    /// can call it as a tool. Resets the active session to rebuild the system prompt.
    /// </summary>
    public void SetLocalServer(int port, string? apiKey)
    {
        lock (_lock) { _serverPort = port; _serverApiKey = apiKey; _session = null; }
    }

    /// <summary>Removes local server context (e.g. when the HTTP server is stopped).</summary>
    public void ClearLocalServer()
    {
        lock (_lock) { _serverPort = null; _serverApiKey = null; _session = null; }
    }

    /// <summary>
    /// Sends <paramref name="message"/> to the configured AI provider, calling
    /// <paramref name="onToken"/> for each streamed token and
    /// <paramref name="onComplete"/> with a metrics summary when the response finishes.
    /// When a local server is configured the AI may issue ApexComputerUse API calls
    /// (apex code blocks); this method runs an agentic loop executing those calls and
    /// feeding results back until the AI produces a plain answer.
    /// </summary>
    public async Task SendAsync(
        string           message,
        Action<string>   onToken,
        Action<string>   onComplete,
        Action<string>   onError,
        CancellationToken ct = default)
    {
        IChatSession session;
        int? serverPort;
        lock (_lock)
        {
            _session ??= CreateSession();
            session    = _session;
            serverPort = _serverPort;
        }

        // ── Agentic tool loop (only when a local server is available) ─────────
        if (serverPort.HasValue)
        {
            const int maxIter = 8;
            string current = message;

            for (int iter = 0; iter < maxIter; iter++)
            {
                AiMessagingCore.Models.ChatMessage response;
                try { response = await session.SendAsync(current, cancellationToken: ct); }
                catch (Exception ex) { onError(ex.Message); return; }

                var calls = ParseApexCalls(response.Content);
                if (calls.Count == 0 || iter == maxIter - 1)
                {
                    onToken(response.Content);
                    onComplete($"{session.ProviderName}/{session.Model}");
                    return;
                }

                var sb = new StringBuilder("Here are the results of your API calls:\n\n");
                foreach (var (method, path, body) in calls)
                {
                    try
                    {
                        var result = await ExecuteApexCallAsync(method, path, body, serverPort.Value, ct);
                        sb.AppendLine($"[{method} {path}]:\n{result}\n");
                    }
                    catch (Exception ex) { sb.AppendLine($"[{method} {path}] ERROR: {ex.Message}\n"); }
                }
                current = sb.ToString();
            }
            return;
        }

        // ── Standard streaming path ───────────────────────────────────────────
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

    internal static List<(string Method, string Path, string? Body)> ParseApexCalls(string text)
    {
        var results = new List<(string, string, string?)>();
        foreach (Match m in _apexBlockRe.Matches(text))
        {
            string method = m.Groups[1].Value.ToUpperInvariant();
            string path   = m.Groups[2].Value;
            string body   = m.Groups[3].Value.Trim();
            results.Add((method, path, body.Length > 0 ? body : null));
        }
        return results;
    }

    private async Task<string> ExecuteApexCallAsync(
        string method, string path, string? body, int port, CancellationToken ct)
    {
        var url = $"http://localhost:{port}{path}";
        using var req = new HttpRequestMessage(new HttpMethod(method), url);
        if (!string.IsNullOrWhiteSpace(_serverApiKey))
            req.Headers.TryAddWithoutValidation("X-Api-Key", _serverApiKey);
        if (body != null && method is "POST" or "PUT" or "PATCH")
            req.Content = new StringContent(body, Encoding.UTF8, "application/json");
        using var resp = await _http.SendAsync(req, ct);
        return await resp.Content.ReadAsStringAsync(ct);
    }

    private IChatSession CreateSession()
    {
        var sysMsg = SystemPrompt;
        if (_serverPort.HasValue)
        {
            var apex = BuildApexSystemPrompt(_serverPort.Value);
            sysMsg = string.IsNullOrWhiteSpace(sysMsg) ? apex : $"{sysMsg}\n\n{apex}";
        }

        var builder = new AiSessionBuilder(_factory, CurrentProvider)
            .WithModel(CurrentModel)
            .WithStreaming();
        if (!string.IsNullOrWhiteSpace(sysMsg))
            builder = builder.WithSystemMessage(sysMsg);
        return builder.Build();
    }

    internal static string BuildApexSystemPrompt(int port) => $$"""
        You have access to the ApexComputerUse Windows automation API running at http://localhost:{{port}}.

        To call an endpoint, output a markdown code block tagged `apex`:

        ```apex
        GET /windows
        ```

        For POST requests include the JSON body on the next line:

        ```apex
        POST /find
        {"window":"Notepad","type":"Edit","name":"Text Editor"}
        ```

        You will receive the JSON result and may make follow-up calls. When you have enough information, give your final answer without any apex blocks.

        Key endpoints:
        - GET /windows              — list all open windows
        - GET /status               — currently selected element
        - GET /elements             — element tree (add ?onscreen=true to filter visible)
        - POST /find                — select element (fields: window, name, id, type)
        - POST /exec                — act on element (field: action = gettext|click|type|keys|highlight|describe, value)
        - POST /capture             — screenshot (field: action = "screen" for fullscreen)
        - GET /sysinfo              — OS / hardware info
        - GET /scenes               — list saved scenes
        - POST /run                 — execute shell command (field: command; only if enabled)
        """;
}
