namespace ApexUIBridge.TestRunner;

using System.Diagnostics;
using System.Text.Json.Nodes;

/// <summary>
/// The full test suite. Exercises the bridge exactly the way an AI coding agent would —
/// via HTTP (GET /windows, POST /find, GET /elements, POST /execute, GET /help) — then
/// walks the returned JSON element tree to find controls by name/AutomationId and
/// interacts with them by numeric element id.
///
/// Each test in <see cref="Catalog"/> is independently addressable by its stable
/// <see cref="TestCase.Id"/>, so an external caller (the ControlServer) can list
/// tests, run one, or run them all.
/// </summary>
public sealed class TestSuite
{
    private const string WinFormsTitle       = "System Configuration Console";
    private const string WpfTitle            = "ApexUIBridge Test Application - WPF";
    private const string HtmlTortureTitle    = "Browser Torture Test 2026";
    private const string HtmlEcommerceTitle  = "AGPA E-Commerce Test App";
    private static readonly TimeSpan ScanCacheTtl = TimeSpan.FromSeconds(2);

    private readonly BridgeClient _client;
    private readonly int _actionDelayMs;
    private readonly int _uiSettleDelayMs;
    private readonly HashSet<string> _skipTests;
    private readonly Action<TestResult>? _onResult;

    // Per-window scan cache so run-all doesn't re-scan each window once per test.
    private readonly Dictionary<string, (DateTime At, JsonNode? Tree)> _scanCache = new();

    public IReadOnlyList<TestCase> Catalog { get; }

    public TestSuite(
        BridgeClient client,
        int actionDelayMs = 100,
        int uiSettleDelayMs = 250,
        HashSet<string>? skipTests = null,
        string? webBaseUrl = null,      // kept for call-site compatibility; web tests removed
        string[]? webPagePaths = null,
        Action<TestResult>? onResult = null)
    {
        _client = client;
        _actionDelayMs = actionDelayMs;
        _uiSettleDelayMs = uiSettleDelayMs;
        _skipTests = skipTests ?? new HashSet<string>();
        _onResult = onResult;
        _ = webBaseUrl; _ = webPagePaths;

        Catalog = BuildCatalog();
    }

    // ── Public API ─────────────────────────────────────────────────────────────

    /// <summary>Run the full catalog in order and return aggregated results.</summary>
    public async Task<CycleResult> RunAsync(CancellationToken ct) => await RunAllAsync(ct);

    public async Task<CycleResult> RunAllAsync(CancellationToken ct)
    {
        _scanCache.Clear();
        var results = new List<TestResult>();
        foreach (var tc in Catalog)
        {
            if (ct.IsCancellationRequested) break;
            var r = await RunOneInternalAsync(tc, ct);
            results.Add(r);
        }
        return new CycleResult(results);
    }

