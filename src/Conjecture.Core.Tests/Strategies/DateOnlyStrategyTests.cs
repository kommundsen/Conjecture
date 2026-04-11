// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core;
using Conjecture.Core.Internal;

namespace Conjecture.Core.Tests.Strategies;

public class DateOnlyStrategyTests
{
    private static ConjectureData MakeData(ulong seed = 42UL) =>
        ConjectureData.ForGeneration(new SplittableRandom(seed));

    [Fact]
    public void DateOnlyValues_BoundedRange_ReturnsInRange()
    {
        DateOnly min = new(2000, 1, 1);
        DateOnly max = new(2030, 12, 31);
        Strategy<DateOnly> strategy = Generate.DateOnlyValues(min, max);
        ConjectureData data = MakeData();

        for (int i = 0; i < 1000; i++)
        {
            DateOnly value = strategy.Generate(data);
            Assert.InRange(value, min, max);
        }
    }

    [Fact]
    public void DateOnlyValues_DefaultRange_DoesNotThrow()
    {
        Strategy<DateOnly> strategy = Generate.DateOnlyValues();
        ConjectureData data = MakeData();

        for (int i = 0; i < 100; i++)
        {
            _ = strategy.Generate(data);
        }
    }

    [Fact]
    public void DateOnlyValues_MinEqualsMax_ReturnsConstant()
    {
        DateOnly t = new(2025, 3, 14);
        Strategy<DateOnly> strategy = Generate.DateOnlyValues(t, t);
        ConjectureData data = MakeData();

        for (int i = 0; i < 20; i++)
        {
            Assert.Equal(t, strategy.Generate(data));
        }
    }

    [Fact]
    public void DateOnlyValues_MinGreaterThanMax_Throws()
    {
        DateOnly later = new(2030, 1, 1);
        DateOnly earlier = new(2020, 1, 1);

        Assert.Throws<ArgumentOutOfRangeException>(() => Generate.DateOnlyValues(later, earlier));
    }
}