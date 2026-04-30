using ApexUIBridge.ElementTester;

// -- Usage ---------------------------------------------------------------------
// dotnet run -- TextBox                  Test TextBox in all torture windows
// dotnet run -- CheckBox wpf             Test CheckBox in WPF torture window only
// dotnet run -- CheckBox winforms        Test CheckBox in WinForms torture window only
// dotnet run -- all                      Test all element types sequentially
// dotnet run -- list                     List available element types
// dotnet run -- scan                     Just scan and list all windows
// dotnet run -- scan "Window Title"      Scan a specific window and dump elements
//
// Prerequisites: ApexUIBridge + torture test windows must already be running.

var elementType = args.FirstOrDefault() ?? "list";
var windowFilter = args.Length > 1 ? args[1].ToLowerInvariant() : "both";

using var client = new BridgeClient();
var ct = CancellationToken.None;

// -- Check bridge is running -------------------------------------------------
if (!await client.IsReadyAsync(ct))
{
    Console.Error.WriteLine("ERROR: Bridge is not running at http://localhost:8765");
    Console.Error.WriteLine("Start ApexUIBridge and the torture test windows first.");
    return 1;
}
Console.WriteLine("[ElementTester] Bridge is ready.\n");

// -- List command ------------------------------------------------------------
if (elementType.Equals("list", StringComparison.OrdinalIgnoreCase))
{
    Console.WriteLine("Available element types:");
    foreach (var key in ElementTests.All.Keys.OrderBy(k => k))
        Console.WriteLine($"  - {key}");
    Console.WriteLine("\nUsage: dotnet run -- <ElementType> [wpf|winforms|both]");
    Console.WriteLine("       dotnet run -- all");
    Console.WriteLine("       dotnet run -- scan [\"Window Title\"]");
    return 0;
}

// -- Scan command ------------------------------------------------------------
if (elementType.Equals("scan", StringComparison.OrdinalIgnoreCase))
{
    if (windowFilter != "both")
    {
        // Scan a specific window by title
        var title = args[1]; // use original case
        Console.WriteLine($"Scanning window: '{title}'");
        var scanResult = await client.SendAsync($"SCAN_WINDOW {title}", ct);
        if (scanResult.Success)
        {
            Console.WriteLine(scanResult.Data);
            var parsed = ScanHelper.ParseAll(scanResult.Data!);
            Console.WriteLine($"\n-- {parsed.Count} elements found --");
            var byType = parsed.GroupBy(e => e.ControlType ?? "Unknown").OrderBy(g => g.Key);
            foreach (var g in byType)
                Console.WriteLine($"  {g.Key}: {g.Count()}");
        }
        else
        {
            Console.Error.WriteLine($"Scan failed: {scanResult.Message}");
        }
    }
    else
    {
        // List all windows
        var listResult = await client.SendAsync("LIST_WINDOWS", ct);
        Console.WriteLine(listResult.Success ? listResult.Data : $"Failed: {listResult.Message}");
    }
    return 0;
}

// -- Resolve target windows --------------------------------------------------
// The torture test windows have these titles:
//   WinForms: "System Configuration Console - UI Torture Test"
//   WPF:      "System Configuration Console - WPF Torture Test"
var targets = new List<(string Label, string TitleSearch)>();
if (windowFilter is "both" or "winforms" or "wf")
    targets.Add(("WinForms Torture", "UI Torture Test"));
if (windowFilter is "both" or "wpf")
    targets.Add(("WPF Torture", "WPF Torture Test"));

if (targets.Count == 0)
{
    Console.Error.WriteLine($"Unknown window filter: '{windowFilter}'. Use 'wpf', 'winforms', or 'both'.");
    return 1;
}

// -- Determine which element types to test -----------------------------------
var typesToTest = new List<string>();
if (elementType.Equals("all", StringComparison.OrdinalIgnoreCase))
{
    typesToTest.AddRange(ElementTests.All.Keys);
}
else if (ElementTests.All.ContainsKey(elementType))
{
    typesToTest.Add(elementType);
}
else
{
    Console.Error.WriteLine($"Unknown element type: '{elementType}'");
    Console.Error.WriteLine("Run with 'list' to see available types.");
    return 1;
}

// -- Run tests ---------------------------------------------------------------
var ctx = new TestContext(client);

foreach (var (label, titleSearch) in targets)
{
    Console.WriteLine($"\n{"?",60}");
    Console.WriteLine($"  {label}");
    Console.WriteLine($"{"?",60}");

    var scan = await client.SendAsync($"SCAN_WINDOW {titleSearch}", ct);
    if (!scan.Success || scan.Data == null)
    {
        ctx.Record($"SCAN_WINDOW '{titleSearch}'", false, scan.Message);
        Console.Error.WriteLine($"  Could not scan '{titleSearch}': {scan.Message}");
        Console.Error.WriteLine("  Make sure the torture test window is open.");
        continue;
    }

    Console.WriteLine($"  Scan OK - {ScanHelper.ParseAll(scan.Data).Count} elements found\n");

    foreach (var type in typesToTest)
    {
        Console.WriteLine($"-- {type} ----------------------------------------");
        var testFn = ElementTests.All[type];
        try
        {
            await testFn(ctx, label, scan.Data, ct);
        }
        catch (Exception ex)
        {
            ctx.Record($"{type} threw exception", false, ex.Message);
        }

        // For WPF: rescan after each element type since tab switching
        // may have changed the visual tree
        if (label.Contains("WPF"))
        {
            var rescan = await client.SendAsync($"SCAN_WINDOW {titleSearch}", ct);
            if (rescan.Success && rescan.Data != null)
                scan = rescan;
        }
    }
}

ctx.PrintSummary();
return ctx.Failed > 0 ? 1 : 0;

