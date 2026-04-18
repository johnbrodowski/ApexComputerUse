namespace ApexUIBridge.ElementTester;

/// <summary>
/// Per-element-type test methods. Each method finds instances of its element type
/// in the scan data and exercises every applicable interaction.
/// </summary>
public static class ElementTests
{
    // ── Registry of all element test types ──────────────────────────────────────
    public static readonly Dictionary<string, Func<TestContext, string, string, CancellationToken, Task>> All = new(StringComparer.OrdinalIgnoreCase)
    {
        ["TextBox"]      = TestTextBox,
        ["CheckBox"]     = TestCheckBox,
        ["RadioButton"]  = TestRadioButton,
        ["Button"]       = TestButton,
        ["ComboBox"]     = TestComboBox,
        ["Slider"]       = TestSlider,
        ["ProgressBar"]  = TestProgressBar,
        ["TabItem"]      = TestTabItem,
        ["Expander"]     = TestExpander,
        ["ToggleButton"] = TestToggleButton,
        ["TreeView"]     = TestTreeView,
        ["Menu"]         = TestMenu,
        ["RichTextBox"]  = TestRichTextBox,
        ["PasswordBox"]  = TestPasswordBox,
        ["DatePicker"]   = TestDatePicker,
        ["ScrollBar"]    = TestScrollBar,
        ["ListBox"]      = TestListBox,
        ["DataGrid"]     = TestDataGrid,
        ["ListView"]     = TestListView,
    };

    // ── TextBox ─────────────────────────────────────────────────────────────────
    static async Task TestTextBox(TestContext ctx, string windowTitle, string scanData, CancellationToken ct)
    {
        var elements = ScanHelper.FindByType(scanData, "TextBox")
            .Concat(ScanHelper.FindByType(scanData, "Edit"))
            .DistinctBy(e => e.Id)
            .Take(5).ToList(); // limit to first 5 to keep output readable

        if (elements.Count == 0) { ctx.Record($"TextBox: no elements found in '{windowTitle}'", false); return; }

        foreach (var el in elements)
        {
            var label = el.AutomationId ?? el.Name ?? $"ID:{el.Id}";
            var prefix = $"TextBox [{label}]";

            // GET_TEXT — read current value
            var getText = await ctx.TestAsync($"{prefix} GET_TEXT", $"GET_TEXT {el.Id}", r => r.Success, ct);

            // TYPE — write a test value
            var testVal = $"et_{DateTime.Now:HHmmss}";
            await ctx.TestAsync($"{prefix} TYPE '{testVal}'", $"TYPE {el.Id} {testVal}", r => r.Success, ct);

            // GET_TEXT — verify the typed value
            await ctx.TestAsync($"{prefix} GET_TEXT after TYPE", $"GET_TEXT {el.Id}",
                r => r.Success && (r.Data?.Contains(testVal) ?? false), ct);

            // SEND_KEYS — append via keyboard simulation
            await ctx.TestAsync($"{prefix} SEND_KEYS ' extra'", $"SEND_KEYS {el.Id} extra", r => r.Success, ct);

            // Restore original value if we had one
            if (getText.Success && getText.Data != null)
                await ctx.Client.SendAsync($"TYPE {el.Id} {getText.Data.Trim()}", ct);
        }
    }

    // ── CheckBox ────────────────────────────────────────────────────────────────
    static async Task TestCheckBox(TestContext ctx, string windowTitle, string scanData, CancellationToken ct)
    {
        var elements = ScanHelper.FindByType(scanData, "CheckBox")
            .Take(6).ToList();

        if (elements.Count == 0) { ctx.Record($"CheckBox: no elements found in '{windowTitle}'", false); return; }

        foreach (var el in elements)
        {
            var label = el.Name ?? el.AutomationId ?? $"ID:{el.Id}";
            var prefix = $"CheckBox [{label}]";

            // TOGGLE — flip state
            await ctx.TestAsync($"{prefix} TOGGLE", $"TOGGLE {el.Id}", r => r.Success, ct);

            // TOGGLE — restore
            await ctx.TestAsync($"{prefix} TOGGLE (restore)", $"TOGGLE {el.Id}", r => r.Success, ct);

            // GET_TEXT — read state
            await ctx.TestAsync($"{prefix} GET_TEXT (read state)", $"GET_TEXT {el.Id}", r => r.Success, ct);
        }
    }

