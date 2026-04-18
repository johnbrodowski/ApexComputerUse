namespace ApexUIBridge.TestRunner;

using System.Text.RegularExpressions;

/// <summary>
/// The full test suite. Treats the test apps as black-box 3rd-party applications —
/// scans their windows, discovers element IDs dynamically, then interacts and asserts.
/// </summary>
public sealed class TestSuite
{
    private readonly BridgeClient _client;
    private readonly int _actionDelayMs;
    private readonly int _uiSettleDelayMs;
    private readonly HashSet<string> _skipTests;
    private readonly string _webBaseUrl;
    private readonly string[] _webPagePaths;

    public TestSuite(
        BridgeClient client,
        HashSet<string>? skipTests = null,
        string? webBaseUrl = null,
        string[]? webPagePaths = null)
    {
        _client = client;
        _actionDelayMs = actionDelayMs;
        _uiSettleDelayMs = uiSettleDelayMs;
        _skipTests = skipTests ?? new HashSet<string>();
        _webBaseUrl = webBaseUrl ?? "";
        _webPagePaths = webPagePaths ?? Array.Empty<string>();
    }

    public async Task<CycleResult> RunAsync(CancellationToken ct)
    {
        var results = new List<TestResult>();

        // ── Discovery ──────────────────────────────────────────────────────────
        await Test(results, "LIST_WINDOWS — WinForms app visible",
            "LIST_WINDOWS",
            r => r.Success && (r.Data?.Contains("ApexUIBridge Test Application - WinForms") ?? false),
            ct);

        await Test(results, "LIST_WINDOWS — WPF app visible",
            "LIST_WINDOWS",
            r => r.Success && (r.Data?.Contains("ApexUIBridge Test Application - WPF") ?? false),
            ct);

        if (!string.IsNullOrWhiteSpace(_webBaseUrl))
        {
            var webPages = _webPagePaths.Length > 0 ? _webPagePaths : ["/"];
            foreach (var page in webPages)
            {
                var webTarget = BuildWebTarget(_webBaseUrl, page);
                await Test(results, $"SCAN_WINDOW — web page visible ({webTarget})",
                    $"SCAN_WINDOW {webTarget}",
                    r => r.Success && !string.IsNullOrWhiteSpace(r.Data),
                    ct);
            }
        }

        // ── WinForms window ────────────────────────────────────────────────────
        var wfScan = await _client.SendAsync("SCAN_WINDOW ApexUIBridge Test Application - WinForms", ct);
        results.Add(Result("SCAN_WINDOW WinForms", wfScan.Success, wfScan.Message));

        if (wfScan.Success && wfScan.Data is { } wfData)
        {
            // TextBox — UIA Name is empty for Edit controls; look for the AutomationId in brackets
            var tbId = FindId(wfData, "[TextBox]");
            if (tbId.HasValue)
            {
                await Test(results, "GET_TEXT WinForms TextBox → 'Test TextBox'",
                    $"GET_TEXT {tbId}",
                    r => r.Success && (r.Data?.Contains("Test TextBox") ?? false),
                    ct);

                var typed = $"autotest_{DateTime.Now:HHmmss}";
                await Test(results, "TYPE into WinForms TextBox",
                    $"TYPE {tbId} {typed}",
                    r => r.Success,
                    ct);

                await Test(results, "GET_TEXT WinForms TextBox → typed value",
                    $"GET_TEXT {tbId}",
                    r => r.Success && (r.Data?.Contains(typed) ?? false),
                    ct);
            }
            else results.Add(Result("WinForms TextBox — element found in scan", false, "ID not found"));

            // CheckBox
            var cbId = FindId(wfData, "Test Checkbox");
            if (cbId.HasValue)
            {
                await Test(results, "TOGGLE WinForms SimpleCheckBox",
                    $"TOGGLE {cbId}",
                    r => r.Success,
                    ct);
                // Toggle back so the UI stays in known state
                await Test(results, "TOGGLE WinForms SimpleCheckBox (restore)",
                    $"TOGGLE {cbId}",
                    r => r.Success,
                    ct);
            }
            else results.Add(Result("WinForms CheckBox — element found in scan", false, "ID not found"));

            // Button (ContextMenu button)
            var btnId = FindId(wfData, "ContextMenu");
            if (btnId.HasValue)
            {
                await Test(results, "CLICK WinForms ContextMenu button",
                    $"CLICK {btnId}",
                    r => r.Success,
                    ct);
            }
            else results.Add(Result("WinForms Button — element found in scan", false, "ID not found"));

            // ProgressBar value read
            var pbId = FindIdByType(wfData, "ProgressBar");
            if (pbId.HasValue)
            {
                await Test(results, "GET_TEXT WinForms ProgressBar (value readable)",
                    $"GET_TEXT {pbId}",
                    r => r.Success,
                    ct);
            }

            // Editable ComboBox
            var comboId = FindId(wfData, "EditableCombo");
            if (comboId.HasValue)
            {
                await Test(results, "EXPAND WinForms EditableCombo",
                    $"EXPAND {comboId}",
                    r => r.Success,
                    ct);
                await Test(results, "COLLAPSE WinForms EditableCombo",
                    $"COLLAPSE {comboId}",
                    r => r.Success,
                    ct);
            }
        }

        // ── WPF window ─────────────────────────────────────────────────────────
        var wpfScan = await _client.SendAsync("SCAN_WINDOW ApexUIBridge Test Application - WPF", ct);
        results.Add(Result("SCAN_WINDOW WPF", wpfScan.Success, wpfScan.Message));

        if (wpfScan.Success && wpfScan.Data is { } wpfData)
        {
            // TextBox — UIA Name is empty for Edit controls; look for the AutomationId in brackets
            var wpfTbId = FindId(wpfData, "[TextBox]");
            if (wpfTbId.HasValue)
            {
                await Test(results, "GET_TEXT WPF TextBox → 'Test TextBox'",
                    $"GET_TEXT {wpfTbId}",
                    r => r.Success && (r.Data?.Contains("Test TextBox") ?? false),
                    ct);

                await Test(results, "TYPE into WPF TextBox",
                    $"TYPE {wpfTbId} wpf_auto_{DateTime.Now:HHmmss}",
                    r => r.Success,
                    ct);
            }
            else results.Add(Result("WPF TextBox — element found in scan", false, "ID not found"));

            // CheckBox
            var wpfCbId = FindId(wpfData, "Test Checkbox");
            if (wpfCbId.HasValue)
            {
                await Test(results, "TOGGLE WPF SimpleCheckBox",
                    $"TOGGLE {wpfCbId}",
                    r => r.Success,
                    ct);
                await Test(results, "TOGGLE WPF SimpleCheckBox (restore)",
                    $"TOGGLE {wpfCbId}",
                    r => r.Success,
                    ct);
            }

            // InvokableButton (has a Command binding — good ICommand test)
            var invId = FindId(wpfData, "[InvokableButton]");
            if (invId.HasValue)
            {
                await Test(results, "CLICK WPF InvokableButton",
                    $"CLICK {invId}",
                    r => r.Success,
                    ct);
                // Give WPF time to process the ICommand and push the binding update through UIA
                await Task.Delay(_uiSettleDelayMs, ct);
                // Re-scan so the element registry reflects the new Content="Invoked!" Name value
                var afterClickScan = await _client.SendAsync("SCAN_WINDOW ApexUIBridge Test Application - WPF", ct);
                results.Add(Result("WPF InvokableButton → 'Invoked!' (after re-scan)",
                    afterClickScan.Success && (afterClickScan.Data?.Contains("'Invoked!'") ?? false),
                    afterClickScan.Success ? "Button name did not change to 'Invoked!'" : afterClickScan.Message));
            }

            // Non-editable ComboBox
            var wpfComboId = FindId(wpfData, "NonEditable");
            if (wpfComboId.HasValue)
            {
                await Test(results, "EXPAND WPF NonEditableCombo",
                    $"EXPAND {wpfComboId}",
                    r => r.Success,
                    ct);
                await Test(results, "COLLAPSE WPF NonEditableCombo",
                    $"COLLAPSE {wpfComboId}",
                    r => r.Success,
                    ct);
            }

            // Slider
            var sliderWpfId = FindIdByType(wpfData, "Slider");
            if (sliderWpfId.HasValue)
            {
                await Test(results, "SET_VALUE WPF Slider to 7",
                    $"SET_VALUE {sliderWpfId} 7",
                    r => r.Success,
                    ct);
            }
        }

        // ── HELP command sanity check ──────────────────────────────────────────
        await Test(results, "HELP command returns content",
            "HELP",
            r => r.Success && !string.IsNullOrWhiteSpace(r.Data),
            ct);

        // ── WinForms Torture Test ──────────────────────────────────────────────
        await RunWinFormsTortureTests(results, ct);

        // ── WPF Torture Test ───────────────────────────────────────────────────
        await RunWpfTortureTests(results, ct);

        return new CycleResult(results);
    }

