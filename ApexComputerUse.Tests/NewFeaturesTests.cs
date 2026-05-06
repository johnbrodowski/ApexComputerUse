using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ApexComputerUse;
using Xunit;
using ElementNode = ApexComputerUse.CommandProcessor.ElementNode;

namespace ApexComputerUse.Tests;

/// <summary>
/// Tests for the agent-friction-fix work:
///   #1 ClassName included in match search
///   #3 gettext source field (covered by FlaUI helper - skipped: needs live UIA)
///   #4 batch CommandRequest deserialization
///   #6 waitfor field deserialization
///   #8 ApexResult error_data round-trips through JSON
///   #2 EventBroker emits create/close/title-changed and respects filters
///
/// Tests requiring a live Windows session (FlaUI UIA) are deliberately not covered here.
/// </summary>
public class NewFeaturesTests
{
    // -- #1 ClassName --------------------------------------------------------

    [Fact]
    public void MatchFilter_FindsByClassName()
    {
        var tree = new ElementNode
        {
            Id = 1, ControlType = "Window", Name = "App",
            Children = new List<ElementNode>
            {
                new() { Id = 2, ControlType = "Pane", Name = "", AutomationId = "",
                        ClassName = "WpfTextView" },
                new() { Id = 3, ControlType = "Button", Name = "OK" }
            }
        };

        var filtered = CommandProcessor.FilterTreeByMatch(tree, "WpfTextView", isRoot: true);
        Assert.NotNull(filtered);

        var ids = new List<int>();
        Walk(filtered);
        Assert.Contains(2, ids);     // class-name match
        Assert.DoesNotContain(3, ids); // unrelated sibling pruned

        void Walk(ElementNode? n)
        {
            if (n == null) return;
            ids.Add(n.Id);
            if (n.Children != null) foreach (var c in n.Children) Walk(c);
        }
    }

    [Fact]
    public void MatchFilter_ClassNameMatchIsCaseInsensitive()
    {
        var tree = new ElementNode
        {
            Id = 1, ControlType = "Window",
            Children = new List<ElementNode>
            {
                new() { Id = 2, ControlType = "Custom", ClassName = "WpfTextView" }
            }
        };
        var filtered = CommandProcessor.FilterTreeByMatch(tree, "wpftextview", isRoot: true);
        Assert.NotNull(filtered);
        Assert.Equal(1, filtered!.Children?.Count);
    }

    // -- #4 Batch parsing ----------------------------------------------------

    [Fact]
    public void JsonMapper_ParsesActionsArray_DefaultsCommandToBlank()
    {
        const string json = """
            {
              "actions": [
                {"action": "type", "value": "hello"},
                {"cmd": "find", "window": "Notepad"},
                {"action": "click"}
              ]
            }
            """;
        var req = CommandRequestJsonMapper.FromJson(json, "execute");
        Assert.NotNull(req.Actions);
        Assert.Equal(3, req.Actions!.Count);
        Assert.Equal("type",   req.Actions[0].Action);
        Assert.Equal("hello",  req.Actions[0].Value);
        Assert.Equal("find",   req.Actions[1].Command);
        Assert.Equal("Notepad", req.Actions[1].Window);
        Assert.Equal("click",  req.Actions[2].Action);
        // Step 0 had no cmd field - mapper leaves blank; the runner defaults to "execute".
        Assert.Equal("",       req.Actions[0].Command);
    }

    [Fact]
    public void JsonMapper_StopOnError_AcceptsBothNamings()
    {
        var snake = CommandRequestJsonMapper.FromJson(
            """{"stop_on_error": false, "actions": []}""", "execute");
        var camel = CommandRequestJsonMapper.FromJson(
            """{"stopOnError": false, "actions": []}""", "execute");
        Assert.False(snake.StopOnError);
        Assert.False(camel.StopOnError);
    }

    // -- #6 waitfor field parsing -------------------------------------------

    [Fact]
    public void JsonMapper_ParsesWaitForFields()
    {
        const string json = """
            {
              "action": "waitfor",
              "property": "value",
              "predicate": "contains",
              "expected": "built",
              "timeout": 15000,
              "interval": 250
            }
            """;
        var req = CommandRequestJsonMapper.FromJson(json, "execute");
        Assert.Equal("waitfor", req.Action);
        Assert.Equal("value",   req.Property);
        Assert.Equal("contains",req.Predicate);
        Assert.Equal("built",   req.Expected);
        Assert.Equal(15000,     req.Timeout);
        Assert.Equal(250,       req.Interval);
    }

    // -- Round 2 #2: wait-window field parsing -----------------------------

    [Fact]
    public void JsonMapper_AcceptsWaitWindowFields()
    {
        const string json = """
            {
              "action": "wait-window",
              "predicate": "contains",
              "expected": "Debug Console",
              "timeout": 15000,
              "interval": 250
            }
            """;
        var req = CommandRequestJsonMapper.FromJson(json, "execute");
        // wait-window reuses waitfor's request fields; confirm the action string itself
        // round-trips and the shared fields are still parsed (no Property required).
        Assert.Equal("wait-window", req.Action);
        Assert.Equal("contains",    req.Predicate);
        Assert.Equal("Debug Console", req.Expected);
        Assert.Equal(15000,         req.Timeout);
        Assert.Equal(250,           req.Interval);
    }

    // -- #8 ApexResult.ErrorData round-trip --------------------------------