    // ── RadioButton ─────────────────────────────────────────────────────────────
    static async Task TestRadioButton(TestContext ctx, string windowTitle, string scanData, CancellationToken ct)
    {
        var elements = ScanHelper.FindByType(scanData, "RadioButton")
            .Take(4).ToList();

        if (elements.Count == 0) { ctx.Record($"RadioButton: no elements found in '{windowTitle}'", false); return; }

        foreach (var el in elements)
        {
            var label = el.Name ?? el.AutomationId ?? $"ID:{el.Id}";
            var prefix = $"RadioButton [{label}]";

            // CLICK — select this radio (WPF uses InvokePattern)
            await ctx.TestAsync($"{prefix} CLICK", $"CLICK {el.Id}", r => r.Success, ct);

            // TOGGLE — WinForms RadioButton supports TogglePattern
            await ctx.TestAsync($"{prefix} TOGGLE", $"TOGGLE {el.Id}", r => r.Success, ct);

            // GET_TEXT — read label/state
            await ctx.TestAsync($"{prefix} GET_TEXT", $"GET_TEXT {el.Id}", r => r.Success, ct);
        }
    }

    // ── Button ──────────────────────────────────────────────────────────────────
    static async Task TestButton(TestContext ctx, string windowTitle, string scanData, CancellationToken ct)
    {
        // Filter to safe buttons — avoid Exit, Shutdown, Close, etc.
        var excluded = new[] { "exit", "close", "shutdown", "stop", "delete", "remove", "clear" };
        var elements = ScanHelper.FindByType(scanData, "Button")
            .Where(e => !excluded.Any(u => (e.Name ?? "").Contains(u, StringComparison.OrdinalIgnoreCase)))
            .Where(e => !excluded.Any(u => (e.AutomationId ?? "").Contains(u, StringComparison.OrdinalIgnoreCase)))
            .Take(6).ToList();

        if (elements.Count == 0) { ctx.Record($"Button: no safe elements found in '{windowTitle}'", false); return; }

        foreach (var el in elements)
        {
            var label = el.Name ?? el.AutomationId ?? $"ID:{el.Id}";
            var prefix = $"Button [{label}]";

            await ctx.TestAsync($"{prefix} CLICK", $"CLICK {el.Id}", r => r.Success, ct);
            await ctx.TestAsync($"{prefix} GET_TEXT", $"GET_TEXT {el.Id}", r => r.Success, ct);
        }
    }

    // ── ComboBox ────────────────────────────────────────────────────────────────
    static async Task TestComboBox(TestContext ctx, string windowTitle, string scanData, CancellationToken ct)
    {
        var elements = ScanHelper.FindByType(scanData, "ComboBox")
            .Take(4).ToList();

        if (elements.Count == 0) { ctx.Record($"ComboBox: no elements found in '{windowTitle}'", false); return; }

        foreach (var el in elements)
        {
            var label = el.Name ?? el.AutomationId ?? $"ID:{el.Id}";
            var prefix = $"ComboBox [{label}]";

            // GET_TEXT — read current selection
            await ctx.TestAsync($"{prefix} GET_TEXT", $"GET_TEXT {el.Id}", r => r.Success, ct);

            // EXPAND
            await ctx.TestAsync($"{prefix} EXPAND", $"EXPAND {el.Id}", r => r.Success, ct);
            await Task.Delay(300, ct);

            // COLLAPSE
            await ctx.TestAsync($"{prefix} COLLAPSE", $"COLLAPSE {el.Id}", r => r.Success, ct);

            // SET_VALUE — try setting by text (may not be supported on all combos)
            await ctx.TestAsync($"{prefix} SET_VALUE", $"SET_VALUE {el.Id} 0", r => r.Success, ct);
        }
    }

