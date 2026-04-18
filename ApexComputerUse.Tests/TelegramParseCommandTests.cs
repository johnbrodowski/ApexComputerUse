using ApexComputerUse;
using Xunit;

namespace ApexComputerUse.Tests;

/// <summary>
/// Unit tests for <see cref="TelegramController.ParseCommand"/> and
/// <see cref="CommandLineParser.Tokenize"/>.
/// Both methods are tested in isolation — no Telegram bot connection required.
/// </summary>
public class TelegramParseCommandTests
{
    // ── ParseCommand — gating rules ───────────────────────────────────────

    [Fact]
    public void ParseCommand_NoSlashPrefix_ReturnsNull()
    {
        Assert.Null(TelegramController.ParseCommand("find window=Notepad"));
    }

    [Fact]
    public void ParseCommand_EmptyString_ReturnsNull()
    {
        Assert.Null(TelegramController.ParseCommand(""));
    }

    // ── /find ─────────────────────────────────────────────────────────────

    [Fact]
    public void ParseCommand_Find_SetsWindowAndName()
    {
        var req = TelegramController.ParseCommand("/find window=Notepad name=Edit");
        Assert.NotNull(req);
        Assert.Equal("find",    req!.Command);
        Assert.Equal("Notepad", req.Window);
        Assert.Equal("Edit",    req.ElementName);
    }

    [Fact]
    public void ParseCommand_Find_ShortAliases()
    {
        var req = TelegramController.ParseCommand("/find w=Calc n=btn1 t=Button");
        Assert.NotNull(req);
        Assert.Equal("Calc",   req!.Window);
        Assert.Equal("btn1",   req.ElementName);
        Assert.Equal("Button", req.SearchType);
    }

    [Fact]
    public void ParseCommand_Find_AutomationId()
    {
        var req = TelegramController.ParseCommand("/find window=App id=textBox1");
        Assert.NotNull(req);
        Assert.Equal("textBox1", req!.AutomationId);
    }

    // ── /execute / /exec ──────────────────────────────────────────────────

    [Fact]
    public void ParseCommand_Execute_SetsActionAndValue()
    {
        var req = TelegramController.ParseCommand("/execute action=click value=42");
        Assert.NotNull(req);
        Assert.Equal("execute", req!.Command);
        Assert.Equal("click",   req.Action);
        Assert.Equal("42",      req.Value);
    }

    [Fact]
    public void ParseCommand_ExecAlias_MapsToExecuteCommand()
    {
        var req = TelegramController.ParseCommand("/exec a=type v=hello");
        Assert.NotNull(req);
        Assert.Equal("execute", req!.Command);
        Assert.Equal("type",    req.Action);
        Assert.Equal("hello",   req.Value);
    }

    // ── /ocr ─────────────────────────────────────────────────────────────

    [Fact]
    public void ParseCommand_Ocr_ValueFromKeyedArg()
    {
        var req = TelegramController.ParseCommand("/ocr value=10,20,300,100");
        Assert.NotNull(req);
        Assert.Equal("ocr",          req!.Command);
        Assert.Equal("10,20,300,100", req.Value);
    }

    [Fact]
    public void ParseCommand_Ocr_ValueFromBareArgs()
    {
        // When no key=value, bare comma-containing arg is used as value.
        var req = TelegramController.ParseCommand("/ocr 10,20,300,100");
        Assert.NotNull(req);
        Assert.Equal("10,20,300,100", req!.Value);
    }

    // ── /capture ─────────────────────────────────────────────────────────

    [Fact]
    public void ParseCommand_Capture_SetsActionAndValue()
    {
        var req = TelegramController.ParseCommand("/capture action=window value=7");
        Assert.NotNull(req);
        Assert.Equal("capture", req!.Command);
        Assert.Equal("window",  req.Action);
        Assert.Equal("7",       req.Value);
    }

    // ── /status, /windows, /elements ─────────────────────────────────────

    [Fact]
    public void ParseCommand_Status_ReturnsStatusCommand()
    {
        var req = TelegramController.ParseCommand("/status");
        Assert.NotNull(req);
        Assert.Equal("status", req!.Command);
    }

    [Fact]
    public void ParseCommand_Windows_ReturnsWindowsCommand()
    {
        var req = TelegramController.ParseCommand("/windows");
        Assert.Equal("windows", req!.Command);
    }

    [Fact]
    public void ParseCommand_Elements_WithType()
    {
        var req = TelegramController.ParseCommand("/elements type=Button");
        Assert.NotNull(req);
        Assert.Equal("elements", req!.Command);
        Assert.Equal("Button",   req.SearchType);
    }

