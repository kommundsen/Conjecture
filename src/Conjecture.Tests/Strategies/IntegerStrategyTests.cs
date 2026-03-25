using Conjecture.Core;
using Conjecture.Core.Internal;

namespace Conjecture.Tests.Generation;

public class IntegerStrategyTests
{
    private static ConjectureData MakeData(ulong seed = 42UL) =>
        ConjectureData.ForGeneration(new SplittableRandom(seed));

    [Fact]
    public void Integers_DefaultRange_Int_InRange()
    {
        var strategy = Gen.Integers<int>();
        var data = MakeData();
        for (var i = 0; i < 100; i++)
            Assert.InRange(strategy.Next(data), int.MinValue, int.MaxValue);
    }

    [Fact]
    public void Integers_BoundedRange_ReturnsInRange()
    {
        var strategy = Gen.Integers<int>(0, 9);
        var data = MakeData();
        for (var i = 0; i < 1000; i++)
            Assert.InRange(strategy.Next(data), 0, 9);
    }

    [Fact]
    public void Integers_MinEqualsMax_ReturnsConstant()
    {
        var strategy = Gen.Integers<int>(5, 5);
        var data = MakeData();
        for (var i = 0; i < 20; i++)
            Assert.Equal(5, strategy.Next(data));
    }

    [Fact]
    public void Integers_NegativeRange_ReturnsInRange()
    {
        var strategy = Gen.Integers<int>(-10, -1);
        var data = MakeData();
        for (var i = 0; i < 100; i++)
            Assert.InRange(strategy.Next(data), -10, -1);
    }

    [Fact]
    public void Integers_LongRange_ReturnsInRange()
    {
        var strategy = Gen.Integers<long>(0L, 100L);
        var data = MakeData();
        for (var i = 0; i < 100; i++)
            Assert.InRange(strategy.Next(data), 0L, 100L);
    }

    [Fact]
    public void Integers_ByteRange_ReturnsInRange()
    {
        var strategy = Gen.Integers<byte>(0, 10);
        var data = MakeData();
        for (var i = 0; i < 100; i++)
            Assert.InRange(strategy.Next(data), (byte)0, (byte)10);
    }
}