    [Fact]
    public void ApexResult_From_PromotesErrorData_FromCommandResponse()
    {
        var cr = new CommandResponse
        {
            Success = false,
            Message = "Element does not support Toggle pattern.",
            ErrorData = new Dictionary<string, object>
            {
                ["failed_pattern"]     = "Toggle",
                ["supported_patterns"] = new[] { "Invoke", "Value" },
                ["element_state"]      = new Dictionary<string, object>
                {
                    ["enabled"]   = true,
                    ["offscreen"] = false
                },
                ["hint"] = "Element supports Invoke; try action=click"
            }
        };
        var result = ApexResult.From("execute", cr);
        Assert.False(result.Success);
        Assert.NotNull(result.ErrorData);
        Assert.Equal("Toggle", result.ErrorData!["failed_pattern"]);
        Assert.Contains("hint", result.ErrorData.Keys);
    }

    [Fact]
    public void ApexResult_From_PreservesFuzzyCandidateErrorData()
    {
        var candidates = new[]
        {
            new Dictionary<string, object>
            {
                ["name"] = "Search",
                ["automationId"] = "",
                ["controlType"] = "Button",
                ["matchType"] = "fuzzy",
                ["score"] = 0.42
            }
        };
        var cr = new CommandResponse
        {
            Success = false,
            Message = "Low-confidence match for 'Start'. Use a numeric ID or choose one of the candidates.",
            ErrorData = new Dictionary<string, object>
            {
                ["reason"] = "low_confidence",
                ["query"] = "Start",
                ["scope"] = "element",
                ["candidates"] = candidates
            }
        };

        var result = ApexResult.From("find", cr);

        Assert.False(result.Success);
        Assert.NotNull(result.ErrorData);
        Assert.Equal("low_confidence", result.ErrorData!["reason"]);
        Assert.Same(candidates, result.ErrorData["candidates"]);
    }

    [Fact]
    public void ApexResult_From_PromotesExtras_IntoData_DictionaryUsed_ForGetTextSource()
    {
        var cr = new CommandResponse
        {
            Success = true,
            Message = "'gettext' executed.",
            Data    = "Hello, World!",
            Extras  = new Dictionary<string, string> { ["source"] = "ValuePattern" }
        };
        var result = ApexResult.From("execute", cr);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("Hello, World!", result.Data!["result"]);
        Assert.Equal("ValuePattern",  result.Data["source"]);
    }

    // -- #2 EventBroker behaviour -------------------------------------------

    [Fact]
    public async Task EventBroker_EmitsCreateCloseAndTitleChange_OnDiff()
    {
        var sequences = new Queue<List<(int id, string title)>>();
        sequences.Enqueue(new() { (1, "A"), (2, "B") });                    // initial baseline
        sequences.Enqueue(new() { (1, "A"), (2, "B"), (3, "C") });          // 3 created
        sequences.Enqueue(new() { (1, "A"), (3, "C-renamed") });            // 2 closed, 3 renamed
        var fallback = new List<(int, string)> { (1, "A"), (3, "C-renamed") };

        Func<List<(int, string)>> snap = () =>
        {
            return sequences.Count > 0 ? sequences.Dequeue() : fallback;
        };

        using var broker = new EventBroker(snap, pollInterval: TimeSpan.FromMilliseconds(40));
        using var sub    = broker.Subscribe();

        var collected = new List<EventBroker.EventEnvelope>();
        var deadline  = DateTime.UtcNow.AddSeconds(2);
        while (DateTime.UtcNow < deadline && collected.Count < 3)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
            try
            {
                if (await sub.Reader.WaitToReadAsync(cts.Token))
                    while (sub.Reader.TryRead(out var ev)) collected.Add(ev);
            }
            catch (OperationCanceledException) { /* keep looping */ }
        }

        Assert.Contains(collected, e => e.Type == "window-created"        && e.WindowId == 3);
        Assert.Contains(collected, e => e.Type == "window-closed"         && e.WindowId == 2);
        Assert.Contains(collected, e => e.Type == "window-title-changed"  && e.WindowId == 3
                                                                            && (string?)e.Data["title"] == "C-renamed");
    }

    [Fact]
    public async Task EventBroker_TypeFilter_ScopesEmittedEvents()
    {
        var sequences = new Queue<List<(int id, string title)>>();
        sequences.Enqueue(new() { (1, "A") });
        sequences.Enqueue(new() { (1, "A"), (2, "B") });   // create event
        sequences.Enqueue(new() { (1, "A-renamed"), (2, "B") }); // title event
        var fallback = new List<(int, string)> { (1, "A-renamed"), (2, "B") };
        Func<List<(int, string)>> snap = () => sequences.Count > 0 ? sequences.Dequeue() : fallback;

        using var broker = new EventBroker(snap, pollInterval: TimeSpan.FromMilliseconds(40));
        using var sub    = broker.Subscribe(types: new HashSet<string> { "window-created" });

        var collected = new List<EventBroker.EventEnvelope>();
        var deadline  = DateTime.UtcNow.AddSeconds(2);
        while (DateTime.UtcNow < deadline && collected.Count < 1)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
            try
            {
                if (await sub.Reader.WaitToReadAsync(cts.Token))
                    while (sub.Reader.TryRead(out var ev)) collected.Add(ev);
            }
            catch (OperationCanceledException) { /* keep looping */ }
        }

        Assert.Single(collected);
        Assert.Equal("window-created", collected[0].Type);
        Assert.Equal(2, collected[0].WindowId);
    }
}
