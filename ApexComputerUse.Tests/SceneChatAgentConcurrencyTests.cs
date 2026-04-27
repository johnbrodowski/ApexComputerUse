using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ApexComputerUse.Tests;

/// <summary>
/// Validates the turn-serialization pattern used in SceneChatAgent.
/// SceneChatAgent requires WinForms/service dependencies and cannot be
/// directly unit-tested; these tests exercise the SemaphoreSlim(1,1)
/// gate pattern it now uses to prevent concurrent-turn buffer corruption.
/// </summary>
public class SceneChatAgentConcurrencyTests
{
    [Fact]
    public async Task TurnGate_SemaphoreSlim1_SerializesOverlappingCalls()
    {
        var gate = new SemaphoreSlim(1, 1);
        int active = 0;
        int maxActive = 0;
        var completions = new List<string>();

        async Task SimulateTurn(string id)
        {
            await gate.WaitAsync();
            try
            {
                var current = Interlocked.Increment(ref active);
                // atomically update max
                int prev;
                do { prev = maxActive; } while (current > prev &&
                    Interlocked.CompareExchange(ref maxActive, current, prev) != prev);

                await Task.Delay(5); // simulate streaming tokens
                completions.Add(id);
                Interlocked.Decrement(ref active);
            }
            finally
            {
                gate.Release();
            }
        }

        await Task.WhenAll(
            SimulateTurn("a"), SimulateTurn("b"), SimulateTurn("c"),
            SimulateTurn("d"), SimulateTurn("e"));

        Assert.Equal(1, maxActive);
        Assert.Equal(5, completions.Count);
    }

    [Fact]
    public async Task TurnGate_PerTurnLocalBuffer_IsolatesBuffersAcrossTurns()
    {
        var gate = new SemaphoreSlim(1, 1);
        var captured = new List<string>();

        async Task SimulateTurn(string token)
        {
            await gate.WaitAsync();
            try
            {
                // Each turn has its own local buffer — no shared mutable state.
                var localBuffer = new System.Text.StringBuilder();
                localBuffer.Append(token);
                await Task.Delay(2);
                captured.Add(localBuffer.ToString());
            }
            finally
            {
                gate.Release();
            }
        }

        await Task.WhenAll(
            SimulateTurn("x"), SimulateTurn("y"), SimulateTurn("z"));

        // Each buffer should contain exactly one token — no cross-contamination.
        Assert.All(captured, s => Assert.Equal(1, s.Length));
        Assert.Equal(3, captured.Count);
    }
}
