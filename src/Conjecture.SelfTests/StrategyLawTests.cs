using Conjecture.Core;
using Conjecture.Core.Generation;
using Conjecture.Core.Internal;
using Conjecture.Xunit.V3;
using Xunit;

namespace Conjecture.SelfTests;

public class StrategyLawTests
{
    [Fact]
    public async Task FunctorIdentity_SelectIdentity_ProducesSameValue()
    {
        ConjectureSettings settings = new() { Seed = 0xC0FFEEul, MaxExamples = 1, UseDatabase = false };
        int baseline = 0;
        int mapped = 0;

        await TestRunner.Run(settings, data =>
        {
            baseline = Gen.Integers<int>().Next(data);
        });

        await TestRunner.Run(settings, data =>
        {
            mapped = Gen.Integers<int>().Select(x => x).Next(data);
        });

        Assert.Equal(baseline, mapped);
    }

    [Fact]
    public async Task FilterTrue_WhereAlwaysTrue_ProducesSameValue()
    {
        ConjectureSettings settings = new() { Seed = 0xDEADBEEFul, MaxExamples = 1, UseDatabase = false };
        int baseline = 0;
        int filtered = 0;

        await TestRunner.Run(settings, data =>
        {
            baseline = Gen.Integers<int>(0, 1000).Next(data);
        });

        await TestRunner.Run(settings, data =>
        {
            filtered = Gen.Integers<int>(0, 1000).Where(_ => true).Next(data);
        });

        Assert.Equal(baseline, filtered);
    }

    [Fact]
    public async Task FilterFalse_WhereNeverTrue_NoValidExamplesProduced()
    {
        // MaxExamples = 1 keeps the test fast; all attempts are filtered so no valid examples run.
        ConjectureSettings settings = new() { Seed = 1ul, MaxExamples = 1, UseDatabase = false };

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            _ = Gen.Integers<int>().Where(_ => false).Next(data);
        });

        Assert.True(result.Passed, "Runner should pass vacuously — no valid examples exist.");
        Assert.Equal(0, result.ExampleCount);
    }

    [Fact]
    public async Task SelectManyAssociativity_ConstantStrategies_BothSidesEqual()
    {
        ConjectureSettings settings = new() { Seed = 777ul, MaxExamples = 1, UseDatabase = false };

        // s: always 3, f: x -> x*3, g: y -> y+1  →  expected: (3*3)+1 = 10
        int left = 0;
        await TestRunner.Run(settings, data =>
        {
            left = Gen.Integers<int>(3, 3)
                      .SelectMany(x => Gen.Integers<int>(x * 3, x * 3))
                      .SelectMany(y => Gen.Integers<int>(y + 1, y + 1))
                      .Next(data);
        });

        int right = 0;
        await TestRunner.Run(settings, data =>
        {
            right = Gen.Integers<int>(3, 3)
                       .SelectMany(x => Gen.Integers<int>(x * 3, x * 3)
                                           .SelectMany(y => Gen.Integers<int>(y + 1, y + 1)))
                       .Next(data);
        });

        Assert.Equal(10, left);
        Assert.Equal(10, right);
    }

    private sealed class ListsOf2To8 : IStrategyProvider<List<int>>
    {
        public Strategy<List<int>> Create() => Gen.Lists(Gen.Integers<int>(), minSize: 2, maxSize: 8);
    }

    [Property(MaxExamples = 500)]
    public void ListSizeBoundsRespected([From<ListsOf2To8>] List<int> list)
    {
        Assert.True(list.Count >= 2, $"List size {list.Count} is below minSize 2");
        Assert.True(list.Count <= 8, $"List size {list.Count} exceeds maxSize 8");
    }
}
