// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core;

namespace Conjecture.Core.Tests.Strategies;

public class IntegerStrategyTests
{
    [Fact]
    public void Integers_DefaultRange_Int_InRange()
    {
        Strategy<int> strategy = Strategy.Integers<int>();
        Assert.All(strategy.WithSeed(42UL).Sample(100), v => Assert.InRange(v, int.MinValue, int.MaxValue));
    }

    [Fact]
    public void Integers_BoundedRange_ReturnsInRange()
    {
        Strategy<int> strategy = Strategy.Integers<int>(0, 9);
        Assert.All(strategy.WithSeed(1UL).Sample(1000), v => Assert.InRange(v, 0, 9));
    }

    [Fact]
    public void Integers_MinEqualsMax_ReturnsConstant()
    {
        Strategy<int> strategy = Strategy.Integers<int>(5, 5);
        Assert.All(strategy.WithSeed(1UL).Sample(5), v => Assert.Equal(5, v));
    }

    [Fact]
    public void Integers_NegativeRange_ReturnsInRange()
    {
        Strategy<int> strategy = Strategy.Integers<int>(-10, -1);
        Assert.All(strategy.WithSeed(1UL).Sample(100), v => Assert.InRange(v, -10, -1));
    }

    [Fact]
    public void Integers_LongRange_ReturnsInRange()
    {
        Strategy<long> strategy = Strategy.Integers<long>(0L, 100L);
        Assert.All(strategy.WithSeed(1UL).Sample(100), v => Assert.InRange(v, 0L, 100L));
    }

    [Fact]
    public void Integers_ByteRange_ReturnsInRange()
    {
        Strategy<byte> strategy = Strategy.Integers<byte>(0, 10);
        Assert.All(strategy.WithSeed(1UL).Sample(100), v => Assert.InRange(v, (byte)0, (byte)10));
    }
}