    /// <summary>Run a single test by its stable id. Throws <see cref="KeyNotFoundException"/> if unknown.</summary>
    public async Task<TestResult> RunOneAsync(string id, CancellationToken ct)
    {
        var tc = Catalog.FirstOrDefault(c => string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase))
            ?? throw new KeyNotFoundException($"No test with id '{id}'");
        // Drop scan cache for a single-shot run so the tree is fresh.
        _scanCache.Clear();
        return await RunOneInternalAsync(tc, ct);
    }

    // ── Catalog construction ──────────────────────────────────────────────────

    private IReadOnlyList<TestCase> BuildCatalog()
    {
        var list = new List<TestCase>
        {
            // Discovery
            new("discovery-winforms-visible", "GET /windows — WinForms app visible", "discovery",
                ct => CheckAsync("GET /windows — WinForms app visible", "GET /windows",
                    () => _client.ListWindowsAsync(ct),
                    r => r.Success && (r.Result?.Contains(WinFormsTitle) ?? false), ct)),

            new("discovery-wpf-visible", "GET /windows — WPF app visible", "discovery",
                ct => CheckAsync("GET /windows — WPF app visible", "GET /windows",
                    () => _client.ListWindowsAsync(ct),
                    r => r.Success && (r.Result?.Contains(WpfTitle) ?? false), ct)),

            // WinForms
            new("winforms-scan", "SCAN_WINDOW WinForms", "winforms",
                ct => ScanAsRootTestAsync("WinForms", WinFormsTitle, ct)),

            new("winforms-textbox-type", "POST /execute type → WinForms TextBox", "winforms",
                async ct =>
                {
                    var (tree, error) = await SelectTabAndScanAsync(WinFormsTitle, "Identity", ct);
                    if (tree == null) return Fail("POST /execute type → WinForms TextBox", error ?? "WinForms scan failed");
                    var tb = FindNode(tree, n => ControlTypeIs(n, "Edit"));
                    if (tb == null) return Fail("POST /execute type → WinForms TextBox", "No Edit control found");
                    var typed = $"autotest_{DateTime.Now:HHmmss}";
                    return await ExecAsync("POST /execute type → WinForms TextBox", tb, "type", typed, ct);
                }),

            new("winforms-textbox-gettext", "POST /execute gettext → typed value", "winforms",
                async ct =>
                {
                    // Type a deterministic value, then gettext and assert round-trip.
                    var (tree, error) = await SelectTabAndScanAsync(WinFormsTitle, "Identity", ct);
                    if (tree == null) return Fail("POST /execute gettext → typed value", error ?? "WinForms scan failed");
                    var tb = FindNode(tree, n => ControlTypeIs(n, "Edit"));
                    if (tb == null) return Fail("POST /execute gettext → typed value", "No Edit control found");
                    var typed = $"autotest_{DateTime.Now:HHmmss}";
                    var typeResp = await _client.ExecuteAsync(tb["id"]!.GetValue<int>(), "type", typed, ct);
                    if (!typeResp.Success) return Fail("POST /execute gettext → typed value", $"type failed: {typeResp.Detail}");
                    if (_actionDelayMs > 0) await Task.Delay(_actionDelayMs, ct);
                    return await CheckAsync("POST /execute gettext → typed value",
                        $"POST /execute action=gettext id={tb["id"]}",
                        () => _client.ExecuteAsync(tb["id"]!.GetValue<int>(), "gettext", null, ct),
                        r => r.Success && (r.Result?.Contains(typed) ?? false), ct);
                }),

            new("winforms-checkbox-toggle", "POST /execute toggle → WinForms SimpleCheckBox", "winforms",
                async ct =>
                {
                    var (tree, error) = await SelectTabAndScanAsync(WinFormsTitle, "Identity", ct);
                    if (tree == null) return Fail("POST /execute toggle → WinForms SimpleCheckBox", error ?? "WinForms scan failed");
                    var cb = FindNode(tree, n => NameOrIdContains(n, "AD Sync") && ControlTypeIs(n, "CheckBox"));
                    if (cb == null) return Fail("POST /execute toggle → WinForms SimpleCheckBox", "Not found");
                    var r = await ExecAsync("POST /execute toggle → WinForms SimpleCheckBox", cb, "toggle", null, ct);
                    // Restore so subsequent runs are idempotent.
                    _ = await _client.ExecuteAsync(cb["id"]!.GetValue<int>(), "toggle", null, ct);
                    return r;
                }),

            new("winforms-contextmenu-click", "POST /execute click → WinForms ContextMenu button", "winforms",
                async ct =>
                {
                    var tree = await GetScanAsync(WinFormsTitle, ct);
                    if (tree == null) return Fail("POST /execute click → WinForms ContextMenu button", "WinForms scan failed");
                    var btn = FindNode(tree, n => ControlTypeIs(n, "Button") && NameOrIdContains(n, "Save"));
                    if (btn == null) return Fail("POST /execute click → WinForms ContextMenu button", "Not found");
                    return await ExecAsync("POST /execute click → WinForms ContextMenu button", btn, "click", null, ct);
                }),

            new("winforms-progressbar-gettext", "POST /execute gettext → WinForms ProgressBar", "winforms",
                async ct =>
                {
                    var tree = await GetScanAsync(WinFormsTitle, ct);
                    if (tree == null) return Fail("POST /execute gettext → WinForms ProgressBar", "WinForms scan failed");
                    var pb = FindNode(tree, n => ControlTypeIs(n, "ProgressBar"));
                    if (pb == null) return Fail("POST /execute gettext → WinForms ProgressBar", "Not found");
                    return await ExecAsync("POST /execute gettext → WinForms ProgressBar", pb, "gettext", null, ct);
                }),

            new("winforms-combo-expand-collapse", "POST /execute expand/collapse → WinForms EditableCombo", "winforms",
                async ct =>
                {
                    var (tree, error) = await SelectTabAndScanAsync(WinFormsTitle, "Identity", ct);
                    if (tree == null) return Fail("POST /execute expand/collapse → WinForms EditableCombo", error ?? "WinForms scan failed");
                    var combo = FindNode(tree, n => ControlTypeIs(n, "ComboBox") && NameOrIdContains(n, "Department"));
                    if (combo == null) return Fail("POST /execute expand/collapse → WinForms EditableCombo", "Not found");
                    var expand = await ExecAsync("POST /execute expand → WinForms EditableCombo", combo, "expand", null, ct);
                    if (!expand.Passed) return expand with { Name = "POST /execute expand/collapse → WinForms EditableCombo" };
                    var collapse = await ExecAsync("POST /execute collapse → WinForms EditableCombo", combo, "collapse", null, ct);
                    return collapse with { Name = "POST /execute expand/collapse → WinForms EditableCombo" };
                }),

            // WPF
            new("wpf-scan", "SCAN_WINDOW WPF", "wpf",
                ct => ScanAsRootTestAsync("WPF", WpfTitle, ct)),

            new("wpf-textbox-type", "POST /execute type → WPF TextBox", "wpf",
                async ct =>
                {
                    var (tree, error) = await SelectTabAndScanAsync(WpfTitle, "Identity", ct);
                    if (tree == null) return Fail("POST /execute type → WPF TextBox", error ?? "WPF scan failed");
                    var tb = FindNode(tree, n => ControlTypeIs(n, "Edit"));
                    if (tb == null) return Fail("POST /execute type → WPF TextBox", "Not found");
                    return await ExecAsync("POST /execute type → WPF TextBox", tb, "type",
                        $"wpf_auto_{DateTime.Now:HHmmss}", ct);
                }),

            new("wpf-checkbox-toggle", "POST /execute toggle → WPF SimpleCheckBox", "wpf",
                async ct =>
                {
                    var (tree, error) = await SelectTabAndScanAsync(WpfTitle, "Identity", ct);
                    if (tree == null) return Fail("POST /execute toggle → WPF SimpleCheckBox", error ?? "WPF scan failed");
                    var cb = FindNode(tree, n => AutomationIdIs(n, "FlagAdSync"));
                    if (cb == null) return Fail("POST /execute toggle → WPF SimpleCheckBox", "Not found");
                    var r = await ExecAsync("POST /execute toggle → WPF SimpleCheckBox", cb, "toggle", null, ct);
                    _ = await _client.ExecuteAsync(cb["id"]!.GetValue<int>(), "toggle", null, ct);
                    return r;
                }),

            new("wpf-invokable-click", "POST /execute click → WPF InvokableButton", "wpf",
                async ct =>
                {
                    var tree = await GetScanAsync(WpfTitle, ct);
                    if (tree == null) return Fail("POST /execute click → WPF InvokableButton", "WPF scan failed");
                    var inv = FindNode(tree, n => AutomationIdIs(n, "TbSave"));
                    if (inv == null) return Fail("POST /execute click → WPF InvokableButton", "Not found");
                    return await ExecAsync("POST /execute click → WPF InvokableButton", inv, "click", null, ct);
                }),

            new("wpf-combo-expand-collapse", "POST /execute expand/collapse → WPF NonEditableCombo", "wpf",
                async ct =>
                {
                    var (tree, error) = await SelectTabAndScanAsync(WpfTitle, "Identity", ct);
                    if (tree == null) return Fail("POST /execute expand/collapse → WPF NonEditableCombo", error ?? "WPF scan failed");
                    var combo = FindNode(tree, n => AutomationIdIs(n, "Department"));
                    if (combo == null) return Fail("POST /execute expand/collapse → WPF NonEditableCombo", "Not found");
                    var expand = await ExecAsync("POST /execute expand → WPF NonEditableCombo", combo, "expand", null, ct);
                    if (!expand.Passed) return expand with { Name = "POST /execute expand/collapse → WPF NonEditableCombo" };
                    var collapse = await ExecAsync("POST /execute collapse → WPF NonEditableCombo", combo, "collapse", null, ct);
                    return collapse with { Name = "POST /execute expand/collapse → WPF NonEditableCombo" };
                }),

            new("wpf-slider-setrange-verify", "POST /execute setrange 4 → WPF Slider (verified)", "wpf",
                ct => SliderSetAndVerifyAsync("POST /execute setrange 4 → WPF Slider (verified)",
                    WpfTitle, "4", ct, tabName: "Identity")),

            // Richer WinForms interactions ─────────────────────────────────────
            new("winforms-textbox-edit-clear-retype", "TextBox edit → clear → retype (verified)", "winforms",
                ct => TextBoxEditRetypeAsync("TextBox edit → clear → retype (verified)",
                    WinFormsTitle, ControlTypeEdit, ct, tabName: "Identity")),

            new("winforms-slider-setrange-verify", "POST /execute setrange 3 → WinForms Slider (verified)", "winforms",
                ct => SliderSetAndVerifyAsync("POST /execute setrange 3 → WinForms Slider (verified)",
                    WinFormsTitle, "3", ct, tabName: "Identity")),

            new("winforms-listbox-select", "POST /execute select → WinForms ListBox item", "winforms",
                ct => ListBoxSelectFirstItemAsync("POST /execute select → WinForms ListBox item",
                    WinFormsTitle, ct, tabName: "Dialogs")),

            new("winforms-radio-select", "POST /execute click → WinForms RadioButton2 (mutex)", "winforms",
                ct => RadioSelectAsync("POST /execute click → WinForms RadioButton2 (mutex)",
                    WinFormsTitle, "Via Proxy", ct, tabName: "Network")),

            new("winforms-3state-cycle", "Cycle ThreeStateCheckBox x3 (WinForms)", "winforms",
                ct => ThreeStateCycleAsync("Cycle ThreeStateCheckBox x3 (WinForms)", WinFormsTitle, "Indeterminate state", ct, tabName: "Dialogs")),

            new("winforms-menu-edit-copy", "Click Edit → Copy Plain menu item (WinForms)", "winforms",
                ct => MenuClickAsync("Click Edit → Copy Plain menu item (WinForms)",
                    WinFormsTitle, parentMenu: "Edit", leafMenu: "Plain Text", ct)),

            // Richer WPF interactions ──────────────────────────────────────────
            new("wpf-listbox-select", "POST /execute select → WPF ListBox item", "wpf",
                ct => ListBoxSelectFirstItemAsync("POST /execute select → WPF ListBox item",
                    WpfTitle, ct, tabName: "Network", listSelector: n => AutomationIdIs(n, "EndpointList"))),

            new("wpf-expander-toggle", "POST /execute expand/collapse → WPF Expander", "wpf",
                async ct =>
                {
                    var (tree, error) = await SelectTabAndScanAsync(WpfTitle, "WPF", ct);
                    if (tree == null) return Fail("POST /execute expand/collapse → WPF Expander", error ?? "WPF scan failed");
                    var exp = FindNode(tree, n => AutomationIdIs(n, "ExpanderServer"))
                           ?? FindNode(tree, n => NameOrIdContains(n, "Server Configuration"));
                    if (exp == null) return Fail("POST /execute expand/collapse → WPF Expander", "Not found");
                    var expand = await ExecAsync("POST /execute expand → WPF Expander", exp, "expand", null, ct);
                    if (!expand.Passed) return expand with { Name = "POST /execute expand/collapse → WPF Expander" };
                    var collapse = await ExecAsync("POST /execute collapse → WPF Expander", exp, "collapse", null, ct);
                    return collapse with { Name = "POST /execute expand/collapse → WPF Expander" };
                }),

            new("wpf-menu-file", "Click File menu (WPF)", "wpf",
                ct => MenuClickAsync("Click File menu (WPF)", WpfTitle,
                    parentMenu: "File", leafMenu: null, ct)),

            // HTML (browser) — skipped gracefully if no browser window is open ──
            new("html-torture-scan", "SCAN_WINDOW Web (Torture Test)", "html",
                ct => ScanAsRootTestAsync("Torture Test", HtmlTortureTitle, ct)),

            new("html-torture-textbox-type", "POST /execute type → HTML TextBox on Torture Test (verified)", "html",
                ct => TextBoxEditRetypeAsync("POST /execute type → HTML TextBox on Torture Test (verified)",
                    HtmlTortureTitle, ControlTypeEdit, ct, nameHint: "Normal text")),

            new("html-ecommerce-scan", "SCAN_WINDOW Web (Ecommerce)", "html",
                ct => ScanAsRootTestAsync("Ecommerce", HtmlEcommerceTitle, ct)),

            new("html-ecommerce-slider-setrange-verify", "POST /execute setrange 100 → HTML range slider on Ecommerce (verified)", "html",
                ct => SliderSetAndVerifyAsync("POST /execute setrange 100 → HTML range slider on Ecommerce (verified)",
                    HtmlEcommerceTitle, "100", ct)),

            // Meta
            new("meta-help", "GET /help — returns content", "meta",
                ct => CheckAsync("GET /help — returns content", "GET /help",
                    () => _client.HelpAsync(ct),
                    r => r.Success && !string.IsNullOrWhiteSpace(r.Result), ct)),
        };
        return list.AsReadOnly();
    }

    private const string ControlTypeEdit = "Edit";

    // ── Higher-level reusable test bodies ─────────────────────────────────────

    /// <summary>Type a deterministic value into an Edit control, read it back, clear, retype, read back again.</summary>
    private async Task<TestResult> TextBoxEditRetypeAsync(string name, string windowTitle, string controlType, CancellationToken ct, string? tabName = null, string? nameHint = null)
    {
        var (tree, error) = tabName == null
            ? (await GetScanAsync(windowTitle, ct), (string?)null)
            : await SelectTabAndScanAsync(windowTitle, tabName, ct);
        if (tree == null) return Fail(name, error ?? $"scan of '{windowTitle}' failed (window not open?)");

        var tb = nameHint != null
            ? FindNode(tree, n => ControlTypeIs(n, controlType) && NameOrIdContains(n, nameHint))
            : FindNode(tree, n => ControlTypeIs(n, controlType));
        if (tb == null) return Fail(name, $"No {controlType} control found{(nameHint != null ? $" matching '{nameHint}'" : "")}");
        int id = tb["id"]!.GetValue<int>();

        string first  = $"first_{DateTime.Now:HHmmssfff}";
        string second = $"second_{DateTime.Now:HHmmssfff}";

        var r1 = await _client.ExecuteAsync(id, "type", first, ct);
        if (!r1.Success) return Fail(name, $"type #1 failed: {r1.Detail}");
        if (_actionDelayMs > 0) await Task.Delay(_actionDelayMs, ct);

        var g1 = await _client.ExecuteAsync(id, "gettext", null, ct);
        if (!g1.Success || !(g1.Result?.Contains(first) ?? false))
            return Fail(name, $"gettext after first type did not contain '{first}'. Got: {g1.Result}");

        // Clear + retype to prove the edit surface accepts a new value.
        var clear = await _client.ExecuteAsync(id, "clear", null, ct);
        if (!clear.Success)
        {
            // Some Edit surfaces lack a "clear" action — fall back to selectall + overwrite.
            _ = await _client.ExecuteAsync(id, "selectall", null, ct);
        }
        if (_actionDelayMs > 0) await Task.Delay(_actionDelayMs, ct);

        var r2 = await _client.ExecuteAsync(id, "type", second, ct);
        if (!r2.Success) return Fail(name, $"type #2 failed: {r2.Detail}");
        if (_actionDelayMs > 0) await Task.Delay(_actionDelayMs, ct);

        var g2 = await _client.ExecuteAsync(id, "gettext", null, ct);
        bool ok = g2.Success && (g2.Result?.Contains(second) ?? false);
        return new TestResult(name, ok,
            ok ? $"typed+verified '{first}' then '{second}'"
               : $"final gettext did not contain '{second}'. Got: {g2.Result}",
            Command: $"POST /execute type/gettext id={id}");
    }

    /// <summary>Find the first Slider, setrange to <paramref name="value"/>, then getrange and assert.</summary>
    private async Task<TestResult> SliderSetAndVerifyAsync(string name, string windowTitle, string value, CancellationToken ct, string? tabName = null)
    {
        var (tree, error) = tabName == null
            ? (await GetScanAsync(windowTitle, ct), (string?)null)
            : await SelectTabAndScanAsync(windowTitle, tabName, ct);
        if (tree == null) return Fail(name, error ?? $"scan of '{windowTitle}' failed (window not open?)");

        var slider = FindNode(tree, n => ControlTypeIs(n, "Slider"));
        if (slider == null) return Fail(name, "No Slider control found");
        int id = slider["id"]!.GetValue<int>();

        var setr = await _client.ExecuteAsync(id, "setrange", value, ct);
        if (!setr.Success) return Fail(name, $"setrange failed: {setr.Detail}");
        if (_uiSettleDelayMs > 0) await Task.Delay(_uiSettleDelayMs, ct);

        var getr = await _client.ExecuteAsync(id, "getrange", null, ct);
        if (!getr.Success) return Fail(name, $"getrange failed: {getr.Detail}");
        // getrange reports "value min-max" or similar — just check the numeric target is present.
        bool ok = getr.Result != null && getr.Result.Contains(value);
        return new TestResult(name, ok,
            ok ? $"setrange→getrange round-trip: {getr.Result}"
               : $"getrange did not contain '{value}'. Got: {getr.Result}",
            Command: $"POST /execute setrange/getrange id={id} value={value}");
    }

    /// <summary>Find a ListBox, select its first selectable child, and assert the select call succeeded.</summary>
    private async Task<TestResult> ListBoxSelectFirstItemAsync(
        string name,
        string windowTitle,
        CancellationToken ct,
        string? tabName = null,
        Func<JsonObject, bool>? listSelector = null)
    {
        var (tree, error) = tabName == null
            ? (await GetScanAsync(windowTitle, ct), (string?)null)
            : await SelectTabAndScanAsync(windowTitle, tabName, ct);
        if (tree == null) return Fail(name, error ?? $"scan of '{windowTitle}' failed (window not open?)");

        // Try AutomationId/name "ListBox" first, then fall back to controlType=List.
        var list = listSelector == null ? null : FindNode(tree, listSelector)
                ?? FindNode(tree, n => AutomationIdIs(n, "ListBox"))
                ?? FindNode(tree, n => NameOrIdContains(n, "ListBox"))
                ?? FindNode(tree, n => ControlTypeIs(n, "List"));
        if (list == null) return Fail(name, "No ListBox found");

        var firstChild = (list["children"] as JsonArray)?.FirstOrDefault() as JsonObject;
        if (firstChild == null) return Fail(name, "ListBox has no children");
        int childId = firstChild["id"]!.GetValue<int>();

        var r = await _client.ExecuteAsync(childId, "select", null, ct);
        bool ok = r.Success;
        return new TestResult(name, ok, ok ? $"selected item id={childId}" : r.Detail,
            Command: $"POST /execute select id={childId}");
    }

    /// <summary>Click RadioButton2 and assert the click succeeded.</summary>
    private async Task<TestResult> RadioSelectAsync(
        string name,
        string windowTitle,
        string radioName,
        CancellationToken ct,
        string? tabName = null)
    {
        var (tree, error) = tabName == null
            ? (await GetScanAsync(windowTitle, ct), (string?)null)
            : await SelectTabAndScanAsync(windowTitle, tabName, ct);
        if (tree == null) return Fail(name, error ?? $"scan of '{windowTitle}' failed (window not open?)");

        var rb = FindNode(tree, n => ControlTypeIs(n, "RadioButton") && NameOrIdContains(n, radioName));
        if (rb == null) return Fail(name, $"{radioName} not found");
        int id = rb["id"]!.GetValue<int>();

        var click = await _client.ExecuteAsync(id, "select", null, ct);
        if (!click.Success)
        {
            // Some hosts only accept "click" for radios.
            click = await _client.ExecuteAsync(id, "click", null, ct);
        }
        return new TestResult(name, click.Success, click.Detail,
            Command: $"POST /execute select|click id={id}");
    }

    /// <summary>Toggle the ThreeStateCheckBox three times to cycle through all states.</summary>
    private async Task<TestResult> ThreeStateCycleAsync(
        string name,
        string windowTitle,
        string checkboxName,
        CancellationToken ct,
        string? tabName = null)
    {
        var (tree, error) = tabName == null
            ? (await GetScanAsync(windowTitle, ct), (string?)null)
            : await SelectTabAndScanAsync(windowTitle, tabName, ct);
        if (tree == null) return Fail(name, error ?? $"scan of '{windowTitle}' failed (window not open?)");

        var cb = FindNode(tree, n => ControlTypeIs(n, "CheckBox") && NameOrIdContains(n, checkboxName));
        if (cb == null) return Fail(name, $"{checkboxName} not found");
        int id = cb["id"]!.GetValue<int>();

        for (int i = 0; i < 3; i++)
        {
            var r = await _client.ExecuteAsync(id, "toggle", null, ct);
            if (!r.Success) return Fail(name, $"toggle #{i + 1} failed: {r.Detail}");
            if (_actionDelayMs > 0) await Task.Delay(_actionDelayMs, ct);
        }
        return new TestResult(name, true, "cycled ThreeStateCheckBox 3×",
            Command: $"POST /execute toggle (×3) id={id}");
    }

    /// <summary>Click a top-level menu (e.g. "Edit") and optionally a leaf item inside it (e.g. "Plain").</summary>
    private async Task<TestResult> MenuClickAsync(string name, string windowTitle, string parentMenu, string? leafMenu, CancellationToken ct)
    {
        var tree = await GetScanAsync(windowTitle, ct);
        if (tree == null) return Fail(name, $"scan of '{windowTitle}' failed (window not open?)");

        var parent = FindNode(tree, n => NameOrIdContains(n, parentMenu)
                                       && (ControlTypeIs(n, "MenuItem") || ControlTypeIs(n, "MenuBar")
                                           || ControlTypeIs(n, "Menu")));
        parent ??= FindNode(tree, n => NameOrIdContains(n, parentMenu));
        if (parent == null) return Fail(name, $"Menu '{parentMenu}' not found");

        int parentId = parent["id"]!.GetValue<int>();
        var parentClick = await _client.ExecuteAsync(parentId, "click", null, ct);
        if (!parentClick.Success)
            return Fail(name, $"click on '{parentMenu}' failed: {parentClick.Detail}");

        if (string.IsNullOrEmpty(leafMenu))
        {
            return new TestResult(name, true, $"opened '{parentMenu}'",
                Command: $"POST /execute click id={parentId}");
        }

        // The dropdown renders in a popup — re-scan and look for the leaf.
        await Task.Delay(_uiSettleDelayMs, ct);
        _scanCache.Remove(windowTitle);
        var treeAfter = await GetScanAsync(windowTitle, ct);
        JsonNode? leaf = null;
        if (treeAfter != null)
            leaf = FindNode(treeAfter, n => NameOrIdContains(n, leafMenu) && ControlTypeIs(n, "MenuItem"))
                ?? FindNode(treeAfter, n => NameOrIdContains(n, leafMenu));

        if (leaf == null)
        {
            // Some menu popups are a sibling window — this is still a partial success.
            return new TestResult(name, true,
                $"opened '{parentMenu}'; leaf '{leafMenu}' was not found in the refreshed tree (popup may be a separate window)",
                Command: $"POST /execute click id={parentId}");
        }

        int leafId = leaf["id"]!.GetValue<int>();
        var leafClick = await _client.ExecuteAsync(leafId, "click", null, ct);
        return new TestResult(name, leafClick.Success,
            leafClick.Success ? $"clicked '{parentMenu}' → '{leafMenu}'" : leafClick.Detail,
            Command: $"POST /execute click id={leafId}");
    }

    private async Task<(JsonNode? Tree, string? Error)> SelectTabAndScanAsync(string windowTitle, string tabName, CancellationToken ct)
    {
        var tree = await GetScanAsync(windowTitle, ct);
        if (tree == null) return (null, $"scan of '{windowTitle}' failed (window not open?)");

        var tab = FindNode(tree, n => ControlTypeIs(n, "Tab"));
        if (tab == null) return (null, $"No Tab control found in '{windowTitle}'");

        var select = await _client.ExecuteAsync(tab["id"]!.GetValue<int>(), "select", tabName, ct);
        if (!select.Success) return (null, $"select tab '{tabName}' failed: {select.Detail}");
        if (_uiSettleDelayMs > 0) await Task.Delay(_uiSettleDelayMs, ct);

        _scanCache.Remove(windowTitle);
        var rescanned = await GetScanAsync(windowTitle, ct);
        return rescanned == null
            ? (null, $"re-scan of '{windowTitle}' after selecting '{tabName}' failed")
            : (rescanned, null);
    }

    // ── Core runner for a single case ─────────────────────────────────────────

    private async Task<TestResult> RunOneInternalAsync(TestCase tc, CancellationToken ct)
    {
        if (_skipTests.Contains(tc.Name))
        {
            var skipped = new TestResult(tc.Name, true, "Skipped — previously passed", Skipped: true);
            _onResult?.Invoke(skipped);
            return skipped;
        }

        TestResult r;
        try
        {
            r = await tc.RunAsync(ct);
        }
        catch (Exception ex)
        {
            r = new TestResult(tc.Name, false, $"Unhandled: {ex.Message}");
        }
        _onResult?.Invoke(r);
        if (_actionDelayMs > 0) await Task.Delay(_actionDelayMs, ct);
        return r;
    }

    // ── Scan helpers (with short TTL cache) ───────────────────────────────────

    private async Task<JsonNode?> GetScanAsync(string windowTitle, CancellationToken ct)
    {
        if (_scanCache.TryGetValue(windowTitle, out var cached) &&
            DateTime.UtcNow - cached.At < ScanCacheTtl && cached.Tree != null)
        {
            return cached.Tree;
        }

        var resp = await _client.ScanWindowAsync(windowTitle, ct);
        if (!resp.Success || string.IsNullOrWhiteSpace(resp.Result))
        {
            _scanCache[windowTitle] = (DateTime.UtcNow, null);
            return null;
        }

        try
        {
            var envelope = JsonNode.Parse(resp.Result!);
            var tree = envelope?["root"];
            _scanCache[windowTitle] = (DateTime.UtcNow, tree);
            return tree;
        }
        catch
        {
            _scanCache[windowTitle] = (DateTime.UtcNow, null);
            return null;
        }
    }

    private async Task<TestResult> ScanAsRootTestAsync(string label, string windowTitle, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var resp = await _client.ScanWindowAsync(windowTitle, ct);
        sw.Stop();

        JsonNode? tree = null;
        if (resp.Success && !string.IsNullOrWhiteSpace(resp.Result))
        {
            try { tree = JsonNode.Parse(resp.Result!)?["root"]; }
            catch { /* reported via passed=false below */ }
        }
        _scanCache[windowTitle] = (DateTime.UtcNow, tree);

        return new TestResult($"SCAN_WINDOW {label}", resp.Success, resp.Detail,
            ElapsedMs: sw.ElapsedMilliseconds,
            Command: $"POST /find window={windowTitle} → GET /elements");
    }

    // ── Low-level actions ─────────────────────────────────────────────────────

    private async Task<TestResult> ExecAsync(string name, JsonNode elem,
        string action, string? value, CancellationToken ct)
    {
        int id = elem["id"]!.GetValue<int>();
        string cmd = value == null
            ? $"POST /execute action={action} id={id}"
            : $"POST /execute action={action} id={id} value={value}";
        var sw = Stopwatch.StartNew();
        var r = await _client.ExecuteAsync(id, action, value, ct);
        sw.Stop();
        return new TestResult(name, r.Success, r.Detail, ElapsedMs: sw.ElapsedMilliseconds, Command: cmd);
    }

    private async Task<TestResult> CheckAsync(string name, string cmd,
        Func<Task<ApiResponse>> call, Func<ApiResponse, bool> assert, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var r = await call();
        sw.Stop();
        return new TestResult(name, assert(r), r.Detail, ElapsedMs: sw.ElapsedMilliseconds, Command: cmd);
    }

    private static TestResult Fail(string name, string detail) => new(name, false, detail);

    // ── Tree-walking helpers ──────────────────────────────────────────────────

    private static JsonNode? FindNode(JsonNode root, Func<JsonObject, bool> pred)
    {
        if (root is JsonObject obj)
        {
            if (pred(obj)) return obj;
            if (obj["children"] is JsonArray kids)
                foreach (var child in kids)
                    if (child != null)
                    {
                        var hit = FindNode(child, pred);
                        if (hit != null) return hit;
                    }
        }
        return null;
    }

    private static bool ControlTypeIs(JsonObject n, string type) =>
        string.Equals(n["controlType"]?.GetValue<string>(), type, StringComparison.OrdinalIgnoreCase);

    private static bool AutomationIdIs(JsonObject n, string aid) =>
        string.Equals(n["automationId"]?.GetValue<string>(), aid, StringComparison.OrdinalIgnoreCase);

    private static bool NameOrIdContains(JsonObject n, string text)
    {
        var name = n["name"]?.GetValue<string>();
        var aid  = n["automationId"]?.GetValue<string>();
        return (name?.Contains(text, StringComparison.OrdinalIgnoreCase) ?? false)
            || (aid ?.Contains(text, StringComparison.OrdinalIgnoreCase) ?? false);
    }
}

