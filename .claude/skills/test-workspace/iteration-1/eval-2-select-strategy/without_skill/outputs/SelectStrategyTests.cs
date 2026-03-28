using Conjecture.Core;
using Conjecture.Core.Generation;
using Conjecture.Core.Internal;

namespace Conjecture.Tests.Strategies;

public class SelectStrategyTests
{
    private static ConjectureData MakeData(ulong seed = 42UL) =>
        ConjectureData.ForGeneration(new SplittableRandom(seed));

    [Fact]
    public void Select_DoublesValues()
    {
        var strategy = Gen.Integers<int>(0, 10).Select(x => x * 2);
        var data = MakeData();
        for (var i = 0; i < 100; i++)
        {
            var result = strategy.Next(data);
            Assert.True(result % 2 == 0, $"Expected even number, got {result}");
        }
    }

    [Fact]
    public void Select_IdentityReturnsOriginalValues()
    {
        var strategy = Gen.Integers<int>(0, 10).Select(x => x);
        var data = MakeData();
        for (var i = 0; i < 100; i++)
        {
            var result = strategy.Next(data);
            Assert.InRange(result, 0, 10);
        }
    }
}
