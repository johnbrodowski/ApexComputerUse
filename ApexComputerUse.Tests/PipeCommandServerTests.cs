using ApexComputerUse;
using Xunit;
using System.Reflection;

namespace ApexComputerUse.Tests;

/// <summary>
/// Unit tests for <see cref="CommandRequestJsonMapper.FromJsonSelfDescribing"/>.
/// The method is tested in isolation — no named-pipe connection required.
/// </summary>
public class PipeCommandServerTests
{
    // ── Happy-path parsing ────────────────────────────────────────────────

    [Fact]
    public void ParseJson_MinimalCommand_SetsCommand()
    {
        var req = CommandRequestJsonMapper.FromJsonSelfDescribing("""{"command":"windows"}""");
        Assert.Equal("windows", req.Command);
    }

    [Fact]
    public void ParseJson_AllFields_MappedCorrectly()
    {
        const string json = """
            {
              "command":      "find",
              "window":       "Notepad",
              "automationId": "textBox1",
              "elementName":  "Content",
              "searchType":   "Edit",
              "action":       "click",
              "value":        "42",
              "model":        "llama.gguf",
              "proj":         "clip.gguf",
              "prompt":       "what do you see"
            }
            """;

        var req = CommandRequestJsonMapper.FromJsonSelfDescribing(json);

        Assert.Equal("find",            req.Command);
        Assert.Equal("Notepad",         req.Window);
        Assert.Equal("textBox1",        req.AutomationId);
        Assert.Equal("Content",         req.ElementName);
        Assert.Equal("Edit",            req.SearchType);
        Assert.Equal("click",           req.Action);
        Assert.Equal("42",              req.Value);
        Assert.Equal("llama.gguf",      req.ModelPath);
        Assert.Equal("clip.gguf",       req.MmProjPath);
        Assert.Equal("what do you see", req.Prompt);
    }

    [Fact]
    public void ParseJson_ShortAliases_ResolvedCorrectly()
    {
        // "id" maps to AutomationId; "name" maps to ElementName; "type" to SearchType.
        const string json = """{"command":"find","id":"btn1","name":"OK","type":"Button"}""";
        var req = CommandRequestJsonMapper.FromJsonSelfDescribing(json);
        Assert.Equal("btn1",   req.AutomationId);
        Assert.Equal("OK",     req.ElementName);
        Assert.Equal("Button", req.SearchType);
    }

    [Fact]
    public void ParseJson_ModelPathAlias_Resolved()
    {
        const string json = """{"command":"ai","modelPath":"path/to/model.gguf"}""";
        var req = CommandRequestJsonMapper.FromJsonSelfDescribing(json);
        Assert.Equal("path/to/model.gguf", req.ModelPath);
    }

    [Fact]
    public void ParseJson_MmProjPathAlias_Resolved()
    {
        const string json = """{"command":"ai","mmProjPath":"clip.gguf"}""";
        var req = CommandRequestJsonMapper.FromJsonSelfDescribing(json);
        Assert.Equal("clip.gguf", req.MmProjPath);
    }

    // ── Error fallback ────────────────────────────────────────────────────

    [Fact]
    public void ParseJson_InvalidJson_FallsBackToHelp()
    {
        var req = CommandRequestJsonMapper.FromJsonSelfDescribing("not valid json {{{");
        Assert.Equal("help", req.Command);
    }

    [Fact]
    public void ParseJson_EmptyObject_CommandIsEmpty()
    {
        var req = CommandRequestJsonMapper.FromJsonSelfDescribing("{}");
        Assert.Equal("", req.Command);
    }

    [Fact]
    public void ParseJson_MissingCommand_CommandIsEmpty()
    {
        var req = CommandRequestJsonMapper.FromJsonSelfDescribing("""{"window":"Notepad"}""");
        Assert.Equal("",        req.Command);
        Assert.Equal("Notepad", req.Window);
    }

    [Fact]
    public void ParseJson_NullFieldValues_TreatedAsNull()
    {
        const string json = """{"command":"find","window":null,"action":null}""";
        var req = CommandRequestJsonMapper.FromJsonSelfDescribing(json);
        Assert.Equal("find", req.Command);
        Assert.Null(req.Window);
        Assert.Null(req.Action);
    }

    // ── Edge cases ────────────────────────────────────────────────────────

    [Fact]
    public void ParseJson_ExtraUnknownFields_Ignored()
    {
        const string json = """{"command":"status","unknownField":"value","another":42}""";
        var req = CommandRequestJsonMapper.FromJsonSelfDescribing(json);
        Assert.Equal("status", req.Command);
    }

    [Fact]
    public void ParseJson_EmptyString_FallsBackToHelp()
    {
        var req = CommandRequestJsonMapper.FromJsonSelfDescribing("");
        Assert.Equal("help", req.Command);
    }

    [Fact]
    public void Stop_WhenClientTaskFaulted_DoesNotThrow()
    {
        using var processor = new CommandProcessor();
        using var server = new PipeCommandServer($"ApexPipeTest_{Guid.NewGuid():N}", processor);
        server.Start();

        var tasksField = typeof(PipeCommandServer).GetField("_clientTasks", BindingFlags.NonPublic | BindingFlags.Instance);
        var lockField = typeof(PipeCommandServer).GetField("_clientTasksLock", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(tasksField);
        Assert.NotNull(lockField);

        var tasks = tasksField!.GetValue(server) as List<Task>;
        var gate = lockField!.GetValue(server);
        Assert.NotNull(tasks);
        Assert.NotNull(gate);

        var tcs = new TaskCompletionSource();
        tcs.SetException(new InvalidOperationException("boom"));

        lock (gate!)
        {
            tasks!.Add(tcs.Task);
        }

        var ex = Record.Exception(() => server.Stop());
        Assert.Null(ex);
    }
}
