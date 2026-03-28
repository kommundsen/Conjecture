using Conjecture.Core;
using Conjecture.Core.Generation;
using Conjecture.Core.Internal;

namespace Conjecture.Tests.Strategies;

public class SelectStrategyTests
{
    private static ConjectureData MakeData(ulong seed = 42UL) =>
        ConjectureData.ForGeneration(new SplittableRandom(seed));

    [Fact]
    public void Select_DoublingSelector_ReturnsEvenNumbersInRange()
    {
        var strategy = Gen.Integers<int>(0, 10).Select(x => x * 2);
        var data = MakeData();
        for (var i = 0; i < 100; i++)
        {
            var result = strategy.Next(data);
            Assert.InRange(result, 0, 20);
            Assert.Equal(0, result % 2);
        }
    }

    [Fact]
    public void Select_IdentitySelector_ReturnsValuesInSourceRange()
    {
        var strategy = Gen.Integers<int>(0, 10).Select(x => x);
        var data = MakeData();
        for (var i = 0; i < 100; i++)
        {
            Assert.InRange(strategy.Next(data), 0, 10);
        }
    }
}