    // ── WinForms Torture Test ──────────────────────────────────────────────────

    private async Task RunWinFormsTortureTests(List<TestResult> results, CancellationToken ct)
    {
        const string p = "WF Torture";

        // Re-scan the main WinForms window to get the Tools menu element
        var mainScan = await _client.SendAsync("SCAN_WINDOW ApexUIBridge Test Application - WinForms", ct);
        if (!mainScan.Success || mainScan.Data == null)
        {
            results.Add(Result($"{p}: re-scan main window", false, mainScan.Message)); return;
        }

        // WinForms ToolStripDropDown opens as a SEPARATE floating HWND —
        // SCAN_WINDOW with window-filter can't see its children.
        // Use keyboard shortcut {ALT}tt instead:
        //   {ALT}t → Alt+T opens the "&Tools" menu
        //   t      → T activates "Open &Torture Test Form..." accelerator
        var toolsId = FindId(mainScan.Data, "[toolsToolStripMenuItem]");
        if (!toolsId.HasValue)
        {
            results.Add(Result($"{p}: find [toolsToolStripMenuItem]", false, "Not found")); return;
        }

        await Test(results, $"{p}: keyboard open TortureTestForm via Alt+T,T",
            $"SEND_KEYS {toolsId} {{ALT}}tt", r => r.Success, ct);
        await Task.Delay(_uiSettleDelayMs, ct);

        // Scan the torture test window (title contains "UI Torture Test")
        var scan = await _client.SendAsync("SCAN_WINDOW UI Torture Test", ct);
        results.Add(Result($"{p}: SCAN_WINDOW", scan.Success, scan.Message));
        if (!scan.Success || scan.Data == null) return;
        var td = scan.Data;
        // WinForms creates ALL tab-page controls at startup — no rescan needed per tab.

        // ── Identity tab — CheckBoxes ──────────────────────────────────────────
        foreach (var name in new[] { "AD Sync", "MFA Enabled", "VPN Access" })
            await Torture_Toggle(results, $"{p}: toggle '{name}'", td, $"'{name}'", ct);

        // ── Switch to Network tab ──────────────────────────────────────────────
        await Torture_ClickTab(results, $"{p}: switch → Network", td, "'Network'", ct);
        await Task.Delay(_actionDelayMs, ct);

        // Network tab — TLS CheckBoxes
        foreach (var name in new[] { "Verify Server Certificate", "Certificate Pinning" })
            await Torture_Toggle(results, $"{p}: toggle '{name}'", td, $"'{name}'", ct);

        // Connection-mode RadioButtons — WinForms RadioButton supports TogglePattern
        await Torture_Toggle(results, $"{p}: select 'Via Proxy' radio",  td, "'Via Proxy'", ct, restoreAfter: false);
        await Torture_Toggle(results, $"{p}: restore 'Direct' radio",    td, "'Direct'",    ct, restoreAfter: false);

        // ── Switch to Scheduler tab ────────────────────────────────────────────
        await Torture_ClickTab(results, $"{p}: switch → Scheduler", td, "'Scheduler'", ct);
        await Task.Delay(_actionDelayMs, ct);

        await Torture_Toggle(results, $"{p}: toggle 'Retry on Failure'", td, "'Retry on Failure'", ct);

        // ── Switch to Logs tab ─────────────────────────────────────────────────
        await Torture_ClickTab(results, $"{p}: switch → Logs", td, "'Logs'", ct);
        await Task.Delay(_actionDelayMs, ct);

        await Torture_Toggle(results, $"{p}: toggle 'Auto-scroll'", td, "'Auto-scroll'", ct);

        // ── Return to Identity tab ─────────────────────────────────────────────
        await Torture_ClickTab(results, $"{p}: switch → Identity (restore)", td, "'Identity'", ct);
    }