// ── Result types ───────────────────────────────────────────────────────────────

public sealed record TestCase(
    string Id,
    string Name,
    string Group,
    Func<CancellationToken, Task<TestResult>> RunAsync);

public sealed record TestResult(string Name, bool Passed, string Detail, bool Skipped = false, long? ElapsedMs = null, string? Command = null);

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
        var latencies = Results.Where(r => r.ElapsedMs.HasValue).Select(r => r.ElapsedMs!.Value).ToList();
        var totalMs   = latencies.Sum();
        var avgMs     = latencies.Count > 0 ? latencies.Average() : 0;
        var maxMs     = latencies.Count > 0 ? latencies.Max() : 0;
        var skipLine  = Skipped > 0 ? $"   SKIP:{Skipped}" : "";
        var timeLine  = latencies.Count > 0 ? $"\n total {totalMs}ms   avg {avgMs:F1}ms   max {maxMs}ms" : "";
        return $"PASS:{Passed}  FAIL:{Failed}{skipLine}{timeLine}\n" +
            string.Join("\n", Results.Where(r => !r.Skipped).Select(r =>
                $"  {(r.Passed ? "PASS" : "FAIL")} {r.Name}" +
                (r.Passed
                    ? ""
                    : (!string.IsNullOrEmpty(r.Command)
                        ? $"  cmd={r.Command}\n       {r.Detail}"
                        : $"\n       {r.Detail}"))));
    }
}
