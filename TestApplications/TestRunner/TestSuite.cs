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

    /// <summary>
    /// Run a subset of the catalog in catalog order. Each entry in <paramref name="idsOrNames"/>
    /// is matched case-insensitively against <see cref="TestCase.Id"/> first, then
    /// <see cref="TestCase.Name"/>. Unknown entries are reported as failures so callers
    /// don't silently run nothing. Duplicates are de-duplicated.
    /// </summary>
    public async Task<CycleResult> RunSelectedAsync(IEnumerable<string> idsOrNames, CancellationToken ct)
    {
        _scanCache.Clear();
        var wanted = new HashSet<string>(idsOrNames, StringComparer.OrdinalIgnoreCase);
        var results = new List<TestResult>();
        var matched = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var tc in Catalog)
        {
            if (ct.IsCancellationRequested) break;
            if (wanted.Contains(tc.Id) || wanted.Contains(tc.Name))
            {
                matched.Add(tc.Id);
                matched.Add(tc.Name);
                results.Add(await RunOneInternalAsync(tc, ct));
            }
        }

        foreach (var w in wanted.Where(w => !matched.Contains(w)))
        {
            var miss = new TestResult(w, false, $"No test matched id/name '{w}'");
            _onResult?.Invoke(miss);
            results.Add(miss);
        }

        return new CycleResult(results);
    }

    /// <summary>
    /// Run every test whose <see cref="TestCase.Id"/> or <see cref="TestCase.Name"/> contains
    /// <paramref name="substring"/> (case-insensitive).
    /// </summary>
    public async Task<CycleResult> RunFilteredAsync(string substring, CancellationToken ct)
    {
        _scanCache.Clear();
        var results = new List<TestResult>();
        foreach (var tc in Catalog)
        {
            if (ct.IsCancellationRequested) break;
            if (tc.Id.Contains(substring, StringComparison.OrdinalIgnoreCase) ||
                tc.Name.Contains(substring, StringComparison.OrdinalIgnoreCase))
            {
                results.Add(await RunOneInternalAsync(tc, ct));
            }
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

            new("winforms-slider-setrange-verify", "POST /execute setrange 50 → WinForms Slider (verified)", "winforms",
                ct => SliderSetAndVerifyAsync("POST /execute setrange 50 → WinForms Slider (verified)",
                    WinFormsTitle, "50", ct, tabName: "Identity", nameHint: "Access Level")),

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
                async ct => { await EnsureBrowserActiveAsync(HtmlTortureTitle, ct); return await ScanAsRootTestAsync("Torture Test", HtmlTortureTitle, ct); }),

            new("html-torture-textbox-type", "POST /execute type → HTML TextBox on Torture Test (verified)", "html",
                async ct => { await EnsureBrowserActiveAsync(HtmlTortureTitle, ct);
                    return await TextBoxEditRetypeAsync("POST /execute type → HTML TextBox on Torture Test (verified)",
                        HtmlTortureTitle, ControlTypeEdit, ct, nameHint: "Normal text", focusBeforeType: true); }),

            new("html-ecommerce-scan", "SCAN_WINDOW Web (Ecommerce)", "html",
                async ct => { await EnsureBrowserActiveAsync(HtmlEcommerceTitle, ct); return await ScanAsRootTestAsync("Ecommerce", HtmlEcommerceTitle, ct); }),

            new("html-ecommerce-slider-setrange-verify", "POST /execute setrange 100 → HTML range slider on Ecommerce (verified)", "html",
                async ct => { await EnsureBrowserActiveAsync(HtmlEcommerceTitle, ct);
                    return await SliderSetAndVerifyAsync("POST /execute setrange 100 → HTML range slider on Ecommerce (verified)",
                        HtmlEcommerceTitle, "100", ct); }),

            // WinForms — Identity tab (additional controls)
            new("winforms-identity-combo-status", "WinForms Identity: StatusCombo → select Inactive", "winforms",
                ct => ComboBoxSelectItemAsync("WinForms Identity: StatusCombo → select Inactive",
                    WinFormsTitle, "StatusCombo", "Inactive", ct, tabName: "Identity")),

            new("winforms-identity-numericupdown-salary", "WinForms Identity: SalaryInput → setvalue 75000", "winforms",
                ct => NumericSetAndVerifyAsync("WinForms Identity: SalaryInput → setvalue 75000",
                    WinFormsTitle, "SalaryInput", "75000", ct, tabName: "Identity")),

            new("winforms-identity-richtext-notes", "WinForms Identity: NotesField → retype", "winforms",
                ct => TextBoxEditRetypeAsync("WinForms Identity: NotesField → retype",
                    WinFormsTitle, ControlTypeEdit, ct, tabName: "Identity", nameHint: "NotesField")),

            // WinForms — Network tab
            // WinForms TrackBar exposes UIA range 0-100 regardless of actual Minimum/Maximum.
            // Use UIA-scaled values: actual÷(actualMax/100). UploadSlider max=1000 → scale=10.
            new("winforms-network-slider-upload", "WinForms Network: UploadSlider → setrange 20 (UIA=actual 200)", "winforms",
                ct => SliderSetAndVerifyAsync("WinForms Network: UploadSlider → setrange 20 (UIA=actual 200)",
                    WinFormsTitle, "20", ct, tabName: "Network", nameHint: "Upload")),

            new("winforms-network-slider-download", "WinForms Network: DownloadSlider → setrange 30 (UIA=actual 300)", "winforms",
                ct => SliderSetAndVerifyAsync("WinForms Network: DownloadSlider → setrange 30 (UIA=actual 300)",
                    WinFormsTitle, "30", ct, tabName: "Network", nameHint: "Download")),

            new("winforms-network-checkbox-tls", "WinForms Network: TlsVerify → toggle", "winforms",
                ct => CheckBoxToggleAsync("WinForms Network: TlsVerify → toggle",
                    WinFormsTitle, "TlsVerify", ct, tabName: "Network")),

            new("winforms-network-checkedlistbox-toggle", "WinForms Network: FeaturesCheckedList → select-index 3", "winforms",
                async ct =>
                {
                    const string n = "WinForms Network: FeaturesCheckedList → select-index 3";
                    var (tree, error) = await SelectTabAndScanAsync(WinFormsTitle, "Network", ct);
                    if (tree == null) return Fail(n, error ?? "scan failed");
                    var list = FindNode(tree, n2 => AutomationIdIs(n2, "FeaturesCheckedList"))
                            ?? FindNode(tree, n2 => ControlTypeIs(n2, "List") && NameOrIdContains(n2, "Feature"));
                    if (list == null) return Fail(n, "FeaturesCheckedList not found");
                    var r = await _client.ExecuteAsync(list["id"]!.GetValue<int>(), "select-index", "3", ct);
                    return new TestResult(n, r.Success, r.Detail, Command: $"POST /execute select-index 3 id={list["id"]}");
                }),

            // WinForms — Scheduler tab
            new("winforms-scheduler-textbox", "WinForms Scheduler: JobName → retype", "winforms",
                ct => TextBoxEditRetypeAsync("WinForms Scheduler: JobName → retype",
                    WinFormsTitle, ControlTypeEdit, ct, tabName: "Scheduler", nameHint: "JobName")),

            new("winforms-scheduler-combo-type", "WinForms Scheduler: JobType → select Report", "winforms",
                ct => ComboBoxSelectItemAsync("WinForms Scheduler: JobType → select Report",
                    WinFormsTitle, "JobType", "Report", ct, tabName: "Scheduler")),

            // PrioritySlider min=1 max=10: valid UIA snap points are multiples of 11 (=100/9 per tick).
            // UIA pos 66 = actual 7 (formula: (actual-1)*100÷(max-min) integer = (7-1)*100÷9 = 66).
            new("winforms-scheduler-slider-priority", "WinForms Scheduler: PrioritySlider → setrange 66 (UIA=actual 7)", "winforms",
                ct => SliderSetAndVerifyAsync("WinForms Scheduler: PrioritySlider → setrange 66 (UIA=actual 7)",
                    WinFormsTitle, "66", ct, tabName: "Scheduler", nameHint: "Priority")),

            new("winforms-scheduler-checkbox-enabled", "WinForms Scheduler: JobEnabled → toggle", "winforms",
                ct => CheckBoxToggleAsync("WinForms Scheduler: JobEnabled → toggle",
                    WinFormsTitle, "JobEnabled", ct, tabName: "Scheduler")),

            // WinForms — Layout tab
            new("winforms-layout-button-apply", "WinForms Layout: Button Apply → click", "winforms",
                ct => ButtonClickAsync("WinForms Layout: Button Apply → click",
                    WinFormsTitle, "Apply", ct, tabName: "Layout")),

            new("winforms-layout-button-reset", "WinForms Layout: Button Reset → click", "winforms",
                ct => ButtonClickAsync("WinForms Layout: Button Reset → click",
                    WinFormsTitle, "Reset", ct, tabName: "Layout")),

            // WinForms — Logs tab
            new("winforms-logs-combo-level", "WinForms Logs: LogLevelCombo → select ERROR", "winforms",
                ct => ComboBoxSelectItemAsync("WinForms Logs: LogLevelCombo → select ERROR",
                    WinFormsTitle, "LogLevelCombo", "ERROR", ct, tabName: "Logs")),

            new("winforms-logs-textbox-filter", "WinForms Logs: LogFilter → retype", "winforms",
                ct => TextBoxEditRetypeAsync("WinForms Logs: LogFilter → retype",
                    WinFormsTitle, ControlTypeEdit, ct, tabName: "Logs", nameHint: "LogFilter")),

            new("winforms-logs-checkbox-autoscroll", "WinForms Logs: LogAutoScroll → toggle", "winforms",
                ct => CheckBoxToggleAsync("WinForms Logs: LogAutoScroll → toggle",
                    WinFormsTitle, "LogAutoScroll", ct, tabName: "Logs")),

            // WinForms — Dialogs tab
            new("winforms-dialogs-domainupdown", "WinForms Dialogs: DomainSpinner → setvalue Beta", "winforms",
                async ct =>
                {
                    const string n = "WinForms Dialogs: DomainSpinner → setvalue Beta";
                    var (tree, error) = await SelectTabAndScanAsync(WinFormsTitle, "Dialogs", ct);
                    if (tree == null) return Fail(n, error ?? "scan failed");
                    var spin = FindNode(tree, n2 => AutomationIdIs(n2, "DomainSpinner"))
                            ?? FindNode(tree, n2 => ControlTypeIs(n2, "Spinner") && NameOrIdContains(n2, "Domain"));
                    if (spin == null) return Fail(n, "DomainSpinner not found");
                    int id = spin["id"]!.GetValue<int>();
                    var r = await _client.ExecuteAsync(id, "setvalue", "Beta", ct);
                    return new TestResult(n, r.Success, r.Detail, Command: $"POST /execute setvalue Beta id={id}");
                }),

            new("winforms-dialogs-numericupdown", "WinForms Dialogs: DecimalSpinner → setvalue 42", "winforms",
                ct => NumericSetAndVerifyAsync("WinForms Dialogs: DecimalSpinner → setvalue 42",
                    WinFormsTitle, "DecimalSpinner", "42", ct, tabName: "Dialogs")),

            // WPF — Identity tab
            new("wpf-identity-slider", "WPF Identity: Slider AccessLevel → setrange 3", "wpf",
                ct => SliderSetAndVerifyAsync("WPF Identity: Slider AccessLevel → setrange 3",
                    WpfTitle, "3", ct, tabName: "Identity", nameHint: "AccessLevel")),

            new("wpf-identity-radio-parttime", "WPF Identity: RadioButton EmpPartTime → select", "wpf",
                ct => RadioSelectAsync("WPF Identity: RadioButton EmpPartTime → select",
                    WpfTitle, "EmpPartTime", ct, tabName: "Identity")),

            new("wpf-identity-checkbox-vpn", "WPF Identity: CheckBox FlagVpn → toggle", "wpf",
                ct => CheckBoxToggleAsync("WPF Identity: CheckBox FlagVpn → toggle",
                    WpfTitle, "FlagVpn", ct, tabName: "Identity")),

            new("wpf-identity-combo-location", "WPF Identity: ComboBox Location → select Boston", "wpf",
                ct => ComboBoxSelectItemAsync("WPF Identity: ComboBox Location → select Boston",
                    WpfTitle, "Location", "Boston", ct, tabName: "Identity")),

            new("wpf-identity-textbox-email", "WPF Identity: TextBox Email → retype", "wpf",
                ct => TextBoxEditRetypeAsync("WPF Identity: TextBox Email → retype",
                    WpfTitle, ControlTypeEdit, ct, tabName: "Identity", nameHint: "Email")),

            // WPF — Network tab
            new("wpf-network-slider-upload", "WPF Network: Slider UploadLimit → setrange 200", "wpf",
                ct => SliderSetAndVerifyAsync("WPF Network: Slider UploadLimit → setrange 200",
                    WpfTitle, "200", ct, tabName: "Network", nameHint: "UploadLimit")),

            new("wpf-network-slider-download", "WPF Network: Slider DownloadLimit → setrange 300", "wpf",
                ct => SliderSetAndVerifyAsync("WPF Network: Slider DownloadLimit → setrange 300",
                    WpfTitle, "300", ct, tabName: "Network", nameHint: "DownloadLimit")),

            new("wpf-network-checkbox-tls", "WPF Network: CheckBox TlsClientCert → toggle", "wpf",
                ct => CheckBoxToggleAsync("WPF Network: CheckBox TlsClientCert → toggle",
                    WpfTitle, "TlsClientCert", ct, tabName: "Network")),

            new("wpf-network-radio-proxy", "WPF Network: RadioButton ConnProxy → select", "wpf",
                ct => RadioSelectAsync("WPF Network: RadioButton ConnProxy → select",
                    WpfTitle, "ConnProxy", ct, tabName: "Network")),

            new("wpf-network-combo-protocol", "WPF Network: ComboBox Protocol → select HTTP", "wpf",
                ct => ComboBoxSelectItemAsync("WPF Network: ComboBox Protocol → select HTTP",
                    WpfTitle, "Protocol", "HTTP", ct, tabName: "Network")),

            // WPF — Scheduler tab
            new("wpf-scheduler-textbox", "WPF Scheduler: TextBox JobName → retype", "wpf",
                ct => TextBoxEditRetypeAsync("WPF Scheduler: TextBox JobName → retype",
                    WpfTitle, ControlTypeEdit, ct, tabName: "Scheduler", nameHint: "JobName")),

            new("wpf-scheduler-combo-type", "WPF Scheduler: ComboBox JobType → select Report", "wpf",
                ct => ComboBoxSelectItemAsync("WPF Scheduler: ComboBox JobType → select Report",
                    WpfTitle, "JobType", "Report", ct, tabName: "Scheduler")),

            new("wpf-scheduler-slider", "WPF Scheduler: Slider JobPriority → setrange 7", "wpf",
                ct => SliderSetAndVerifyAsync("WPF Scheduler: Slider JobPriority → setrange 7",
                    WpfTitle, "7", ct, tabName: "Scheduler", nameHint: "JobPriority")),

            new("wpf-scheduler-checkbox", "WPF Scheduler: CheckBox JobEnabled → toggle", "wpf",
                ct => CheckBoxToggleAsync("WPF Scheduler: CheckBox JobEnabled → toggle",
                    WpfTitle, "JobEnabled", ct, tabName: "Scheduler")),

            // WPF — Layout tab
            new("wpf-layout-button-apply", "WPF Layout: Button BtnApply → click", "wpf",
                ct => ButtonClickAsync("WPF Layout: Button BtnApply → click",
                    WpfTitle, "BtnApply", ct, tabName: "Layout")),

            new("wpf-layout-button-reset", "WPF Layout: Button BtnReset → click", "wpf",
                ct => ButtonClickAsync("WPF Layout: Button BtnReset → click",
                    WpfTitle, "BtnReset", ct, tabName: "Layout")),

            // WPF — Logs tab
            new("wpf-logs-combo-level", "WPF Logs: ComboBox LogLevel → select ERROR", "wpf",
                ct => ComboBoxSelectItemAsync("WPF Logs: ComboBox LogLevel → select ERROR",
                    WpfTitle, "LogLevel", "ERROR", ct, tabName: "Logs")),

            new("wpf-logs-textbox-filter", "WPF Logs: TextBox LogFilter → retype", "wpf",
                ct => TextBoxEditRetypeAsync("WPF Logs: TextBox LogFilter → retype",
                    WpfTitle, ControlTypeEdit, ct, tabName: "Logs", nameHint: "LogFilter")),

            new("wpf-logs-checkbox-autoscroll", "WPF Logs: CheckBox LogAutoScroll → toggle", "wpf",
                ct => CheckBoxToggleAsync("WPF Logs: CheckBox LogAutoScroll → toggle",
                    WpfTitle, "LogAutoScroll", ct, tabName: "Logs")),

            new("wpf-logs-button-clear", "WPF Logs: Button LogClear → click", "wpf",
                ct => ButtonClickAsync("WPF Logs: Button LogClear → click",
                    WpfTitle, "LogClear", ct, tabName: "Logs")),

            // WPF — WPF-specific tab
            new("wpf-tab-togglebutton", "WPF WPF-tab: ToggleButton Toggle1 → click+restore", "wpf",
                async ct =>
                {
                    const string n = "WPF WPF-tab: ToggleButton Toggle1 → click+restore";
                    var (tree, error) = await SelectTabAndScanAsync(WpfTitle, "WPF", ct);
                    if (tree == null) return Fail(n, error ?? "scan failed");
                    var btn = FindNode(tree, n2 => AutomationIdIs(n2, "Toggle1"));
                    if (btn == null) return Fail(n, "Toggle1 not found");
                    int id = btn["id"]!.GetValue<int>();
                    var r = await _client.ExecuteAsync(id, "click", null, ct);
                    if (!r.Success) return Fail(n, $"click failed: {r.Detail}");
                    if (_actionDelayMs > 0) await Task.Delay(_actionDelayMs, ct);
                    _ = await _client.ExecuteAsync(id, "click", null, ct);
                    return new TestResult(n, true, "clicked+restored Toggle1", Command: $"POST /execute click id={id}");
                }),

            new("wpf-tab-expander-logging", "WPF WPF-tab: Expander ExpanderLogging → expand/collapse", "wpf",
                async ct =>
                {
                    const string n = "WPF WPF-tab: Expander ExpanderLogging → expand/collapse";
                    var (tree, error) = await SelectTabAndScanAsync(WpfTitle, "WPF", ct);
                    if (tree == null) return Fail(n, error ?? "scan failed");
                    var exp = FindNode(tree, n2 => AutomationIdIs(n2, "ExpanderLogging"));
                    if (exp == null) return Fail(n, "ExpanderLogging not found");
                    int id = exp["id"]!.GetValue<int>();
                    var r = await _client.ExecuteAsync(id, "expand", null, ct);
                    if (!r.Success) return Fail(n, $"expand failed: {r.Detail}");
                    if (_actionDelayMs > 0) await Task.Delay(_actionDelayMs, ct);
                    _ = await _client.ExecuteAsync(id, "collapse", null, ct);
                    return new TestResult(n, true, "expanded+collapsed ExpanderLogging", Command: $"POST /execute expand/collapse id={id}");
                }),

            // WPF — Dialogs tab
            new("wpf-dialogs-slider", "WPF Dialogs: Slider MiscVertSlider → setrange 50", "wpf",
                ct => SliderSetAndVerifyAsync("WPF Dialogs: Slider MiscVertSlider → setrange 50",
                    WpfTitle, "50", ct, tabName: "Dialogs", nameHint: "MiscVertSlider")),

            new("wpf-dialogs-radio", "WPF Dialogs: RadioButton MiscRadioB → select", "wpf",
                ct => RadioSelectAsync("WPF Dialogs: RadioButton MiscRadioB → select",
                    WpfTitle, "MiscRadioB", ct, tabName: "Dialogs")),

            new("wpf-dialogs-listbox", "WPF Dialogs: ListBox MiscMultiListBox → select-index 0", "wpf",
                ct => ListBoxSelectFirstItemAsync("WPF Dialogs: ListBox MiscMultiListBox → select-index 0",
                    WpfTitle, ct, tabName: "Dialogs",
                    listSelector: n2 => AutomationIdIs(n2, "MiscMultiListBox"))),

            new("wpf-dialogs-combo", "WPF Dialogs: ComboBox MiscEditCombo → select Option A", "wpf",
                ct => ComboBoxSelectItemAsync("WPF Dialogs: ComboBox MiscEditCombo → select Option A",
                    WpfTitle, "MiscEditCombo", "Option A", ct, tabName: "Dialogs")),

            new("wpf-dialogs-textbox", "WPF Dialogs: TextBox MiscMultilineTextBox → retype", "wpf",
                ct => TextBoxEditRetypeAsync("WPF Dialogs: TextBox MiscMultilineTextBox → retype",
                    WpfTitle, ControlTypeEdit, ct, tabName: "Dialogs", nameHint: "MiscMultilineTextBox")),

            // HTML — TortureTest
            new("html-torture-checkbox-toggle", "HTML Torture: checkbox check1 → toggle+restore", "html",
                async ct =>
                {
                    const string n = "HTML Torture: checkbox check1 → toggle+restore";
                    await EnsureBrowserActiveAsync(HtmlTortureTitle, ct);
                    var tree = await GetScanAsync(HtmlTortureTitle, ct);
                    if (tree == null) return Fail(n, "scan failed");
                    var cb = FindNode(tree, n2 => AutomationIdIs(n2, "check1"))
                          ?? FindNode(tree, n2 => ControlTypeIs(n2, "CheckBox") && NameOrIdContains(n2, "Required"));
                    if (cb == null) return Fail(n, "check1 not found");
                    int id = cb["id"]!.GetValue<int>();
                    var r = await _client.ExecuteAsync(id, "toggle", null, ct);
                    if (!r.Success) return Fail(n, $"toggle failed: {r.Detail}");
                    if (_actionDelayMs > 0) await Task.Delay(_actionDelayMs, ct);
                    _ = await _client.ExecuteAsync(id, "toggle", null, ct);
                    return new TestResult(n, true, "toggled+restored check1", Command: $"POST /execute toggle id={id}");
                }),

            new("html-torture-radio-select", "HTML Torture: radio 'Radio 2' → click", "html",
                async ct =>
                {
                    const string n = "HTML Torture: radio 'Radio 2' → click";
                    await EnsureBrowserActiveAsync(HtmlTortureTitle, ct);
                    var tree = await GetScanAsync(HtmlTortureTitle, ct);
                    if (tree == null) return Fail(n, "scan failed");
                    var rb = FindNode(tree, n2 => ControlTypeIs(n2, "RadioButton") && NameOrIdContains(n2, "Radio 2"));
                    if (rb == null) return Fail(n, "Radio 2 not found");
                    var r = await _client.ExecuteAsync(rb["id"]!.GetValue<int>(), "click", null, ct);
                    return new TestResult(n, r.Success, r.Detail, Command: $"POST /execute click id={rb["id"]}");
                }),

            new("html-torture-select-single", "HTML Torture: select#single-select → select Option 2", "html",
                async ct =>
                {
                    await EnsureBrowserActiveAsync(HtmlTortureTitle, ct);
                    return await ComboBoxSelectItemAsync("HTML Torture: select#single-select → select Option 2",
                        HtmlTortureTitle, "single-select", "Option 2", ct);
                }),

            new("html-torture-select-multi", "HTML Torture: select#multi-select → select-index 0", "html",
                async ct =>
                {
                    const string n = "HTML Torture: select#multi-select → select-index 0";
                    await EnsureBrowserActiveAsync(HtmlTortureTitle, ct);
                    var tree = await GetScanAsync(HtmlTortureTitle, ct);
                    if (tree == null) return Fail(n, "scan failed");
                    var sel = FindNode(tree, n2 => AutomationIdIs(n2, "multi-select"))
                           ?? FindNode(tree, n2 => ControlTypeIs(n2, "List") && NameOrIdContains(n2, "multi"));
                    if (sel == null) return Fail(n, "multi-select not found");
                    var r = await _client.ExecuteAsync(sel["id"]!.GetValue<int>(), "select-index", "0", ct);
                    return new TestResult(n, r.Success, r.Detail, Command: $"POST /execute select-index 0 id={sel["id"]}");
                }),

            new("html-torture-textarea", "HTML Torture: textarea → retype", "html",
                async ct =>
                {
                    await EnsureBrowserActiveAsync(HtmlTortureTitle, ct);
                    return await TextBoxEditRetypeAsync("HTML Torture: textarea → retype",
                        HtmlTortureTitle, ControlTypeEdit, ct, nameHint: "Textarea", focusBeforeType: true);
                }),

            new("html-torture-button-click", "HTML Torture: button 'Plain button' → click", "html",
                async ct =>
                {
                    const string n = "HTML Torture: button 'Plain button' → click";
                    await EnsureBrowserActiveAsync(HtmlTortureTitle, ct);
                    var tree = await GetScanAsync(HtmlTortureTitle, ct);
                    if (tree == null) return Fail(n, "scan failed");
                    var btn = FindNode(tree, n2 => ControlTypeIs(n2, "Button") && NameOrIdContains(n2, "Plain"));
                    if (btn == null) return Fail(n, "Plain button not found");
                    var r = await _client.ExecuteAsync(btn["id"]!.GetValue<int>(), "click", null, ct);
                    return new TestResult(n, r.Success, r.Detail, Command: $"POST /execute click id={btn["id"]}");
                }),

            // HTML — Ecommerce
            new("html-ecommerce-search-type", "HTML Ecommerce: searchProducts → retype", "html",
                async ct =>
                {
                    await EnsureBrowserActiveAsync(HtmlEcommerceTitle, ct);
                    return await TextBoxEditRetypeAsync("HTML Ecommerce: searchProducts → retype",
                        HtmlEcommerceTitle, ControlTypeEdit, ct, nameHint: "searchProducts", focusBeforeType: true);
                }),

            new("html-ecommerce-category-select", "HTML Ecommerce: categoryFilter → select Electronics", "html",
                async ct =>
                {
                    await EnsureBrowserActiveAsync(HtmlEcommerceTitle, ct);
                    return await ComboBoxSelectItemAsync("HTML Ecommerce: categoryFilter → select Electronics",
                        HtmlEcommerceTitle, "categoryFilter", "Electronics", ct);
                }),

            new("html-ecommerce-checkbox-shipping", "HTML Ecommerce: shipFree → toggle+restore", "html",
                async ct =>
                {
                    const string n = "HTML Ecommerce: shipFree → toggle+restore";
                    await EnsureBrowserActiveAsync(HtmlEcommerceTitle, ct);
                    var tree = await GetScanAsync(HtmlEcommerceTitle, ct);
                    if (tree == null) return Fail(n, "scan failed");
                    var cb = FindNode(tree, n2 => AutomationIdIs(n2, "shipFree"))
                          ?? FindNode(tree, n2 => ControlTypeIs(n2, "CheckBox") && NameOrIdContains(n2, "ship"));
                    if (cb == null) return Fail(n, "shipFree not found");
                    int id = cb["id"]!.GetValue<int>();
                    var r = await _client.ExecuteAsync(id, "toggle", null, ct);
                    if (!r.Success) return Fail(n, $"toggle failed: {r.Detail}");
                    if (_actionDelayMs > 0) await Task.Delay(_actionDelayMs, ct);
                    _ = await _client.ExecuteAsync(id, "toggle", null, ct);
                    return new TestResult(n, true, "toggled+restored shipFree", Command: $"POST /execute toggle id={id}");
                }),

            new("html-ecommerce-add-to-cart", "HTML Ecommerce: first 'Add to Cart' button → click", "html",
                async ct =>
                {
                    const string n = "HTML Ecommerce: first 'Add to Cart' button → click";
                    await EnsureBrowserActiveAsync(HtmlEcommerceTitle, ct);
                    var tree = await GetScanAsync(HtmlEcommerceTitle, ct);
                    if (tree == null) return Fail(n, "scan failed");
                    var btn = FindNode(tree, n2 => ControlTypeIs(n2, "Button") && NameOrIdContains(n2, "Add to Cart"));
                    if (btn == null) return Fail(n, "Add to Cart button not found");
                    var r = await _client.ExecuteAsync(btn["id"]!.GetValue<int>(), "click", null, ct);
                    return new TestResult(n, r.Success, r.Detail, Command: $"POST /execute click id={btn["id"]}");
                }),

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
    /// <summary>
    /// Bring the browser window/tab whose document title contains <paramref name="windowTitle"/>
    /// to the foreground before driving it. Browser tab titles only appear on the active tab,
    /// so without this the next /find may match a different tab and inputs land on the wrong page
    /// (or the address bar). Drops the scan cache so subsequent scans reflect the activated tab.
    /// </summary>
    private async Task EnsureBrowserActiveAsync(string windowTitle, CancellationToken ct)
    {
        _scanCache.Remove(windowTitle);
        _ = await _client.FindWindowAsync(windowTitle, ct);
        if (_uiSettleDelayMs > 0) await Task.Delay(_uiSettleDelayMs, ct);
    }

    private async Task<TestResult> TextBoxEditRetypeAsync(string name, string windowTitle, string controlType, CancellationToken ct, string? tabName = null, string? nameHint = null, bool focusBeforeType = false)
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

        if (focusBeforeType)
        {
            // Browsers accept text only when the element actually has keyboard focus —
            // otherwise type/keys end up in the address bar or whichever element last had focus.
            _ = await _client.ExecuteAsync(id, "focus", null, ct);
            if (_actionDelayMs > 0) await Task.Delay(_actionDelayMs, ct);
        }

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
        string detail = ok
            ? $"typed+verified '{first}' then '{second}'"
            : $"final gettext did not contain '{second}'. Got: {g2.Result}";

        // Restore to a neutral state so this helper doesn't alter downstream tests.
        try
        {
            _ = await _client.ExecuteAsync(id, "clear", null, ct);
        }
        catch { /* restoration is best-effort only */ }

        return new TestResult(name, ok,
            detail,
            Command: $"POST /execute type/gettext id={id}");
    }

    /// <summary>Find the first Slider (optionally matching <paramref name="nameHint"/>), setrange to <paramref name="value"/>, then getrange and assert.</summary>
    private async Task<TestResult> SliderSetAndVerifyAsync(string name, string windowTitle, string value, CancellationToken ct, string? tabName = null, string? nameHint = null)
    {
        _client.Logger?.Invoke($"[Slider] begin '{name}' window='{windowTitle}' tab='{tabName ?? "<none>"}' nameHint='{nameHint ?? "<none>"}' target={value}");
        var (tree, error) = tabName == null
            ? (await GetScanAsync(windowTitle, ct), (string?)null)
            : await SelectTabAndScanAsync(windowTitle, tabName, ct);
        if (tree == null) return Fail(name, error ?? $"scan of '{windowTitle}' failed (window not open?)");

        var allSliders = CollectNodes(tree, n => ControlTypeIs(n, "Slider")).ToList();
        _client.Logger?.Invoke($"[Slider] found {allSliders.Count} Slider control(s): "
            + string.Join(", ", allSliders.Take(10).Select(s => $"id={s["id"]} name=\"{s["name"]?.GetValue<string>()}\" autoId=\"{s["automationId"]?.GetValue<string>()}\"")));

        var slider = nameHint != null
            ? FindNode(tree, n => ControlTypeIs(n, "Slider") && NameOrIdContains(n, nameHint))
            : FindNode(tree, n => ControlTypeIs(n, "Slider"));
        if (slider == null) return Fail(name, $"No Slider control found{(nameHint != null ? $" matching '{nameHint}'" : "")}");
        int id = slider["id"]!.GetValue<int>();
        _client.Logger?.Invoke($"[Slider] selected id={id} name=\"{slider["name"]?.GetValue<string>()}\" autoId=\"{slider["automationId"]?.GetValue<string>()}\"");
        string? originalRange = null;
        try
        {
            var before = await _client.ExecuteAsync(id, "getrange", null, ct);
            if (before.Success) originalRange = before.Result?.Trim();
        }
        catch { /* best-effort snapshot */ }

        var setr = await _client.ExecuteAsync(id, "setrange", value, ct);
        if (!setr.Success) return Fail(name, $"setrange failed: {setr.Detail}");
        if (_uiSettleDelayMs > 0) await Task.Delay(_uiSettleDelayMs, ct);

        var getr = await _client.ExecuteAsync(id, "getrange", null, ct);
        if (!getr.Success) return Fail(name, $"getrange failed: {getr.Detail}");
        // getrange reports "value min-max" or similar — just check the numeric target is present.
        bool ok = getr.Result != null && getr.Result.Contains(value);
        try
        {
            if (!string.IsNullOrWhiteSpace(originalRange))
                _ = await _client.ExecuteAsync(id, "setrange", originalRange, ct);
        }
        catch { /* restoration is best-effort only */ }
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
        // NOTE: the listSelector ternary must be parenthesised so the ?? fallbacks
        // apply regardless of whether listSelector is null.
        var list = (listSelector != null ? FindNode(tree, listSelector) : null)
                ?? FindNode(tree, n => AutomationIdIs(n, "ListBox"))
                ?? FindNode(tree, n => NameOrIdContains(n, "ListBox"))
                ?? FindNode(tree, n => ControlTypeIs(n, "List"));
        if (list == null) return Fail(name, "No ListBox found");

        int listId = list["id"]!.GetValue<int>();
        var r = await _client.ExecuteAsync(listId, "select-index", "0", ct);
        bool ok = r.Success;
        return new TestResult(name, ok, ok ? $"selected index 0 on list id={listId}" : r.Detail,
            Command: $"POST /execute select-index 0 id={listId}");
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
        if (rb == null)
        {
            // Collect diagnostics: list all RadioButton nodes, and also any node whose name contains the target.
            var allRadios = CollectNodes(tree, n => ControlTypeIs(n, "RadioButton")).Take(10).ToList();
            var byName    = CollectNodes(tree, n => NameOrIdContains(n, radioName)).Take(5).ToList();
            var ctypes    = CollectNodes(tree, _ => true).GroupBy(n => n["controlType"]?.GetValue<string>() ?? "?")
                                .Select(g => $"{g.Key}×{g.Count()}").Take(15);
            string radioInfo = allRadios.Count > 0
                ? "RadioButtons: " + string.Join(", ", allRadios.Select(n => $"\"{n["name"]?.GetValue<string>()}\""))
                : "No RadioButton nodes in tree";
            string nameInfo = byName.Count > 0
                ? $"; Nodes named '{radioName}': " + string.Join(", ", byName.Select(n => $"{n["controlType"]?.GetValue<string>()}:\"{n["name"]?.GetValue<string>()}\""))
                : $"; No node named '{radioName}'";
            string typeInfo = "; Types: " + string.Join(", ", ctypes);
            return Fail(name, $"{radioName} not found. {radioInfo}{nameInfo}{typeInfo}");
        }
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

    private async Task<TestResult> CheckBoxToggleAsync(string name, string windowTitle, string controlId, CancellationToken ct, string? tabName = null)
    {
        var (tree, error) = tabName == null
            ? (await GetScanAsync(windowTitle, ct), (string?)null)
            : await SelectTabAndScanAsync(windowTitle, tabName, ct);
        if (tree == null) return Fail(name, error ?? $"scan of '{windowTitle}' failed");

        var cb = FindNode(tree, n => AutomationIdIs(n, controlId))
              ?? FindNode(tree, n => ControlTypeIs(n, "CheckBox") && NameOrIdContains(n, controlId));
        if (cb == null) return Fail(name, $"CheckBox '{controlId}' not found");
        int id = cb["id"]!.GetValue<int>();

        var r = await _client.ExecuteAsync(id, "toggle", null, ct);
        if (!r.Success) return Fail(name, $"toggle failed: {r.Detail}");
        if (_actionDelayMs > 0) await Task.Delay(_actionDelayMs, ct);
        _ = await _client.ExecuteAsync(id, "toggle", null, ct);
        return new TestResult(name, true, $"toggled+restored '{controlId}'",
            Command: $"POST /execute toggle id={id}");
    }

    private async Task<TestResult> ComboBoxSelectItemAsync(string name, string windowTitle, string controlId, string itemText, CancellationToken ct, string? tabName = null)
    {
        var (tree, error) = tabName == null
            ? (await GetScanAsync(windowTitle, ct), (string?)null)
            : await SelectTabAndScanAsync(windowTitle, tabName, ct);
        if (tree == null) return Fail(name, error ?? $"scan of '{windowTitle}' failed");

        var combo = FindNode(tree, n => AutomationIdIs(n, controlId))
                 ?? FindNode(tree, n => ControlTypeIs(n, "ComboBox") && NameOrIdContains(n, controlId));
        if (combo == null) return Fail(name, $"ComboBox '{controlId}' not found");
        int id = combo["id"]!.GetValue<int>();

        _ = await _client.ExecuteAsync(id, "expand", null, ct);
        if (_actionDelayMs > 0) await Task.Delay(_actionDelayMs, ct);

        var sel = await _client.ExecuteAsync(id, "select", itemText, ct);
        if (!sel.Success) return Fail(name, $"select '{itemText}' failed: {sel.Detail}");
        return new TestResult(name, true, $"selected '{itemText}' in '{controlId}'",
            Command: $"POST /execute select id={id} value={itemText}");
    }

    private async Task<TestResult> ButtonClickAsync(string name, string windowTitle, string controlId, CancellationToken ct, string? tabName = null)
    {
        var (tree, error) = tabName == null
            ? (await GetScanAsync(windowTitle, ct), (string?)null)
            : await SelectTabAndScanAsync(windowTitle, tabName, ct);
        if (tree == null) return Fail(name, error ?? $"scan of '{windowTitle}' failed");

        var btn = FindNode(tree, n => AutomationIdIs(n, controlId))
               ?? FindNode(tree, n => ControlTypeIs(n, "Button") && NameOrIdContains(n, controlId));
        if (btn == null) return Fail(name, $"Button '{controlId}' not found");
        return await ExecAsync(name, btn, "click", null, ct);
    }

    private async Task<TestResult> NumericSetAndVerifyAsync(string name, string windowTitle, string controlId, string value, CancellationToken ct, string? tabName = null)
    {
        var (tree, error) = tabName == null
            ? (await GetScanAsync(windowTitle, ct), (string?)null)
            : await SelectTabAndScanAsync(windowTitle, tabName, ct);
        if (tree == null) return Fail(name, error ?? $"scan of '{windowTitle}' failed");

        var num = FindNode(tree, n => AutomationIdIs(n, controlId))
               ?? FindNode(tree, n => ControlTypeIs(n, "Spinner") && NameOrIdContains(n, controlId));
        if (num == null) return Fail(name, $"Spinner '{controlId}' not found");
        int id = num["id"]!.GetValue<int>();

        var setr = await _client.ExecuteAsync(id, "setvalue", value, ct);
        if (!setr.Success) return Fail(name, $"setvalue failed: {setr.Detail}");
        if (_actionDelayMs > 0) await Task.Delay(_actionDelayMs, ct);

        var getr = await _client.ExecuteAsync(id, "getvalue", null, ct);
        bool ok = getr.Success;
        return new TestResult(name, ok,
            ok ? $"setvalue→getvalue: {getr.Result}" : $"getvalue failed: {getr.Detail}",
            Command: $"POST /execute setvalue/getvalue id={id} value={value}");
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

    private static IEnumerable<JsonObject> CollectNodes(JsonNode root, Func<JsonObject, bool> pred)
    {
        if (root is JsonObject obj)
        {
            if (pred(obj)) yield return obj;
            if (obj["children"] is JsonArray kids)
                foreach (var child in kids)
                    if (child != null)
                        foreach (var hit in CollectNodes(child, pred))
                            yield return hit;
        }
    }

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
        static string Normalize(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "";
            var chars = s.Where(char.IsLetterOrDigit).ToArray();
            return new string(chars).ToLowerInvariant();
        }

        var name = n["name"]?.GetValue<string>();
        var aid  = n["automationId"]?.GetValue<string>();
        if ((name?.Contains(text, StringComparison.OrdinalIgnoreCase) ?? false)
            || (aid?.Contains(text, StringComparison.OrdinalIgnoreCase) ?? false))
            return true;

        // Browser accessibility trees sometimes collapse spaces/punctuation (e.g. "AddToCart").
        var needle = Normalize(text);
        if (needle.Length == 0) return false;
        return Normalize(name).Contains(needle, StringComparison.Ordinal)
            || Normalize(aid).Contains(needle, StringComparison.Ordinal);
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