    // ── WPF Torture Test ───────────────────────────────────────────────────────

    private async Task RunWpfTortureTests(List<TestResult> results, CancellationToken ct)
    {
        const string p = "WPF Torture";

        // Open via Tools › Open Torture Test Window...
        var mainScan = await _client.SendAsync("SCAN_WINDOW ApexUIBridge Test Application - WPF", ct);
        if (!mainScan.Success || mainScan.Data == null)
        {
            results.Add(Result($"{p}: re-scan main window", false, mainScan.Message)); return;
        }

        // Click the Tools menu to expand it (no AutomationId — find by name "'Tools'")
        var toolsId = FindId(mainScan.Data, "'Tools'");
        if (toolsId.HasValue)
        {
            await _client.SendAsync($"CLICK {toolsId}", ct);
            await Task.Delay(_actionDelayMs, ct);
        }

        var expandedScan = await _client.SendAsync("SCAN_WINDOW ApexUIBridge Test Application - WPF", ct);
        var menuItemId = expandedScan.Success
            ? FindId(expandedScan.Data ?? "", "Open Torture Test Window")
            : null;

        if (!menuItemId.HasValue)
        {
            results.Add(Result($"{p}: find 'Open Torture Test Window' menu item", false, "Not found")); return;
        }

        await Test(results, $"{p}: click 'Open Torture Test Window...'",
            $"CLICK {menuItemId}", r => r.Success, ct);
        await Task.Delay(_uiSettleDelayMs, ct);

        // Initial scan — Identity tab is selected by default
        var scan = await _client.SendAsync("SCAN_WINDOW WPF Torture Test", ct);
        results.Add(Result($"{p}: SCAN_WINDOW", scan.Success, scan.Message));
        if (!scan.Success || scan.Data == null) return;
        var td = scan.Data;
        // `td` contains Identity tab content + all tab headers (stable IDs across rescans)

        // ── Identity tab ───────────────────────────────────────────────────────
        await Torture_Type(results, $"{p}: TYPE [Username]", td, "[Username]", "torture_user", ct);
        await Torture_Type(results, $"{p}: TYPE [Email]",    td, "[Email]",    "test@torture.local", ct);

        var accessId = FindId(td, "[AccessLevel]");
        if (accessId.HasValue)
            await Test(results, $"{p}: SET_VALUE [AccessLevel] = 4",
                $"SET_VALUE {accessId} 4", r => r.Success, ct);
        else
            results.Add(Result($"{p}: find [AccessLevel]", false, "Not found"));

        // WPF RadioButton does NOT support TogglePattern — use CLICK (InvokePattern)
        await Torture_ClickTab(results, $"{p}: CLICK [EmpContractor]",        td, "[EmpContractor]", ct);
        await Torture_ClickTab(results, $"{p}: CLICK [EmpFullTime] (restore)", td, "[EmpFullTime]",   ct);

        // ── Network tab — rescan after switching ───────────────────────────────
        // WPF TabControl only loads the SELECTED tab's content into the UIA tree;
        // a fresh SCAN_WINDOW is required after every tab switch to see new controls.
        await Torture_ClickTab(results, $"{p}: switch → [TabNetwork]", td, "[TabNetwork]", ct);
        await Task.Delay(_actionDelayMs, ct);
        var networkScan = await _client.SendAsync("SCAN_WINDOW WPF Torture Test", ct);
        if (networkScan.Success && networkScan.Data is { } networkData)
        {
            await Torture_Type(results, $"{p}: TYPE [Port]", networkData, "[Port]", "9443", ct);
        }
        else
        {
            results.Add(Result($"{p}: SCAN_WINDOW after [TabNetwork]", false, networkScan.Message));
        }

        // ── Scheduler tab ─────────────────────────────────────────────────────
        await Torture_ClickTab(results, $"{p}: switch → [TabScheduler]", td, "[TabScheduler]", ct);
        await Task.Delay(_actionDelayMs, ct);
        var schedulerScan = await _client.SendAsync("SCAN_WINDOW WPF Torture Test", ct);
        if (schedulerScan.Success && schedulerScan.Data is { } schedulerData)
        {
            await Torture_Toggle(results, $"{p}: toggle [RetryOnFail]", schedulerData, "[RetryOnFail]", ct);
            await Torture_Toggle(results, $"{p}: toggle [RunOnMissed]", schedulerData, "[RunOnMissed]", ct);
        }
        else
        {
            results.Add(Result($"{p}: SCAN_WINDOW after [TabScheduler]", false, schedulerScan.Message));
        }

        // ── Layout tab — WrapPanel action buttons ─────────────────────────────
        await Torture_ClickTab(results, $"{p}: switch → [TabLayout]", td, "[TabLayout]", ct);
        await Task.Delay(_actionDelayMs, ct);
        var layoutScan = await _client.SendAsync("SCAN_WINDOW WPF Torture Test", ct);
        if (layoutScan.Success && layoutScan.Data is { } layoutData)
        {
            foreach (var btn in new[] { "[BtnApply]", "[BtnValidate]", "[BtnReset]", "[BtnRefresh]", "[BtnExport]", "[BtnDeploy]" })
            {
                var btnId = FindId(layoutData, btn);
                if (btnId.HasValue)
                    await Test(results, $"{p}: CLICK {btn}", $"CLICK {btnId}", r => r.Success, ct);
                else
                    results.Add(Result($"{p}: find {btn}", false, "Not found"));
            }
        }
        else
        {
            results.Add(Result($"{p}: SCAN_WINDOW after [TabLayout]", false, layoutScan.Message));
        }

        // ── WPF tab — Expanders, ToggleButtons, CheckBox, nested sub-tabs ─────
        await Torture_ClickTab(results, $"{p}: switch → [TabWpf]", td, "[TabWpf]", ct);
        await Task.Delay(_actionDelayMs, ct);
        var wpfTabScan = await _client.SendAsync("SCAN_WINDOW WPF Torture Test", ct);
        if (wpfTabScan.Success && wpfTabScan.Data is { } wpfTabData)
        {
            // Expanders (support ExpandCollapsePattern)
            foreach (var expAid in new[] { "[ExpanderServer]", "[ExpanderFeatures]", "[ExpanderLogging]" })
            {
                var eid = FindId(wpfTabData, expAid);
                if (eid.HasValue)
                {
                    await Test(results, $"{p}: EXPAND {expAid}",   $"EXPAND {eid}",   r => r.Success, ct);
                    await Test(results, $"{p}: COLLAPSE {expAid}", $"COLLAPSE {eid}", r => r.Success, ct);
                }
                else results.Add(Result($"{p}: find {expAid}", false, "Not found"));
            }

            // ToggleButtons (WPF ToggleButton supports TogglePattern)
            await Torture_Toggle(results, $"{p}: toggle [Toggle1]",  wpfTabData, "[Toggle1]",  ct);
            await Torture_Toggle(results, $"{p}: toggle [Toggle2]",  wpfTabData, "[Toggle2]",  ct);

            // CheckBox
            await Torture_Toggle(results, $"{p}: toggle [CbNormal]", wpfTabData, "[CbNormal]", ct);

            // Nested sub-tabs (TabControl inside WPF tab)
            foreach (var subTab in new[] { "[SubTabB]", "[SubTabC]", "[SubTabA]" })
                await Torture_ClickTab(results, $"{p}: sub-tab {subTab}", wpfTabData, subTab, ct);
        }
        else
        {
            results.Add(Result($"{p}: SCAN_WINDOW after [TabWpf]", false, wpfTabScan.Message));
        }

        // ── Return to Identity ─────────────────────────────────────────────────
        await Torture_ClickTab(results, $"{p}: switch → [TabIdentity] (restore)", td, "[TabIdentity]", ct);
    }

