namespace ApexUIBridge.ElementTester;

/// <summary>
/// Tracks test results and provides structured output.
/// </summary>
public sealed class TestContext
{
    private readonly BridgeClient _client;
    private readonly List<(string Name, bool Passed, string Detail)> _results = new();

    public TestContext(BridgeClient client) => _client = client;
    public BridgeClient Client => _client;

    public int Passed => _results.Count(r => r.Passed);
    public int Failed => _results.Count(r => !r.Passed);
    public int Total  => _results.Count;

    /// <summary>Execute a bridge command and record the result.</summary>
    public async Task<BridgeResponse> TestAsync(string testName, string command,
        Func<BridgeResponse, bool>? assert = null, CancellationToken ct = default)
    {
        var r = await _client.SendAsync(command, ct);
        bool passed = assert != null ? assert(r) : r.Success;
        _results.Add((testName, passed, passed ? r.Message : $"{r.Message} | Data: {r.Data ?? "(null)"}"));

        var icon = passed ? "PASS" : "FAIL";
        var color = passed ? "\u001b[32m" : "\u001b[31m";
        Console.WriteLine($"  {color}[{icon}]\u001b[0m {testName}");
        if (!passed)
            Console.WriteLine($"         Command:  {command}");
        if (!passed)
            Console.WriteLine($"         Response: {r.Message}");

        return r;
    }

    /// <summary>Record a manual pass/fail (for checks that aren't a single command).</summary>
    public void Record(string testName, bool passed, string detail = "")
    {
        _results.Add((testName, passed, detail));
        var icon = passed ? "PASS" : "FAIL";
        var color = passed ? "\u001b[32m" : "\u001b[31m";
        Console.WriteLine($"  {color}[{icon}]\u001b[0m {testName}");
        if (!passed && !string.IsNullOrEmpty(detail))
            Console.WriteLine($"         {detail}");
    }

    /// <summary>Print final summary.</summary>
    public void PrintSummary()
    {
        Console.WriteLine();
        Console.WriteLine(new string('─', 60) + " Summary");
        Console.WriteLine($"  Total: {Total}   Passed: {Passed}   Failed: {Failed}");
        if (Failed > 0)
        {
            Console.WriteLine("\n  Failed tests:");
            foreach (var r in _results.Where(r => !r.Passed))
                Console.WriteLine($"    - {r.Name}: {r.Detail}");
        }
        Console.WriteLine();
    }
}
