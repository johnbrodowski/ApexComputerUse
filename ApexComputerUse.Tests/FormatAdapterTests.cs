using System.Text;
using System.Text.Json;
using ApexComputerUse;
using Xunit;

namespace ApexComputerUse.Tests;

/// <summary>
/// Tests for <see cref="FormatAdapter.Render"/> format negotiation and serialization.
/// Covers the json, text, and html render paths without a live HTTP server.
/// </summary>
public class FormatAdapterTests
{
    private static ApexResult SuccessResult(string action = "test", string result = "ok") =>
        new() { Success = true, Action = action, Data = new() { ["result"] = result, ["message"] = "Done." } };

    private static ApexResult FailureResult(string action = "test", string error = "Something failed.") =>
        new() { Success = false, Action = action, Error = error };

    // -- JSON format --------------------------------------------------------

    [Fact]
    public void Render_Json_Success_Returns200AndJsonContentType()
    {
        var (body, ct, status) = FormatAdapter.Render(SuccessResult(), "json");

        Assert.Equal(200, status);
        Assert.StartsWith("application/json", ct);
        Assert.NotEmpty(body);
    }

    [Fact]
    public void Render_Json_Failure_Returns400()
    {
        var (_, _, status) = FormatAdapter.Render(FailureResult(), "json");

        Assert.Equal(400, status);
    }

    [Fact]
    public void Render_Json_SuccessResult_BodyContainsSuccessTrue()
    {
        var (body, _, _) = FormatAdapter.Render(SuccessResult("ping"), "json");
        var json = Encoding.UTF8.GetString(body);
        using var doc = JsonDocument.Parse(json);

        Assert.True(doc.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("ping", doc.RootElement.GetProperty("action").GetString());
    }

    [Fact]
    public void Render_Json_FailureResult_BodyContainsErrorAndSuccessFalse()
    {
        var (body, _, _) = FormatAdapter.Render(FailureResult("exec", "Element not found."), "json");
        var json = Encoding.UTF8.GetString(body);
        using var doc = JsonDocument.Parse(json);

        Assert.False(doc.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("Element not found.", doc.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public void Render_Json_NullError_ErrorFieldIsNull()
    {
        var (body, _, _) = FormatAdapter.Render(SuccessResult(), "json");
        var json = Encoding.UTF8.GetString(body);
        using var doc = JsonDocument.Parse(json);

        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("error").ValueKind);
    }

    // -- Text format --------------------------------------------------------

    [Fact]
    public void Render_Text_Success_Returns200AndTextContentType()
    {
        var (body, ct, status) = FormatAdapter.Render(SuccessResult(), "text");

        Assert.Equal(200, status);
        Assert.StartsWith("text/plain", ct);
        Assert.NotEmpty(body);
    }

    [Fact]
    public void Render_Text_Failure_Returns400()
    {
        var (_, _, status) = FormatAdapter.Render(FailureResult(), "text");

        Assert.Equal(400, status);
    }

    [Fact]
    public void Render_Text_BodyContainsSuccessAndActionLines()
    {
        var (body, _, _) = FormatAdapter.Render(SuccessResult("find"), "text");
        var text = Encoding.UTF8.GetString(body);

        Assert.Contains("success: True", text);
        Assert.Contains("action:  find", text);
    }

    [Fact]
    public void Render_Text_ErrorResult_ContainsErrorLine()
    {
        var (body, _, _) = FormatAdapter.Render(FailureResult("exec", "No element selected."), "text");
        var text = Encoding.UTF8.GetString(body);

        Assert.Contains("error:   No element selected.", text);
    }

    [Fact]
    public void Render_Text_DataFields_AppearAsKeyValueLines()
    {
        var result = new ApexResult
        {
            Success = true, Action = "gettext",
            Data = new() { ["result"] = "Hello", ["message"] = "ok" }
        };
        var (body, _, _) = FormatAdapter.Render(result, "text");
        var text = Encoding.UTF8.GetString(body);

        Assert.Contains("result: Hello", text);
        Assert.Contains("message: ok", text);
    }

    // -- HTML format (default) ----------------------------------------------

    [Fact]
    public void Render_Html_Success_Returns200AndHtmlContentType()
    {
        var (body, ct, status) = FormatAdapter.Render(SuccessResult(), "html");

        Assert.Equal(200, status);
        Assert.StartsWith("text/html", ct);
        Assert.Contains("<!DOCTYPE html>", Encoding.UTF8.GetString(body));
    }

    [Fact]
    public void Render_UnknownFormat_DefaultsToHtml()
    {
        var (body, ct, _) = FormatAdapter.Render(SuccessResult(), "bogus");

        Assert.StartsWith("text/html", ct);
        Assert.Contains("<!DOCTYPE html>", Encoding.UTF8.GetString(body));
    }

    [Fact]
    public void Render_Html_EmbedsSafeJson_NoRawScriptClose()
    {
        // A result whose data contains a </script> sequence must be escaped so it
        // cannot break out of the embedded <script type="application/json"> block.
        var result = new ApexResult
        {
            Success = true, Action = "test",
            Data = new() { ["result"] = "a</script>b" }
        };
        var (body, _, _) = FormatAdapter.Render(result, "html");
        var html = Encoding.UTF8.GetString(body);

        Assert.DoesNotContain("</script>a", html);
    }
}