    // ── Slider / TrackBar ───────────────────────────────────────────────────────
    static async Task TestSlider(TestContext ctx, string windowTitle, string scanData, CancellationToken ct)
    {
        var elements = ScanHelper.FindByType(scanData, "Slider")
            .Concat(ScanHelper.FindByType(scanData, "TrackBar"))
            .DistinctBy(e => e.Id)
            .Take(4).ToList();

        if (elements.Count == 0) { ctx.Record($"Slider: no elements found in '{windowTitle}'", false); return; }

        foreach (var el in elements)
        {
            var label = el.AutomationId ?? el.Name ?? $"ID:{el.Id}";
            var prefix = $"Slider [{label}]";

            // GET_TEXT — read current value
            var original = await ctx.TestAsync($"{prefix} GET_TEXT", $"GET_TEXT {el.Id}", r => r.Success, ct);

            // SET_VALUE — set to a known value
            await ctx.TestAsync($"{prefix} SET_VALUE 5", $"SET_VALUE {el.Id} 5", r => r.Success, ct);

            // GET_TEXT — verify the value changed
            await ctx.TestAsync($"{prefix} GET_TEXT after SET_VALUE", $"GET_TEXT {el.Id}", r => r.Success, ct);

            // Restore
            if (original.Success && double.TryParse(original.Data?.Trim(), out var orig))
                await ctx.Client.SendAsync($"SET_VALUE {el.Id} {orig}", ct);
        }
    }

    // ── ProgressBar ─────────────────────────────────────────────────────────────
    static async Task TestProgressBar(TestContext ctx, string windowTitle, string scanData, CancellationToken ct)
    {
        var elements = ScanHelper.FindByType(scanData, "ProgressBar")
            .Take(3).ToList();

        if (elements.Count == 0) { ctx.Record($"ProgressBar: no elements found in '{windowTitle}'", false); return; }

        foreach (var el in elements)
        {
            var label = el.AutomationId ?? el.Name ?? $"ID:{el.Id}";
            var prefix = $"ProgressBar [{label}]";

            // GET_TEXT — read the value (usually numeric)
            await ctx.TestAsync($"{prefix} GET_TEXT", $"GET_TEXT {el.Id}", r => r.Success, ct);
        }
    }

    // ── TabItem / TabPage ───────────────────────────────────────────────────────
    static async Task TestTabItem(TestContext ctx, string windowTitle, string scanData, CancellationToken ct)
    {
        var elements = ScanHelper.FindByType(scanData, "TabItem")
            .Concat(ScanHelper.FindByType(scanData, "Tab "))  // WinForms "Tab" keyword
            .DistinctBy(e => e.Id)
            .Take(6).ToList();

        if (elements.Count == 0) { ctx.Record($"TabItem: no elements found in '{windowTitle}'", false); return; }

        long? firstTabId = elements.FirstOrDefault()?.Id;

        foreach (var el in elements)
        {
            var label = el.Name ?? el.AutomationId ?? $"ID:{el.Id}";
            var prefix = $"TabItem [{label}]";

            // CLICK — switch to this tab
            await ctx.TestAsync($"{prefix} CLICK", $"CLICK {el.Id}", r => r.Success, ct);
            await Task.Delay(300, ct);

            // GET_TEXT — read tab header
            await ctx.TestAsync($"{prefix} GET_TEXT", $"GET_TEXT {el.Id}", r => r.Success, ct);
        }

        // Restore first tab
        if (firstTabId.HasValue)
            await ctx.Client.SendAsync($"CLICK {firstTabId}", ct);
    }

