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

    [Fact]
    public void Integers_UInt_DefaultRange_InRange()
    {
        Strategy<uint> strategy = Strategy.Integers<uint>();
        Assert.All(strategy.WithSeed(1UL).Sample(100), v => Assert.InRange(v, uint.MinValue, uint.MaxValue));
    }

    [Fact]
    public void Integers_ULong_DefaultRange_InRange()
    {
        Strategy<ulong> strategy = Strategy.Integers<ulong>();
        Assert.All(strategy.WithSeed(1UL).Sample(100), v => Assert.InRange(v, ulong.MinValue, ulong.MaxValue));
    }

    [Fact]
    public void Integers_UShort_DefaultRange_InRange()
    {
        Strategy<ushort> strategy = Strategy.Integers<ushort>();
        Assert.All(strategy.WithSeed(1UL).Sample(100), v => Assert.InRange(v, ushort.MinValue, ushort.MaxValue));
    }

    [Fact]
    public void Integers_Short_DefaultRange_InRange()
    {
        Strategy<short> strategy = Strategy.Integers<short>();
        Assert.All(strategy.WithSeed(1UL).Sample(100), v => Assert.InRange(v, short.MinValue, short.MaxValue));
    }

    [Fact]
    public void Integers_SByte_DefaultRange_InRange()
    {
        Strategy<sbyte> strategy = Strategy.Integers<sbyte>();
        Assert.All(strategy.WithSeed(1UL).Sample(100), v => Assert.InRange(v, sbyte.MinValue, sbyte.MaxValue));
    }
}
