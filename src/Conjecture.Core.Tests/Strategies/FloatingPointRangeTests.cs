// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core;
using Conjecture.Core.Internal;

namespace Conjecture.Core.Tests.Strategies;

public class FloatingPointRangeTests
{
    private static ConjectureData MakeData(ulong seed = 42UL) =>
        ConjectureData.ForGeneration(new SplittableRandom(seed));

    [Fact]
    public void Doubles_Range_StaysWithinBounds()
    {
        var strategy = Generate.Doubles(0.0, 1.0);
        var data = MakeData();

        for (var i = 0; i < 1000; i++)
        {
            Assert.InRange(strategy.Generate(data), 0.0, 1.0);
        }
    }

    [Fact]
    public void Doubles_NegativeRange_StaysWithinBounds()
    {
        var strategy = Generate.Doubles(-100.0, -1.0);
        var data = MakeData();

        for (var i = 0; i < 1000; i++)
        {
            Assert.InRange(strategy.Generate(data), -100.0, -1.0);
        }
    }

    [Fact]
    public void Doubles_MinEqualsMax_ReturnsConstant()
    {
        var strategy = Generate.Doubles(3.14, 3.14);
        var data = MakeData();

        for (var i = 0; i < 20; i++)
        {
            Assert.Equal(3.14, strategy.Generate(data));
        }
    }

    [Fact]
    public void Doubles_MinGreaterThanMax_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => Generate.Doubles(1.0, 0.0));
    }

    [Fact]
    public void Floats_Range_StaysWithinBounds()
    {
        var strategy = Generate.Floats(-1f, 1f);
        var data = MakeData();

        for (var i = 0; i < 1000; i++)
        {
            Assert.InRange(strategy.Generate(data), -1f, 1f);
        }
    }

    [Fact]
    public void Floats_MinEqualsMax_ReturnsConstant()
    {
        var strategy = Generate.Floats(2.5f, 2.5f);
        var data = MakeData();

        for (var i = 0; i < 20; i++)
        {
            Assert.Equal(2.5f, strategy.Generate(data));
        }
    }

    [Fact]
    public void Floats_MinGreaterThanMax_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => Generate.Floats(1f, 0f));
    }

    [Fact]
    public void Doubles_Range_ProducesDistinctValues()
    {
        var strategy = Generate.Doubles(0.0, 1.0);
        var data = MakeData();
        var values = Enumerable.Range(0, 50).Select(_ => strategy.Generate(data)).ToList();
        Assert.True(values.Distinct().Count() > 1, "Expected multiple distinct values in range");
    }

    [Fact]
    public void Floats_Range_ProducesDistinctValues()
    {
        var strategy = Generate.Floats(0f, 1f);
        var data = MakeData();
        var values = Enumerable.Range(0, 50).Select(_ => strategy.Generate(data)).ToList();
        Assert.True(values.Distinct().Count() > 1, "Expected multiple distinct values in range");
    }
}