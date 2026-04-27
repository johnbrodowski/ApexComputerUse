using ApexComputerUse;
using Xunit;

namespace ApexComputerUse.Tests;

/// <summary>
/// Tests for /run command extraction — backward-compat "value" field and new "command" alias.
/// ParseJsonString is internal (InternalsVisibleTo).
/// </summary>
public class HttpRunParserTests
{
    // ── ParseJsonString helper ────────────────────────────────────────────

    [Fact]
    public void ParseJsonString_CommandField_ReturnsValue()
    {
        var result = HttpCommandServer.ParseJsonString("""{"command":"dir C:\\"}""", "command");
        Assert.Equal("dir C:\\", result);
    }

    [Fact]
    public void ParseJsonString_MissingField_ReturnsNull()
    {
        var result = HttpCommandServer.ParseJsonString("""{"value":"dir C:\\"}""", "command");
        Assert.Null(result);
    }

    [Fact]
    public void ParseJsonString_EmptyBody_ReturnsNull()
    {
        Assert.Null(HttpCommandServer.ParseJsonString("", "command"));
        Assert.Null(HttpCommandServer.ParseJsonString("   ", "command"));
    }

    [Fact]
    public void ParseJsonString_InvalidJson_ReturnsNull()
    {
        Assert.Null(HttpCommandServer.ParseJsonString("{not valid json", "command"));
    }

    // ── CommandRequestJsonMapper — "value" field backward compat ─────────

    [Fact]
    public void FromJson_ValueField_MapsToValue()
    {
        var req = CommandRequestJsonMapper.FromJson("""{"value":"whoami"}""", "run");
        Assert.Equal("whoami", req.Value);
    }

    [Fact]
    public void FromJson_NullBody_DoesNotThrow()
    {
        var req = CommandRequestJsonMapper.FromJson("", "run");
        Assert.Null(req.Value);
    }
}
