using ApexComputerUse;
using Xunit;

namespace ApexComputerUse.Tests;

public class FuzzyMatchPolicyTests
{
    [Fact]
    public void ExactMatch_IsAcceptedAsExact()
    {
        var ranked = ApexHelper.RankTextCandidates("Debug Target", new[]
        {
            ("Debug Target", "", "SplitButton"),
            ("Start Without Debugging", "", "Button")
        });

        Assert.Equal(FuzzyMatchStatus.Exact, ApexHelper.ClassifyRankedCandidates(ranked));
        Assert.Equal("Debug Target", ranked[0].Name);
        Assert.Equal("exact", ranked[0].MatchType);
    }

    [Fact]
    public void Start_DoesNotAutoSelectSearch()
    {
        var ranked = ApexHelper.RankTextCandidates("Start", new[]
        {
            ("Search", "", "Button")
        });

        Assert.Equal(FuzzyMatchStatus.LowConfidence, ApexHelper.ClassifyRankedCandidates(ranked));
        Assert.Equal("Search", ranked[0].Name);
        Assert.Equal("fuzzy", ranked[0].MatchType);
    }

    [Fact]
    public void AmbiguousPrefixMatches_ReturnCandidates()
    {
        var ranked = ApexHelper.RankTextCandidates("Start", new[]
        {
            ("Start Debugging", "", "Button"),
            ("Start Without Debugging", "", "Button")
        });

        Assert.Equal(FuzzyMatchStatus.Ambiguous, ApexHelper.ClassifyRankedCandidates(ranked));
        Assert.Equal(2, ranked.Count);
    }

    [Fact]
    public void ClearPrefixMatch_IsAccepted()
    {
        var ranked = ApexHelper.RankTextCandidates("Debug Target", new[]
        {
            ("Debug Target selector", "", "SplitButton"),
            ("Start Without Debugging", "", "Button")
        });

        Assert.Equal(FuzzyMatchStatus.Accepted, ApexHelper.ClassifyRankedCandidates(ranked));
        Assert.Equal("Debug Target selector", ranked[0].Name);
        Assert.Equal("prefix", ranked[0].MatchType);
    }
}
