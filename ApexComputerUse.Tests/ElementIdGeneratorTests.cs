using ApexComputerUse;
using Xunit;

namespace ApexComputerUse.Tests;

/// <summary>
/// Unit tests for <see cref="ElementIdGenerator"/>.
/// These tests cover hash-based mode, incremental mode, Reset(), and thread safety.
/// No FlaUI / UIA3 dependency — all helpers under test are pure computation.
/// </summary>
public class ElementIdGeneratorTests
{
    // ── Hash-based mode (default: UseIncrementalIds = false) ──────────────

    [Fact]
    public void HashMode_SameHash_ReturnsSameId()
    {
        var gen  = new ElementIdGenerator { UseIncrementalIds = false };
        string h = "abcdef01" + new string('0', 24); // 32 hex chars
        Assert.Equal(gen.GenerateIdFromHash(h), gen.GenerateIdFromHash(h));
    }

    [Fact]
    public void HashMode_DifferentFirstEightChars_DifferentIds()
    {
        var gen = new ElementIdGenerator { UseIncrementalIds = false };
        // ConvertHashToId takes first 8 hex chars — ensure those differ
        int id1 = gen.GenerateIdFromHash("00000001" + new string('0', 24));
        int id2 = gen.GenerateIdFromHash("00000002" + new string('0', 24));
        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public void HashMode_ResultIsNonNegative()
    {
        // Implementation calls Math.Abs on the converted hex value.
        // 0x80000000 would overflow as signed, but the substring is only 8 chars = max 0xFFFFFFFF.
        // Math.Abs of any parsed value should be ≥ 0.
        var gen = new ElementIdGenerator { UseIncrementalIds = false };
        int id  = gen.GenerateIdFromHash("ffffffff" + new string('0', 24));
        Assert.True(id >= 0, $"Expected non-negative ID, got {id}");
    }

    [Fact]
    public void HashMode_DeterministicAcrossInstances()
    {
        // Hash-based IDs depend only on the hash string, not on instance state.
        string hash = "1a2b3c4d" + new string('0', 24);
        var g1 = new ElementIdGenerator { UseIncrementalIds = false };
        var g2 = new ElementIdGenerator { UseIncrementalIds = false };
        Assert.Equal(g1.GenerateIdFromHash(hash), g2.GenerateIdFromHash(hash));
    }

    // ── Incremental mode (UseIncrementalIds = true) ───────────────────────

    [Fact]
    public void IncrementalMode_FirstTwoHashes_GetIds1And2()
    {
        var gen = new ElementIdGenerator { UseIncrementalIds = true };
        int id1 = gen.GenerateIdFromHash("alpha");
        int id2 = gen.GenerateIdFromHash("beta");
        Assert.Equal(1, id1);
        Assert.Equal(2, id2);
    }

    [Fact]
    public void IncrementalMode_SameHash_ReturnsSameId()
    {
        var gen  = new ElementIdGenerator { UseIncrementalIds = true };
        string h = "repeated_hash";
        int first  = gen.GenerateIdFromHash(h);
        int second = gen.GenerateIdFromHash(h);
        int third  = gen.GenerateIdFromHash(h);
        Assert.Equal(first, second);
        Assert.Equal(first, third);
    }

    [Fact]
    public void IncrementalMode_ForceFlag_OverridesHashMode()
    {
        // Even when UseIncrementalIds = false, forceUseIncrementalIds = true
        // should assign sequential IDs tracked in the dictionary.
        var gen = new ElementIdGenerator { UseIncrementalIds = false };
        int id = gen.GenerateIdFromHash("forced", forceUseIncrementalIds: true);
        Assert.Equal(1, id);
    }

    [Fact]
    public void IncrementalMode_CountersMatchAssignedIds()
    {
        var gen = new ElementIdGenerator { UseIncrementalIds = true };
        gen.GenerateIdFromHash("h1");
        gen.GenerateIdFromHash("h2");
        gen.GenerateIdFromHash("h1"); // duplicate — no new ID

        Assert.Equal(2, gen.CurrentMaxId);
        Assert.Equal(2, gen.UniqueHashCount);
    }

    // ── Reset ─────────────────────────────────────────────────────────────

    [Fact]
    public void Reset_ClearsAllState()
    {
        var gen = new ElementIdGenerator { UseIncrementalIds = true };
        gen.GenerateIdFromHash("a");
        gen.GenerateIdFromHash("b");
        gen.GenerateIdFromHash("c");

        gen.Reset();

        Assert.Equal(0, gen.CurrentMaxId);
        Assert.Equal(0, gen.UniqueHashCount);
    }

    [Fact]
    public void Reset_AllowsReuseOfId1()
    {
        var gen = new ElementIdGenerator { UseIncrementalIds = true };
        gen.GenerateIdFromHash("first");
        gen.GenerateIdFromHash("second");
        gen.Reset();

        int id = gen.GenerateIdFromHash("first"); // same hash, but reset cleared it
        Assert.Equal(1, id);
    }

    // ── CurrentMaxId / UniqueHashCount properties ─────────────────────────

    [Fact]
    public void CurrentMaxId_InitiallyZero()
    {
        var gen = new ElementIdGenerator { UseIncrementalIds = true };
        Assert.Equal(0, gen.CurrentMaxId);
    }

    [Fact]
    public void UniqueHashCount_InitiallyZero()
    {
        var gen = new ElementIdGenerator { UseIncrementalIds = true };
        Assert.Equal(0, gen.UniqueHashCount);
    }

    [Fact]
    public void UniqueHashCount_DoesNotCountDuplicates()
    {
        var gen = new ElementIdGenerator { UseIncrementalIds = true };
        for (int i = 0; i < 5; i++)
            gen.GenerateIdFromHash("always-the-same");
        Assert.Equal(1, gen.UniqueHashCount);
    }

    // ── Thread safety ─────────────────────────────────────────────────────

    [Fact]
    public async Task IncrementalMode_ConcurrentAccess_NoExceptionsAndCorrectCount()
    {
        const int threadCount = 50;
        var gen = new ElementIdGenerator { UseIncrementalIds = true };
        await Task.WhenAll(Enumerable.Range(0, threadCount)
            .Select(i => Task.Run(() => gen.GenerateIdFromHash($"hash_{i:D4}"))));

        Assert.Equal(threadCount, gen.UniqueHashCount);
        Assert.Equal(threadCount, gen.CurrentMaxId);
    }

    [Fact]
    public async Task HashMode_ConcurrentAccess_NoExceptions()
    {
        var gen = new ElementIdGenerator { UseIncrementalIds = false };
        Exception? caught = null;
        try
        {
            await Task.WhenAll(Enumerable.Range(0, 50)
                .Select(i => Task.Run(() => gen.GenerateIdFromHash($"{i:x8}" + new string('0', 24)))));
        }
        catch (Exception ex) { caught = ex; }
        Assert.Null(caught);
    }
}
