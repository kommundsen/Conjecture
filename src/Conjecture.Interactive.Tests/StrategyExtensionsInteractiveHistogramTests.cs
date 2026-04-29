// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core;

namespace Conjecture.Interactive.Tests;

public class StrategyExtensionsInteractiveHistogramTests
{
    [Fact]
    public void Histogram_DoubleStrategy_ReturnsTextHistogram()
    {
        Strategy<double> strategy = Strategy.Doubles(0.0, 100.0);

        string text = strategy.Histogram(sampleSize: 200, seed: 1UL);

        Assert.Contains("█", text);
        Assert.Contains("│", text);
    }

    [Fact]
    public void Histogram_IntStrategy_ReturnsTextHistogram()
    {
        Strategy<int> strategy = Strategy.Integers<int>(1, 100);

        string text = strategy.Histogram(sampleSize: 200, seed: 1UL);

        Assert.Contains("█", text);
        Assert.Contains("│", text);
    }

    [Fact]
    public void Histogram_SelectorOverload_ReturnsTextHistogram()
    {
        Strategy<string> strategy = Strategy.Strings(minLength: 1, maxLength: 20);

        string text = strategy.Histogram(static x => (double)x.Length, sampleSize: 200, seed: 1UL);

        Assert.Contains("█", text);
        Assert.Contains("│", text);
    }
}