using ApexComputerUse;
using Xunit;

namespace ApexComputerUse.Tests;

/// <summary>
/// Unit tests for <see cref="ApexHelper.Levenshtein"/>.
/// Verifies correctness of the standard edit-distance algorithm.
/// No FlaUI / UI automation dependency.
/// </summary>
public class LevenshteinTests
{
    // ── Boundary conditions ───────────────────────────────────────────────

    [Fact]
    public void BothEmpty_ReturnsZero()
        => Assert.Equal(0, ApexHelper.Levenshtein("", ""));

    [Fact]
    public void EmptyA_ReturnsBLength()
        => Assert.Equal(5, ApexHelper.Levenshtein("", "hello"));

    [Fact]
    public void EmptyB_ReturnsALength()
        => Assert.Equal(5, ApexHelper.Levenshtein("world", ""));

    [Fact]
    public void IdenticalStrings_ReturnsZero()
        => Assert.Equal(0, ApexHelper.Levenshtein("notepad", "notepad"));

    // ── Single-operation edits ────────────────────────────────────────────

    [Fact]
    public void OneDeletion_ReturnsOne()
        => Assert.Equal(1, ApexHelper.Levenshtein("kitten", "itten"));

    [Fact]
    public void OneInsertion_ReturnsOne()
        => Assert.Equal(1, ApexHelper.Levenshtein("cat", "cats"));

    [Fact]
    public void OneSubstitution_ReturnsOne()
        => Assert.Equal(1, ApexHelper.Levenshtein("cat", "bat"));

    // ── Classic examples ──────────────────────────────────────────────────

    [Fact]
    public void KittenToSitting_Returns3()
        => Assert.Equal(3, ApexHelper.Levenshtein("kitten", "sitting"));

    [Fact]
    public void SaturdayToSunday_Returns3()
        => Assert.Equal(3, ApexHelper.Levenshtein("saturday", "sunday"));

    // ── Symmetry ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData("abc",  "xyz")]
    [InlineData("notepad", "calc")]
    [InlineData("hello", "")]
    public void IsSymmetric(string a, string b)
        => Assert.Equal(ApexHelper.Levenshtein(a, b), ApexHelper.Levenshtein(b, a));

    // ── Application-domain cases ──────────────────────────────────────────

    [Fact]
    public void NotepadVsNotepadPlusPlus_SmallDistance()
    {
        int d = ApexHelper.Levenshtein("notepad", "notepad++");
        Assert.Equal(2, d);
    }

    [Fact]
    public void CompletelyDifferentWords_LargeDistance()
    {
        int d = ApexHelper.Levenshtein("abcdef", "xyz");
        Assert.True(d >= 3);
    }

    [Fact]
    public void SingleCharStrings_SubstitutionIsOne()
        => Assert.Equal(1, ApexHelper.Levenshtein("a", "b"));

    [Fact]
    public void SingleCharStrings_SameIsZero()
        => Assert.Equal(0, ApexHelper.Levenshtein("z", "z"));
}