    // ── Shared Torture-test helpers ────────────────────────────────────────────

    /// <summary>Toggle an element (and optionally restore it) — records two results.</summary>
    private async Task Torture_Toggle(
        List<TestResult> results, string name, string scanData,
        string search, CancellationToken ct, bool restoreAfter = true)
    {
        var id = FindId(scanData, search);
        if (!id.HasValue) { results.Add(Result(name, false, $"Not found: {search}")); return; }
        await Test(results, name,               $"TOGGLE {id}", r => r.Success, ct);
        if (restoreAfter)
            await Test(results, $"{name} (restore)", $"TOGGLE {id}", r => r.Success, ct);
    }

    /// <summary>Type a value into a text field found by search token.</summary>
    private async Task Torture_Type(
        List<TestResult> results, string name, string scanData,
        string search, string value, CancellationToken ct)
    {
        var id = FindId(scanData, search);
        if (!id.HasValue) { results.Add(Result(name, false, $"Not found: {search}")); return; }
        await Test(results, name, $"TYPE {id} {value}", r => r.Success, ct);
    }

    /// <summary>Click a tab/item identified by a search token.</summary>
    private async Task Torture_ClickTab(
        List<TestResult> results, string name, string scanData,
        string search, CancellationToken ct)
    {
        var id = FindId(scanData, search);
        if (!id.HasValue) { results.Add(Result(name, false, $"Not found: {search}")); return; }
        await Test(results, name, $"CLICK {id}", r => r.Success, ct);
    }