    // ── Expander ────────────────────────────────────────────────────────────────
    static async Task TestExpander(TestContext ctx, string windowTitle, string scanData, CancellationToken ct)
    {
        var elements = ScanHelper.FindByType(scanData, "Expander")
            .Take(4).ToList();

        if (elements.Count == 0) { ctx.Record($"Expander: no elements found in '{windowTitle}'", false); return; }

        foreach (var el in elements)
        {
            var label = el.Name ?? el.AutomationId ?? $"ID:{el.Id}";
            var prefix = $"Expander [{label}]";

            await ctx.TestAsync($"{prefix} EXPAND", $"EXPAND {el.Id}", r => r.Success, ct);
            await Task.Delay(200, ct);
            await ctx.TestAsync($"{prefix} COLLAPSE", $"COLLAPSE {el.Id}", r => r.Success, ct);
            await ctx.TestAsync($"{prefix} GET_TEXT", $"GET_TEXT {el.Id}", r => r.Success, ct);
        }
    }

    // ── ToggleButton ────────────────────────────────────────────────────────────
    static async Task TestToggleButton(TestContext ctx, string windowTitle, string scanData, CancellationToken ct)
    {
        var elements = ScanHelper.FindByType(scanData, "ToggleButton")
            .Take(4).ToList();

        if (elements.Count == 0) { ctx.Record($"ToggleButton: no elements found in '{windowTitle}'", false); return; }

        foreach (var el in elements)
        {
            var label = el.Name ?? el.AutomationId ?? $"ID:{el.Id}";
            var prefix = $"ToggleButton [{label}]";

            await ctx.TestAsync($"{prefix} TOGGLE", $"TOGGLE {el.Id}", r => r.Success, ct);
            await ctx.TestAsync($"{prefix} TOGGLE (restore)", $"TOGGLE {el.Id}", r => r.Success, ct);
            await ctx.TestAsync($"{prefix} CLICK", $"CLICK {el.Id}", r => r.Success, ct);
            // Restore via click again
            await ctx.TestAsync($"{prefix} CLICK (restore)", $"CLICK {el.Id}", r => r.Success, ct);
            await ctx.TestAsync($"{prefix} GET_TEXT", $"GET_TEXT {el.Id}", r => r.Success, ct);
        }
    }

    // ── TreeView / TreeItem ─────────────────────────────────────────────────────
    static async Task TestTreeView(TestContext ctx, string windowTitle, string scanData, CancellationToken ct)
    {
        var elements = ScanHelper.FindByType(scanData, "TreeItem")
            .Take(5).ToList();

        if (elements.Count == 0) { ctx.Record($"TreeView: no tree items found in '{windowTitle}'", false); return; }

        foreach (var el in elements)
        {
            var label = el.Name ?? el.AutomationId ?? $"ID:{el.Id}";
            var prefix = $"TreeItem [{label}]";

            await ctx.TestAsync($"{prefix} EXPAND", $"EXPAND {el.Id}", r => r.Success, ct);
            await ctx.TestAsync($"{prefix} COLLAPSE", $"COLLAPSE {el.Id}", r => r.Success, ct);
            await ctx.TestAsync($"{prefix} CLICK", $"CLICK {el.Id}", r => r.Success, ct);
            await ctx.TestAsync($"{prefix} GET_TEXT", $"GET_TEXT {el.Id}", r => r.Success, ct);
        }
    }

    // ── Menu / MenuItem ─────────────────────────────────────────────────────────
    static async Task TestMenu(TestContext ctx, string windowTitle, string scanData, CancellationToken ct)
    {
        // Only test top-level menu items — avoid triggering destructive actions
        var excluded = new[] { "exit", "close", "quit", "torture" };
        var elements = ScanHelper.FindByType(scanData, "MenuItem")
            .Where(e => !excluded.Any(u => (e.Name ?? "").Contains(u, StringComparison.OrdinalIgnoreCase)))
            .Take(4).ToList();

        if (elements.Count == 0) { ctx.Record($"Menu: no menu items found in '{windowTitle}'", false); return; }

        foreach (var el in elements)
        {
            var label = el.Name ?? el.AutomationId ?? $"ID:{el.Id}";
            var prefix = $"MenuItem [{label}]";

            await ctx.TestAsync($"{prefix} CLICK (open)", $"CLICK {el.Id}", r => r.Success, ct);
            await Task.Delay(400, ct);
            await ctx.TestAsync($"{prefix} GET_TEXT", $"GET_TEXT {el.Id}", r => r.Success, ct);

            // Press Escape to close any open menu
            await ctx.Client.SendAsync($"SEND_KEYS {el.Id} {{ESC}}", ct);
            await Task.Delay(200, ct);
        }
    }

