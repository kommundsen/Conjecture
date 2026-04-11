// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core;
using Conjecture.Core.Internal;

namespace Conjecture.Core.Tests.Strategies;

public class TimeOnlyStrategyTests
{
    private static ConjectureData MakeData(ulong seed = 42UL) =>
        ConjectureData.ForGeneration(new SplittableRandom(seed));

    [Fact]
    public void TimeOnlyValues_BoundedRange_ReturnsInRange()
    {
        TimeOnly min = new(6, 0, 0);
        TimeOnly max = new(18, 0, 0);
        Strategy<TimeOnly> strategy = Generate.TimeOnlyValues(min, max);
        ConjectureData data = MakeData();

        for (int i = 0; i < 1000; i++)
        {
            TimeOnly value = strategy.Generate(data);
            Assert.InRange(value, min, max);
        }
    }

    [Fact]
    public void TimeOnlyValues_DefaultRange_DoesNotThrow()
    {
        Strategy<TimeOnly> strategy = Generate.TimeOnlyValues();
        ConjectureData data = MakeData();

        for (int i = 0; i < 100; i++)
        {
            _ = strategy.Generate(data);
        }
    }

    [Fact]
    public void TimeOnlyValues_MinEqualsMax_ReturnsConstant()
    {
        TimeOnly t = new(14, 30, 0);
        Strategy<TimeOnly> strategy = Generate.TimeOnlyValues(t, t);
        ConjectureData data = MakeData();

        for (int i = 0; i < 20; i++)
        {
            Assert.Equal(t, strategy.Generate(data));
        }
    }

    [Fact]
    public void TimeOnlyValues_MinGreaterThanMax_Throws()
    {
        TimeOnly later = new(20, 0, 0);
        TimeOnly earlier = new(8, 0, 0);

        Assert.Throws<ArgumentOutOfRangeException>(() => Generate.TimeOnlyValues(later, earlier));
    }
}