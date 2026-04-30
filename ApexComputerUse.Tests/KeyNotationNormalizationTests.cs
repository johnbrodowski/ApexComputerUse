using ApexComputerUse;
using Xunit;

namespace ApexComputerUse.Tests;

public class KeyNotationNormalizationTests
{
    [Theory]
    [InlineData("[Enter]", "{Enter}")]
    [InlineData("https://www.site.com[Enter]", "https://www.site.com{Enter}")]
    [InlineData("[CTRL][A]", "{CTRL}{A}")]
    [InlineData("Ctrl+[Enter]", "Ctrl+{Enter}")]
    [InlineData("[ Enter ]", "{Enter}")]
    public void ConvertsRecognizedBracketTokens(string input, string expected)
        => Assert.Equal(expected, ApexHelper.NormalizeBracketKeyTokens(input));

    [Theory]
    [InlineData("literal [notakey] text")]
    [InlineData("[")]
    [InlineData("[]")]
    [InlineData("no special tokens")]
    public void LeavesUnknownOrInvalidBracketTextUnchanged(string input)
        => Assert.Equal(input, ApexHelper.NormalizeBracketKeyTokens(input));
}
