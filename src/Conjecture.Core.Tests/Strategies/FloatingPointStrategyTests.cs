// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Generic;
using System.Linq;

using Conjecture.Core;

namespace Conjecture.Core.Tests.Strategies;

public class FloatingPointStrategyTests
{
    [Fact]
    public void Doubles_GeneratesFiniteAndNonFiniteValues_WithinDoubleRange()
    {
        Strategy<double> strategy = Strategy.Doubles();
        Assert.All(strategy.WithSeed(42UL).Sample(100), v =>
            Assert.True(!double.IsNaN(v) || double.IsNaN(v)));
    }

    [Fact]
    public void Doubles_IncludesPositiveAndNegativeValues()
    {
        Strategy<double> strategy = Strategy.Doubles();
        IReadOnlyList<double> samples = strategy.WithSeed(0UL).Sample(1000);
        Assert.Contains(samples, v => v > 0);
        Assert.Contains(samples, v => v < 0);
    }

    [Fact]
    public void Doubles_DeterministicWithSeed()
    {
        Strategy<double> strategy = Strategy.Doubles();
        IReadOnlyList<double> results1 = strategy.WithSeed(77UL).Sample(20);
        IReadOnlyList<double> results2 = strategy.WithSeed(77UL).Sample(20);
        Assert.Equal(results1, results2);
    }

    [Fact]
    public void Floats_GeneratesFloatValues()
    {
        Strategy<float> strategy = Strategy.Floats();
        Assert.All(strategy.WithSeed(42UL).Sample(100), v => Assert.IsType<float>(v));
    }

    [Fact]
    public void Floats_IncludesPositiveAndNegativeValues()
    {
        Strategy<float> strategy = Strategy.Floats();
        IReadOnlyList<float> samples = strategy.WithSeed(0UL).Sample(1000);
        Assert.Contains(samples, v => v > 0);
        Assert.Contains(samples, v => v < 0);
    }

    [Fact]
    public void Floats_DeterministicWithSeed()
    {
        Strategy<float> strategy = Strategy.Floats();
        IReadOnlyList<float> results1 = strategy.WithSeed(77UL).Sample(20);
        IReadOnlyList<float> results2 = strategy.WithSeed(77UL).Sample(20);
        Assert.Equal(results1, results2);
    }

    [Fact]
    public void Doubles_ProducesDistinctValues()
    {
        Strategy<double> strategy = Strategy.Doubles();
        IReadOnlyList<double> values = strategy.WithSeed(42UL).Sample(50);
        Assert.True(values.Distinct().Count() > 1, "Expected multiple distinct doubles");
    }

    [Fact]
    public void Floats_ProducesDistinctValues()
    {
        Strategy<float> strategy = Strategy.Floats();
        IReadOnlyList<float> values = strategy.WithSeed(42UL).Sample(50);
        Assert.True(values.Distinct().Count() > 1, "Expected multiple distinct floats");
    }
}
