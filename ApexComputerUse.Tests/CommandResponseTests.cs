using System.Text.Json;
using ApexComputerUse;
using Xunit;

namespace ApexComputerUse.Tests;

/// <summary>
/// Unit tests for <see cref="CommandResponse.ToText"/> and
/// <see cref="CommandResponse.ToJson"/>.
/// </summary>
public class CommandResponseTests
{
    // \-\- ToText \-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-

    [Fact]
    public void ToText_SuccessNoData_StartsWithOK()
    {
        var r = new CommandResponse { Success = true, Message = "done" };
        Assert.Equal("OK done", r.ToText());
    }

    [Fact]
    public void ToText_FailureNoData_StartsWithERR()
    {
        var r = new CommandResponse { Success = false, Message = "oops" };
        Assert.Equal("ERR oops", r.ToText());
    }

    [Fact]
    public void ToText_WithData_DataOnSecondLine()
    {
        var r = new CommandResponse { Success = true, Message = "found", Data = "payload" };
        Assert.Equal("OK found\npayload", r.ToText());
    }

    [Fact]
    public void ToText_NullData_NoTrailingNewline()
    {
        var r = new CommandResponse { Success = true, Message = "ok", Data = null };
        Assert.DoesNotContain("\n", r.ToText());
    }

    // \-\- ToJson \-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-

    [Fact]
    public void ToJson_ProducesValidJson()
    {
        var r    = new CommandResponse { Success = true, Message = "hello", Data = "world" };
        string j = r.ToJson();
        var    d = JsonDocument.Parse(j);

        Assert.True(d.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("hello", d.RootElement.GetProperty("message").GetString());
        Assert.Equal("world", d.RootElement.GetProperty("data").GetString());
    }

    [Fact]
    public void ToJson_NullData_SerializedAsNull()
    {
        var r = new CommandResponse { Success = false, Message = "err", Data = null };
        using var d = JsonDocument.Parse(r.ToJson());
        Assert.Equal(JsonValueKind.Null, d.RootElement.GetProperty("data").ValueKind);
    }

    [Fact]
    public void ToJson_IsIndented()
    {
        var r = new CommandResponse { Success = true, Message = "m" };
        Assert.Contains("\n", r.ToJson());
    }
}
