// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core;
using Conjecture.Core.Internal;

namespace Conjecture.Core.Tests.Strategies;

public class WithLabelTests
{
    private static ConjectureData MakeData(ulong seed = 42UL) =>
        ConjectureData.ForGeneration(new SplittableRandom(seed));

    [Fact]
    public void WithLabel_ReturnsStrategyWithCorrectLabel()
    {
        var labeled = Strategy.Integers<int>().WithLabel("age");

        Assert.Equal("age", labeled.Label);
    }

    [Fact]
    public void WithLabel_UnlabeledStrategyHasNullLabel()
    {
        Strategy<int> strategy = Strategy.Integers<int>();

        Assert.Null(strategy.Label);
    }

    [Fact]
    public void WithLabel_GenerationProducesValues()
    {
        var labeled = Strategy.Integers<int>(0, 100).WithLabel("age");
        var data = MakeData();

        var value = labeled.Generate(data);

        Assert.InRange(value, 0, 100);
    }

    [Fact]
    public void WithLabel_ProducesSameValuesAsUnlabeled()
    {
        var inner = Strategy.Integers<int>();
        var labeled = inner.WithLabel("x");

        var dataA = MakeData(seed: 99UL);
        var dataB = MakeData(seed: 99UL);

        for (var i = 0; i < 50; i++)
        {
            Assert.Equal(inner.Generate(dataA), labeled.Generate(dataB));
        }
    }

    [Fact]
    public void WithLabel_LabelCanBeOverriddenByChaining()
    {
        var labeled = Strategy.Integers<int>().WithLabel("first").WithLabel("second");

        Assert.Equal("second", labeled.Label);
    }
}