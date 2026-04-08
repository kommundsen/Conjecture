// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core;

namespace Conjecture.Core.Tests;

public class DataGenTests
{
    [Fact]
    public void Sample_ReturnsCorrectCount()
    {
        var result = DataGen.Sample(Generate.Integers<int>(0, 100), count: 10, seed: 42UL);
        Assert.Equal(10, result.Count);
    }

    [Fact]
    public void Sample_SameSeed_ProducesSameOutput()
    {
        var strategy = Generate.Integers<int>(0, 100);
        var result1 = DataGen.Sample(strategy, count: 10, seed: 42UL);
        var result2 = DataGen.Sample(strategy, count: 10, seed: 42UL);
        Assert.Equal(result1, result2);
    }

    [Fact]
    public void Sample_DifferentSeeds_ProduceDifferentOutput()
    {
        var strategy = Generate.Integers<int>(0, 100);
        var result1 = DataGen.Sample(strategy, count: 10, seed: 1UL);
        var result2 = DataGen.Sample(strategy, count: 10, seed: 2UL);
        Assert.NotEqual(result1, result2);
    }

    [Fact]
    public void Stream_YieldsLazily()
    {
        var items = DataGen.Stream(Generate.Integers<int>(0, 100), count: 100, seed: 42UL)
            .Take(5)
            .ToList();

        Assert.Equal(5, items.Count);
    }
}