    // ── RichTextBox ─────────────────────────────────────────────────────────────
    static async Task TestRichTextBox(TestContext ctx, string windowTitle, string scanData, CancellationToken ct)
    {
        var elements = ScanHelper.FindByType(scanData, "RichTextBox")
            .Concat(ScanHelper.FindByType(scanData, "RichEdit"))
            .DistinctBy(e => e.Id)
            .Take(3).ToList();

        if (elements.Count == 0) { ctx.Record($"RichTextBox: no elements found in '{windowTitle}'", false); return; }

        foreach (var el in elements)
        {
            var label = el.AutomationId ?? el.Name ?? $"ID:{el.Id}";
            var prefix = $"RichTextBox [{label}]";

            await ctx.TestAsync($"{prefix} GET_TEXT", $"GET_TEXT {el.Id}", r => r.Success, ct);
            await ctx.TestAsync($"{prefix} TYPE 'test line'", $"TYPE {el.Id} test line from ElementTester", r => r.Success, ct);
            await ctx.TestAsync($"{prefix} SEND_KEYS", $"SEND_KEYS {el.Id} hello", r => r.Success, ct);
        }
    }

    // ── PasswordBox ─────────────────────────────────────────────────────────────
    static async Task TestPasswordBox(TestContext ctx, string windowTitle, string scanData, CancellationToken ct)
    {
        var elements = ScanHelper.FindByType(scanData, "PasswordBox")
            .Concat(ScanHelper.FindByType(scanData, "Password"))
            .DistinctBy(e => e.Id)
            .Take(2).ToList();

        if (elements.Count == 0) { ctx.Record($"PasswordBox: no elements found in '{windowTitle}'", false); return; }

        foreach (var el in elements)
        {
            var label = el.AutomationId ?? el.Name ?? $"ID:{el.Id}";
            var prefix = $"PasswordBox [{label}]";

            // TYPE — should work even though value isn't readable
            await ctx.TestAsync($"{prefix} TYPE", $"TYPE {el.Id} secretpass123", r => r.Success, ct);

            // GET_TEXT — PasswordBox typically blocks read access
            await ctx.TestAsync($"{prefix} GET_TEXT (may fail — password protected)", $"GET_TEXT {el.Id}", null, ct);

            // SEND_KEYS
            await ctx.TestAsync($"{prefix} SEND_KEYS", $"SEND_KEYS {el.Id} extra", r => r.Success, ct);
        }
    }

    // ── DatePicker ──────────────────────────────────────────────────────────────
    static async Task TestDatePicker(TestContext ctx, string windowTitle, string scanData, CancellationToken ct)
    {
        var elements = ScanHelper.FindByType(scanData, "DatePicker")
            .Concat(ScanHelper.FindByType(scanData, "DateTimePicker"))
            .DistinctBy(e => e.Id)
            .Take(3).ToList();

        if (elements.Count == 0) { ctx.Record($"DatePicker: no elements found in '{windowTitle}'", false); return; }

        foreach (var el in elements)
        {
            var label = el.AutomationId ?? el.Name ?? $"ID:{el.Id}";
            var prefix = $"DatePicker [{label}]";

            await ctx.TestAsync($"{prefix} GET_TEXT", $"GET_TEXT {el.Id}", r => r.Success, ct);
            await ctx.TestAsync($"{prefix} SET_VALUE '2026-01-15'", $"SET_VALUE {el.Id} 2026-01-15", r => r.Success, ct);
            await ctx.TestAsync($"{prefix} EXPAND (open calendar)", $"EXPAND {el.Id}", r => r.Success, ct);
            await Task.Delay(300, ct);
            await ctx.TestAsync($"{prefix} COLLAPSE", $"COLLAPSE {el.Id}", r => r.Success, ct);
        }
    }

