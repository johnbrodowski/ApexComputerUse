using ApexComputerUse;
using Xunit;

namespace ApexComputerUse.Tests;

/// <summary>
/// Unit tests for the apex tool-call parsing and system-prompt generation helpers
/// in <see cref="AiChatService"/>. No AI provider or HTTP server is required.
/// </summary>
public class AiChatServiceTests
{
    // ── ParseApexCalls — empty / no-match cases ───────────────────────────────

    [Fact]
    public void ParseApexCalls_EmptyString_ReturnsEmpty()
    {
        Assert.Empty(AiChatService.ParseApexCalls(""));
    }

    [Fact]
    public void ParseApexCalls_NoApexBlocks_ReturnsEmpty()
    {
        var text = "Here is a plain answer with no code blocks at all.";
        Assert.Empty(AiChatService.ParseApexCalls(text));
    }

    [Fact]
    public void ParseApexCalls_NonApexCodeBlock_ReturnsEmpty()
    {
        var text = "```json\n{\"foo\":1}\n```";
        Assert.Empty(AiChatService.ParseApexCalls(text));
    }

    // ── ParseApexCalls — GET ──────────────────────────────────────────────────

    [Fact]
    public void ParseApexCalls_GetNoBody_ParsesMethodAndPath()
    {
        var text = "```apex\nGET /windows\n```";
        var calls = AiChatService.ParseApexCalls(text);

        Assert.Single(calls);
        Assert.Equal("GET", calls[0].Method);
        Assert.Equal("/windows", calls[0].Path);
        Assert.Null(calls[0].Body);
    }

    [Fact]
    public void ParseApexCalls_GetWithQueryString_PreservesQuery()
    {
        var text = "```apex\nGET /elements?onscreen=true\n```";
        var calls = AiChatService.ParseApexCalls(text);

        Assert.Single(calls);
        Assert.Equal("/elements?onscreen=true", calls[0].Path);
    }

    // ── ParseApexCalls — POST with body ───────────────────────────────────────

    [Fact]
    public void ParseApexCalls_PostWithJsonBody_ParsesAll()
    {
        var text = """
            ```apex
            POST /find
            {"window":"Notepad","type":"Edit"}
            ```
            """;
        var calls = AiChatService.ParseApexCalls(text);

        Assert.Single(calls);
        Assert.Equal("POST", calls[0].Method);
        Assert.Equal("/find", calls[0].Path);
        Assert.Equal("{\"window\":\"Notepad\",\"type\":\"Edit\"}", calls[0].Body);
    }

    [Fact]
    public void ParseApexCalls_PostWithMultilineBody_TrimsWhitespace()
    {
        var text = "```apex\nPOST /exec\n  {\"action\":\"click\"}  \n```";
        var calls = AiChatService.ParseApexCalls(text);

        Assert.Single(calls);
        Assert.Equal("{\"action\":\"click\"}", calls[0].Body);
    }

    // ── ParseApexCalls — method normalisation ─────────────────────────────────

    [Theory]
    [InlineData("get",    "GET")]
    [InlineData("post",   "POST")]
    [InlineData("PUT",    "PUT")]
    [InlineData("delete", "DELETE")]
    [InlineData("patch",  "PATCH")]
    public void ParseApexCalls_MethodIsNormalisedToUppercase(string rawMethod, string expectedMethod)
    {
        var text = $"```apex\n{rawMethod} /test\n```";
        var calls = AiChatService.ParseApexCalls(text);

        Assert.Single(calls);
        Assert.Equal(expectedMethod, calls[0].Method);
    }

    // ── ParseApexCalls — multiple blocks ──────────────────────────────────────

    [Fact]
    public void ParseApexCalls_TwoBlocks_ReturnsBothInOrder()
    {
        var text = """
            First I will list the windows:
            ```apex
            GET /windows
            ```
            Then I will find the element:
            ```apex
            POST /find
            {"window":"Notepad"}
            ```
            """;
        var calls = AiChatService.ParseApexCalls(text);

        Assert.Equal(2, calls.Count);
        Assert.Equal("GET",    calls[0].Method);
        Assert.Equal("/windows", calls[0].Path);
        Assert.Equal("POST",   calls[1].Method);
        Assert.Equal("/find",  calls[1].Path);
        Assert.Equal("{\"window\":\"Notepad\"}", calls[1].Body);
    }

    [Fact]
    public void ParseApexCalls_ThreeBlocks_ReturnsAll()
    {
        var text = """
            ```apex
            GET /windows
            ```
            ```apex
            GET /status
            ```
            ```apex
            POST /exec
            {"action":"gettext"}
            ```
            """;
        Assert.Equal(3, AiChatService.ParseApexCalls(text).Count);
    }

    // ── ParseApexCalls — embedded in prose ────────────────────────────────────

    [Fact]
    public void ParseApexCalls_BlockEmbeddedInProse_IsExtracted()
    {
        var text = """
            Sure, let me check what windows are open.

            ```apex
            GET /windows
            ```

            I'll analyse the results and respond shortly.
            """;
        var calls = AiChatService.ParseApexCalls(text);

        Assert.Single(calls);
        Assert.Equal("GET", calls[0].Method);
        Assert.Equal("/windows", calls[0].Path);
    }

    // ── BuildApexSystemPrompt ─────────────────────────────────────────────────

    [Fact]
    public void BuildApexSystemPrompt_ContainsPort()
    {
        var prompt = AiChatService.BuildApexSystemPrompt(9999);
        Assert.Contains("localhost:9999", prompt);
    }

    [Fact]
    public void BuildApexSystemPrompt_ContainsApexBlockExample()
    {
        var prompt = AiChatService.BuildApexSystemPrompt(8080);
        Assert.Contains("```apex", prompt);
    }

    [Theory]
    [InlineData("GET /windows")]
    [InlineData("GET /status")]
    [InlineData("POST /find")]
    [InlineData("POST /exec")]
    [InlineData("POST /capture")]
    public void BuildApexSystemPrompt_ListsKeyEndpoints(string endpoint)
    {
        var prompt = AiChatService.BuildApexSystemPrompt(8080);
        Assert.Contains(endpoint, prompt);
    }
}
