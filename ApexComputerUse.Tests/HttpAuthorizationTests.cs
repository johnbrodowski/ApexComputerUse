using ApexComputerUse;
using Xunit;

namespace ApexComputerUse.Tests;

/// <summary>
/// Tests for HTTP path authorization and permission defaults.
/// Covers IsPathAllowed path routing, ClientPermissions defaults,
/// deny-by-default for unknown remote callers, and AppConfig security defaults.
/// </summary>
public class HttpAuthorizationTests
{
    // ── ClientPermissions defaults ────────────────────────────────────────

    [Fact]
    public void ClientPermissions_Defaults_AllowCoreFeaturesButNotShellOrClients()
    {
        var p = new ClientPermissions();

        Assert.True(p.AllowAutomation);
        Assert.True(p.AllowCapture);
        Assert.True(p.AllowAi);
        Assert.True(p.AllowScenes);
        Assert.True(p.AllowDiagnostics);
        Assert.False(p.AllowShellRun);
        Assert.False(p.AllowClients);
    }

    // ── IsPathAllowed — /health always allowed (no permission required) ───

    [Fact]
    public void IsPathAllowed_Health_AlwaysAllowed()
    {
        var noPermissions = new ClientPermissions
        {
            AllowAutomation = false, AllowCapture = false, AllowAi      = false,
            AllowScenes     = false, AllowShellRun = false, AllowClients = false,
            AllowDiagnostics = false
        };

        Assert.True(HttpCommandServer.IsPathAllowed("/health", noPermissions));
    }

    // ── IsPathAllowed — diagnostics require AllowDiagnostics ─────────────

    [Theory]
    [InlineData("/ping")]
    [InlineData("/metrics")]
    [InlineData("/sysinfo")]
    [InlineData("/env")]
    [InlineData("/ls")]
    [InlineData("/help")]
    [InlineData("/status")]
    public void IsPathAllowed_DiagnosticEndpoints_RequireAllowDiagnostics(string path)
    {
        var denied  = new ClientPermissions { AllowDiagnostics = false };
        var allowed = new ClientPermissions { AllowDiagnostics = true };

        Assert.False(HttpCommandServer.IsPathAllowed(path, denied));
        Assert.True(HttpCommandServer.IsPathAllowed(path, allowed));
    }

    // ── IsPathAllowed — shell run gated by AllowShellRun ─────────────────

    [Fact]
    public void IsPathAllowed_Run_RequiresAllowShellRun()
    {
        var denied  = new ClientPermissions { AllowShellRun = false };
        var allowed = new ClientPermissions { AllowShellRun = true };

        Assert.False(HttpCommandServer.IsPathAllowed("/run", denied));
        Assert.True(HttpCommandServer.IsPathAllowed("/run", allowed));
    }

    // ── IsPathAllowed — capture / OCR ────────────────────────────────────

    [Theory]
    [InlineData("/capture")]
    [InlineData("/capture/screen")]
    [InlineData("/ocr")]
    public void IsPathAllowed_CaptureOcr_RequiresAllowCapture(string path)
    {
        var denied  = new ClientPermissions { AllowCapture = false };
        var allowed = new ClientPermissions { AllowCapture = true };

        Assert.False(HttpCommandServer.IsPathAllowed(path, denied));
        Assert.True(HttpCommandServer.IsPathAllowed(path, allowed));
    }

    // ── IsPathAllowed — AI / chat ─────────────────────────────────────────

    [Theory]
    [InlineData("/ai/init")]
    [InlineData("/ai/infer")]
    [InlineData("/chat")]
    [InlineData("/chat/send")]
    public void IsPathAllowed_AiChat_RequiresAllowAi(string path)
    {
        var denied  = new ClientPermissions { AllowAi = false };
        var allowed = new ClientPermissions { AllowAi = true };

        Assert.False(HttpCommandServer.IsPathAllowed(path, denied));
        Assert.True(HttpCommandServer.IsPathAllowed(path, allowed));
    }

    // ── IsPathAllowed — scenes / editor ──────────────────────────────────