    // ── ScrollBar ───────────────────────────────────────────────────────────────
    static async Task TestScrollBar(TestContext ctx, string windowTitle, string scanData, CancellationToken ct)
    {
        var elements = ScanHelper.FindByType(scanData, "ScrollBar")
            .Take(3).ToList();

        if (elements.Count == 0) { ctx.Record($"ScrollBar: no elements found in '{windowTitle}'", false); return; }

        foreach (var el in elements)
        {
            var label = el.AutomationId ?? el.Name ?? $"ID:{el.Id}";
            var prefix = $"ScrollBar [{label}]";

            await ctx.TestAsync($"{prefix} GET_TEXT", $"GET_TEXT {el.Id}", r => r.Success, ct);
            await ctx.TestAsync($"{prefix} SET_VALUE 50", $"SET_VALUE {el.Id} 50", r => r.Success, ct);
        }
    }

    // ── ListBox ─────────────────────────────────────────────────────────────────
    static async Task TestListBox(TestContext ctx, string windowTitle, string scanData, CancellationToken ct)
    {
        var elements = ScanHelper.FindByType(scanData, "ListBox")
            .Concat(ScanHelper.FindByType(scanData, "ListItem"))
            .DistinctBy(e => e.Id)
            .Take(5).ToList();

        if (elements.Count == 0) { ctx.Record($"ListBox: no elements found in '{windowTitle}'", false); return; }

        foreach (var el in elements)
        {
            var label = el.Name ?? el.AutomationId ?? $"ID:{el.Id}";
            var prefix = $"ListBox [{label}]";

            await ctx.TestAsync($"{prefix} CLICK", $"CLICK {el.Id}", r => r.Success, ct);
            await ctx.TestAsync($"{prefix} GET_TEXT", $"GET_TEXT {el.Id}", r => r.Success, ct);
        }
    }

    // ── DataGrid ────────────────────────────────────────────────────────────────
    static async Task TestDataGrid(TestContext ctx, string windowTitle, string scanData, CancellationToken ct)
    {
        var elements = ScanHelper.FindByType(scanData, "DataGrid")
            .Concat(ScanHelper.FindByType(scanData, "DataItem"))
            .DistinctBy(e => e.Id)
            .Take(4).ToList();

        if (elements.Count == 0) { ctx.Record($"DataGrid: no elements found in '{windowTitle}'", false); return; }

        foreach (var el in elements)
        {
            var label = el.Name ?? el.AutomationId ?? $"ID:{el.Id}";
            var prefix = $"DataGrid [{label}]";

            await ctx.TestAsync($"{prefix} GET_TEXT", $"GET_TEXT {el.Id}", r => r.Success, ct);
            await ctx.TestAsync($"{prefix} CLICK", $"CLICK {el.Id}", r => r.Success, ct);
        }
    }

    // ── ListView ────────────────────────────────────────────────────────────────
    static async Task TestListView(TestContext ctx, string windowTitle, string scanData, CancellationToken ct)
    {
        var elements = ScanHelper.FindByType(scanData, "ListView")
            .Concat(ScanHelper.FindByType(scanData, "ListItem"))
            .DistinctBy(e => e.Id)
            .Take(4).ToList();

        if (elements.Count == 0) { ctx.Record($"ListView: no elements found in '{windowTitle}'", false); return; }

        foreach (var el in elements)
        {
            var label = el.Name ?? el.AutomationId ?? $"ID:{el.Id}";
            var prefix = $"ListView [{label}]";

            await ctx.TestAsync($"{prefix} GET_TEXT", $"GET_TEXT {el.Id}", r => r.Success, ct);
            await ctx.TestAsync($"{prefix} CLICK", $"CLICK {el.Id}", r => r.Success, ct);
        }
    }
}