    [Fact]
    public void ParseCommand_Elements_TypeFromBareArg()
    {
        var req = TelegramController.ParseCommand("/elements Button");
        Assert.NotNull(req);
        Assert.Equal("Button", req!.SearchType);
    }

    // ── /ai ──────────────────────────────────────────────────────────────

    [Fact]
    public void ParseCommand_Ai_SetsActionModelAndPrompt()
    {
        var req = TelegramController.ParseCommand("/ai action=ask prompt=what");
        Assert.NotNull(req);
        Assert.Equal("ai",   req!.Command);
        Assert.Equal("ask",  req.Action);
        Assert.Equal("what", req.Prompt);
    }

    [Fact]
    public void ParseCommand_Ai_ActionFromBareFirstWord()
    {
        // Without explicit action=, first word is used as action.
        var req = TelegramController.ParseCommand("/ai describe");
        Assert.NotNull(req);
        Assert.Equal("describe", req!.Action);
    }

    // ── /help / /start ───────────────────────────────────────────────────

    [Fact]
    public void ParseCommand_Help_ReturnsHelpCommand()
    {
        Assert.Equal("help", TelegramController.ParseCommand("/help")!.Command);
    }

    [Fact]
    public void ParseCommand_Start_MapsToHelp()
    {
        Assert.Equal("help", TelegramController.ParseCommand("/start")!.Command);
    }

    // ── @botname suffix stripping ─────────────────────────────────────────

    [Fact]
    public void ParseCommand_WithBotnameSuffix_Stripped()
    {
        var req = TelegramController.ParseCommand("/status@MyApexBot");
        Assert.NotNull(req);
        Assert.Equal("status", req!.Command);
    }

    // ── Unknown command fall-through ──────────────────────────────────────

    [Fact]
    public void ParseCommand_UnknownCommand_ReturnsCommandNameAndArgs()
    {
        var req = TelegramController.ParseCommand("/foobar some args");
        Assert.NotNull(req);
        Assert.Equal("foobar",    req!.Command);
        Assert.Equal("some args", req.Value);
    }

    // ── ParseKeyValues ────────────────────────────────────────────────────

    [Fact]
    public void ParseKeyValues_SimpleKeyValue_Parsed()
    {
        var d = CommandLineParser.Tokenize("key=value");
        Assert.Equal("value", d["key"]);
    }

    [Fact]
    public void ParseKeyValues_MultipleKeyValues_AllParsed()
    {
        var d = CommandLineParser.Tokenize("a=1 b=2 c=3");
        Assert.Equal("1", d["a"]);
        Assert.Equal("2", d["b"]);
        Assert.Equal("3", d["c"]);
    }

    [Fact]
    public void ParseKeyValues_QuotedValue_PreservesSpaces()
    {
        var d = CommandLineParser.Tokenize(@"window=""My Notepad""");
        Assert.Equal("My Notepad", d["window"]);
    }

    [Fact]
    public void ParseKeyValues_CaseInsensitiveKeys()
    {
        var d = CommandLineParser.Tokenize("Window=Calc");
        Assert.Equal("Calc", d["window"]);   // stored as-is; lookup is case-insensitive
        Assert.Equal("Calc", d["WINDOW"]);
    }

    [Fact]
    public void ParseKeyValues_EmptyInput_ReturnsEmptyDictionary()
    {
        var d = CommandLineParser.Tokenize("");
        Assert.Empty(d);
    }

    [Fact]
    public void ParseKeyValues_KeyWithoutEquals_StoredWithEmptyValue()
    {
        var d = CommandLineParser.Tokenize("flagonly");
        Assert.True(d.ContainsKey("flagonly"));
        Assert.Equal("", d["flagonly"]);
    }

    // ── DictExtensions.Get ────────────────────────────────────────────────

    [Fact]
    public void DictGet_FirstMatchingKey_Returned()
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["a"] = "alpha"
        };
        Assert.Equal("alpha", d.Get("a", "b", "c"));
    }

    [Fact]
    public void DictGet_SecondKey_UsedWhenFirstMissing()
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["b"] = "beta"
        };
        Assert.Equal("beta", d.Get("a", "b"));
    }

    [Fact]
    public void DictGet_AllKeysMissing_ReturnsNull()
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        Assert.Null(d.Get("x", "y", "z"));
    }

    [Fact]
    public void DictGet_WhitespaceValue_SkippedInFavorOfNextKey()
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["a"] = "   ",  // whitespace-only — treated as missing
            ["b"] = "beta"
        };
        Assert.Equal("beta", d.Get("a", "b"));
    }
}