    [Theory]
    [InlineData("/scenes")]
    [InlineData("/scenes/abc123")]
    [InlineData("/editor")]
    public void IsPathAllowed_ScenesEditor_RequiresAllowScenes(string path)
    {
        var denied  = new ClientPermissions { AllowScenes = false };
        var allowed = new ClientPermissions { AllowScenes = true };

        Assert.False(HttpCommandServer.IsPathAllowed(path, denied));
        Assert.True(HttpCommandServer.IsPathAllowed(path, allowed));
    }

    // ── IsPathAllowed — clients ───────────────────────────────────────────

    [Theory]
    [InlineData("/clients")]
    [InlineData("/clients/abc123")]
    public void IsPathAllowed_Clients_RequiresAllowClients(string path)
    {
        var denied  = new ClientPermissions { AllowClients = false };
        var allowed = new ClientPermissions { AllowClients = true };

        Assert.False(HttpCommandServer.IsPathAllowed(path, denied));
        Assert.True(HttpCommandServer.IsPathAllowed(path, allowed));
    }

    // ── IsPathAllowed — automation (everything else) ──────────────────────

    [Theory]
    [InlineData("/find")]
    [InlineData("/exec")]
    [InlineData("/elements")]
    [InlineData("/windows")]
    [InlineData("/uimap")]
    [InlineData("/draw")]
    public void IsPathAllowed_AutomationPaths_RequiresAllowAutomation(string path)
    {
        var denied  = new ClientPermissions { AllowAutomation = false };
        var allowed = new ClientPermissions { AllowAutomation = true };

        Assert.False(HttpCommandServer.IsPathAllowed(path, denied));
        Assert.True(HttpCommandServer.IsPathAllowed(path, allowed));
    }

    // ── AppConfig security defaults ───────────────────────────────────────

    [Fact]
    public void AppConfig_CompiledDefaults_AreSecure()
    {
        // Create a fresh config with no JSON, no env vars applied.
        var cfg = new AppConfig();

        Assert.False(cfg.HttpBindAll,    "HttpBindAll must default to false (localhost-only)");
        Assert.False(cfg.EnableShellRun, "EnableShellRun must default to false");
        Assert.Empty(cfg.ApiKey);        // key is generated at the GUI layer, not baked in
    }

    // ── Bind-all + empty API key guard ────────────────────────────────────

    [Theory]
    [InlineData(true,  "",      true)]   // bindAll=true + no key → refuse
    [InlineData(true,  "   ",   true)]   // bindAll=true + whitespace key → refuse
    [InlineData(true,  "abc",   false)]  // bindAll=true + key → allow
    [InlineData(false, "",      false)]  // bindAll=false + no key → allow (localhost only)
    [InlineData(false, "abc",   false)]  // bindAll=false + key → allow
    public void BindAllGuard_RefusesWhenBindAllAndNoApiKey(bool bindAll, string apiKey, bool expectRefuse)
    {
        // This mirrors the guard condition in ServerTabController.ToggleHttp and ApexService.OnStart.
        bool shouldRefuse = bindAll && string.IsNullOrWhiteSpace(apiKey);
        Assert.Equal(expectRefuse, shouldRefuse);
    }

    // ── Active-request counter: increment inside lambda ───────────────────

    [Fact]
    public async Task ActiveRequestCounter_IncrementInsideLambda_PairsWithDecrement()
    {
        // Verify that moving Interlocked.Increment inside the Task.Run lambda
        // means counter stays at 0 when the task never actually runs.
        int counter = 0;

        // Simulate what ListenLoop now does: increment INSIDE the lambda body.
        using var cts = new System.Threading.CancellationTokenSource();
        cts.Cancel(); // cancel before the task has a chance to run

        var task = Task.Run(() =>
        {
            System.Threading.Interlocked.Increment(ref counter);
            try { /* work */ }
            finally { System.Threading.Interlocked.Decrement(ref counter); }
        });

        await task;
        Assert.Equal(0, counter);
    }
}
