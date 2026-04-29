// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core;
using Conjecture.Core.Internal;

namespace Conjecture.Core.Tests.Strategies;

public class IntegerStrategyTests
{
    private static ConjectureData MakeData(ulong seed = 42UL) =>
        ConjectureData.ForGeneration(new SplittableRandom(seed));

    [Fact]
    public void Integers_DefaultRange_Int_InRange()
    {
        var strategy = Strategy.Integers<int>();
        var data = MakeData();
        for (var i = 0; i < 100; i++)
        {
            Assert.InRange(strategy.Generate(data), int.MinValue, int.MaxValue);
        }
    }

    [Fact]
    public void Integers_BoundedRange_ReturnsInRange()
    {
        var strategy = Strategy.Integers<int>(0, 9);
        var data = MakeData();
        for (var i = 0; i < 1000; i++)
        {
            Assert.InRange(strategy.Generate(data), 0, 9);
        }
    }

    [Fact]
    public void Integers_MinEqualsMax_ReturnsConstant()
    {
        var strategy = Strategy.Integers<int>(5, 5);
        var data = MakeData();
        for (var i = 0; i < 20; i++)
        {
            Assert.Equal(5, strategy.Generate(data));
        }
    }

    [Fact]
    public void Integers_NegativeRange_ReturnsInRange()
    {
        var strategy = Strategy.Integers<int>(-10, -1);
        var data = MakeData();
        for (var i = 0; i < 100; i++)
        {
            Assert.InRange(strategy.Generate(data), -10, -1);
        }
    }

    [Fact]
    public void Integers_LongRange_ReturnsInRange()
    {
        var strategy = Strategy.Integers<long>(0L, 100L);
        var data = MakeData();
        for (var i = 0; i < 100; i++)
        {
            Assert.InRange(strategy.Generate(data), 0L, 100L);
        }
    }

    [Fact]
    public void Integers_ByteRange_ReturnsInRange()
    {
        var strategy = Strategy.Integers<byte>(0, 10);
        var data = MakeData();
        for (var i = 0; i < 100; i++)
        {
            Assert.InRange(strategy.Generate(data), (byte)0, (byte)10);
        }
    }
}