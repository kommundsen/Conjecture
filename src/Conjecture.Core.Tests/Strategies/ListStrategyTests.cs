// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Generic;
using System.Linq;

using Conjecture.Core;

namespace Conjecture.Core.Tests.Strategies;

public class ListStrategyTests
{
    [Fact]
    public void Lists_ProducesListOfCorrectType()
    {
        Strategy<List<int>> strategy = Strategy.Lists(Strategy.Integers<int>());
        Assert.IsType<List<int>>(strategy.Sample());
    }

    [Fact]
    public void Lists_DefaultSizeVariesAcrossSeeds()
    {
        Strategy<List<int>> strategy = Strategy.Lists(Strategy.Integers<int>());
        IReadOnlyList<List<int>> samples = strategy.WithSeed(0UL).Sample(200);
        HashSet<int> sizes = samples.Select(l => l.Count).ToHashSet();
        Assert.True(sizes.Count > 5, "List sizes should vary across seeds");
    }

    [Fact]
    public void Lists_DefaultSizeWithinRange()
    {
        Strategy<List<int>> strategy = Strategy.Lists(Strategy.Integers<int>());
        Assert.All(strategy.WithSeed(0UL).Sample(200), l => Assert.InRange(l.Count, 0, 100));
    }

    [Fact]
    public void Lists_RespectsMinSizeAndMaxSize()
    {
        Strategy<List<int>> strategy = Strategy.Lists(Strategy.Integers<int>(), minSize: 3, maxSize: 5);
        Assert.All(strategy.WithSeed(0UL).Sample(100), l => Assert.InRange(l.Count, 3, 5));
    }

    [Fact]
    public void Lists_EmptyListPossibleWhenMinSizeIsZero()
    {
        Strategy<List<int>> strategy = Strategy.Lists(Strategy.Integers<int>(), minSize: 0, maxSize: 10);
        IReadOnlyList<List<int>> samples = strategy.WithSeed(0UL).Sample(500);
        Assert.Contains(samples, l => l.Count == 0);
    }

    [Fact]
    public void Lists_DeterministicWithSameSeed()
    {
        Strategy<List<int>> strategy = Strategy.Lists(Strategy.Integers<int>());
        List<int> list1 = strategy.WithSeed(99UL).Sample();
        List<int> list2 = strategy.WithSeed(99UL).Sample();
        Assert.Equal(list1, list2);
    }

    [Fact]
    public void Lists_ElementsComefromInnerStrategy()
    {
        Strategy<List<int>> strategy = Strategy.Lists(Strategy.Integers<int>(min: 7, max: 7), minSize: 5, maxSize: 5);
        List<int> result = strategy.Sample();
        Assert.All(result, x => Assert.Equal(7, x));
    }
}