    // ── Core helpers ──────────────────────────────────────────────────────────
    private async Task Test(List<TestResult> results, string name, string command,
        Func<BridgeResponse, bool> assert, CancellationToken ct)
    {
        if (_skipTests.Contains(name))
        {
            results.Add(new TestResult(name, true, "Skipped — previously passed", Skipped: true));
            return;
        }
        var r = await _client.SendAsync(command, ct);
        results.Add(Result(name, assert(r), r.Message));
    }

    private static TestResult Result(string name, bool passed, string detail) =>
        new(name, passed, detail);

    /// <summary>Find the first element ID whose line contains <paramref name="text"/>.</summary>
    private static long? FindId(string scanData, string text)
    {
        foreach (var line in scanData.Split('\n'))
        {
            if (!line.Contains(text, StringComparison.OrdinalIgnoreCase)) continue;
            var m = Regex.Match(line, @"ID:(\d+)");
            if (m.Success && long.TryParse(m.Groups[1].Value, out var id)) return id;
        }
        return null;
    }

    /// <summary>Find the first element ID whose line contains a given control type keyword.</summary>
    private static long? FindIdByType(string scanData, string typeName)
    {
        foreach (var line in scanData.Split('\n'))
        {
            if (!line.Contains(typeName, StringComparison.OrdinalIgnoreCase)) continue;
            var m = Regex.Match(line, @"ID:(\d+)");
            if (m.Success && long.TryParse(m.Groups[1].Value, out var id)) return id;
        }
        return null;
    }

