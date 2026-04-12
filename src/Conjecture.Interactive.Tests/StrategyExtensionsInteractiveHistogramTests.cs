// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core;

namespace Conjecture.Interactive.Tests;

public class StrategyExtensionsInteractiveHistogramTests
{
    [Fact]
    public void Histogram_DoubleStrategy_ReturnsSvgString()
    {
        Strategy<double> strategy = Generate.Doubles(0.0, 100.0);

        string svg = strategy.Histogram(sampleSize: 200, seed: 1UL);

        Assert.Contains("<svg", svg, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Histogram_IntStrategy_ReturnsSvgString()
    {
        Strategy<int> strategy = Generate.Integers<int>(1, 100);

        string svg = strategy.Histogram(sampleSize: 200, seed: 1UL);

        Assert.Contains("<svg", svg, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Histogram_SelectorOverload_ReturnsSvgString()
    {
        Strategy<string> strategy = Generate.Strings(minLength: 1, maxLength: 20);

        string svg = strategy.Histogram(static x => (double)x.Length, sampleSize: 200, seed: 1UL);

        Assert.Contains("<svg", svg, StringComparison.OrdinalIgnoreCase);
    }
}