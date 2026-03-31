using Conjecture.Core;
using Conjecture.Core.Internal;
using Xunit;

using ShrinkEngine = Conjecture.Core.Internal.Shrinker.Shrinker;

namespace Conjecture.SelfTests;

public class ShrinkerInvariantTests
{
    private static Status Replay(IReadOnlyList<IRNode> nodes, Action<ConjectureData> predicate)
    {
        ConjectureData data = ConjectureData.ForRecord(nodes);
        try
        {
            predicate(data);
            return Status.Valid;
        }
        catch (UnsatisfiedAssumptionException)
        {
            return Status.Invalid;
        }
        catch (InvalidOperationException) when (data.Status == Status.Overrun)
        {
            return Status.Overrun;
        }
        catch
        {
            return Status.Interesting;
        }
    }

    private static bool IsLexicographicallyLeq(IReadOnlyList<IRNode> a, IReadOnlyList<IRNode> b)
    {
        int minLen = Math.Min(a.Count, b.Count);
        for (int i = 0; i < minLen; i++)
        {
            if (a[i].Value < b[i].Value) return true;
            if (a[i].Value > b[i].Value) return false;
        }
        return a.Count <= b.Count;
    }

    private static void FailIfOver10(ConjectureData data)
    {
        ulong v = data.DrawInteger(0, 1000);
        if (v > 10) throw new InvalidOperationException("too big");
    }

    private static void FailIfOver5(ConjectureData data)
    {
        ulong v = data.DrawInteger(0, 500);
        if (v > 5) throw new InvalidOperationException("too big");
    }

    [Fact]
    public async Task Idempotent_ReshrinkingFullyShrunkResult_MakesNoProgress()
    {
        ConjectureSettings settings = new() { Seed = 42ul, MaxExamples = 20, UseDatabase = false };

        TestRunResult result = await TestRunner.Run(settings, FailIfOver10);

        Assert.False(result.Passed);

        (IReadOnlyList<IRNode> _, int additionalShrinks) = await ShrinkEngine.ShrinkAsync(
            result.Counterexample!,
            nodes => new ValueTask<Status>(Replay(nodes, FailIfOver10)));

        Assert.Equal(0, additionalShrinks);
    }

    [Fact]
    public async Task PreservesFailure_ShrunkCounterexample_StillInteresting()
    {
        ConjectureSettings settings = new() { Seed = 1ul, MaxExamples = 20, UseDatabase = false };

        TestRunResult result = await TestRunner.Run(settings, FailIfOver5);

        Assert.False(result.Passed);
        Assert.Equal(Status.Interesting, Replay(result.Counterexample!, FailIfOver5));
    }

    [Fact]
    public async Task Reduces_ShrunkCounterexample_IsLexicographicallyLeqOriginal()
    {
        ConjectureSettings settings = new() { Seed = 99ul, MaxExamples = 20, UseDatabase = false };

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            ulong v = data.DrawInteger(0, 10000);
            if (v > 100) throw new InvalidOperationException("too big");
        });

        Assert.False(result.Passed);
        Assert.True(
            IsLexicographicallyLeq(result.Counterexample!, result.OriginalCounterexample!),
            "Shrunk counterexample must be lexicographically <= the original failing example.");
    }

    [Fact]
    public async Task BoundsRespected_ShrunkNodes_AllWithinStrategyBounds()
    {
        ConjectureSettings settings = new() { Seed = 7ul, MaxExamples = 20, UseDatabase = false };

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            ulong v = data.DrawInteger(50, 200);
            if (v > 75) throw new InvalidOperationException("too big");
        });

        Assert.False(result.Passed);

        foreach (IRNode node in result.Counterexample!)
        {
            Assert.True(node.Value >= node.Min,
                $"Node value {node.Value} is below its min bound {node.Min}");
            Assert.True(node.Value <= node.Max,
                $"Node value {node.Value} exceeds its max bound {node.Max}");
        }
    }
}
