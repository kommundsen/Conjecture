using Conjecture.Core;
using Conjecture.Core.Generation;
using Conjecture.Core.Internal;

namespace Conjecture.Tests.Strategies;

public class WithLabelTests
{
    private static ConjectureData MakeData(ulong seed = 42UL) =>
        ConjectureData.ForGeneration(new SplittableRandom(seed));

    [Fact]
    public void WithLabel_ReturnsStrategyWithCorrectLabel()
    {
        var labeled = Gen.Integers<int>().WithLabel("age");

        Assert.Equal("age", labeled.Label);
    }

    [Fact]
    public void WithLabel_UnlabeledStrategyHasNullLabel()
    {
        Strategy<int> strategy = Gen.Integers<int>();

        Assert.Null(strategy.Label);
    }

    [Fact]
    public void WithLabel_GenerationProducesValues()
    {
        var labeled = Gen.Integers<int>(0, 100).WithLabel("age");
        var data = MakeData();

        var value = labeled.Next(data);

        Assert.InRange(value, 0, 100);
    }

    [Fact]
    public void WithLabel_ProducesSameValuesAsUnlabeled()
    {
        var inner = Gen.Integers<int>();
        var labeled = inner.WithLabel("x");

        var dataA = MakeData(seed: 99UL);
        var dataB = MakeData(seed: 99UL);

        for (var i = 0; i < 50; i++)
        {
            Assert.Equal(inner.Next(dataA), labeled.Next(dataB));
        }
    }

    [Fact]
    public void WithLabel_LabelCanBeOverriddenByChaining()
    {
        var labeled = Gen.Integers<int>().WithLabel("first").WithLabel("second");

        Assert.Equal("second", labeled.Label);
    }
}