    private static string BuildWebTarget(string webBaseUrl, string pagePath)
    {
        if (Uri.TryCreate(pagePath, UriKind.Absolute, out var absolute))
            return absolute.ToString();

        var baseUrl = webBaseUrl.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(pagePath) || pagePath == "/")
            return baseUrl;

        return $"{baseUrl}/{pagePath.TrimStart('/')}";
    }
}

// ── Result types ───────────────────────────────────────────────────────────────
public sealed record TestResult(string Name, bool Passed, string Detail, bool Skipped = false);

public sealed class CycleResult
{
    public IReadOnlyList<TestResult> Results  { get; }
    public int      Passed     => Results.Count(r =>  r.Passed && !r.Skipped);
    public int      Failed     => Results.Count(r => !r.Passed);
    public int      Skipped    => Results.Count(r =>  r.Skipped);
    public bool     AllPassed  => Failed == 0;
    public DateTime RunAt      { get; } = DateTime.Now;

    public CycleResult(IEnumerable<TestResult> results) =>
        Results = results.ToList().AsReadOnly();

    public string Summary()
    {
        var skippedLine = Skipped > 0 ? $"   ⏭️ {Skipped} skipped" : "";
        return $"✅ {Passed} passed   ❌ {Failed} failed{skippedLine}\n" +
            string.Join("\n", Results.Where(r => !r.Skipped).Select(r =>
                $"  {(r.Passed ? "✅" : "❌")} {r.Name}" +
                (r.Passed ? "" : $"\n       ↳ {r.Detail}")));
    }
}
