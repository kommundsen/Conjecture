using Conjecture.Core;
using Conjecture.Core.Internal;
using Conjecture.Core.Generation;

namespace Conjecture.Tests.Strategies;

public class SelectManyStrategyTests
{
    private static ConjectureData MakeData(ulong seed = 42UL) =>
        ConjectureData.ForGeneration(new SplittableRandom(seed));

    [Fact]
    public void SelectMany_DependentGeneration()
    {
        var strategy = Gen.Integers<int>(1, 5).SelectMany(n => Gen.Integers<int>(0, n));
        var data = MakeData();
        for (var i = 0; i < 100; i++)
        {
            Assert.InRange(strategy.Next(data), 0, 5);
        }
    }

    [Fact]
    public void SelectMany_QuerySyntax()
    {
        var strategy =
            from x in Gen.Integers<int>(1, 3)
            from y in Gen.Integers<int>(1, x)
            select x * 10 + y;

        var data = MakeData();
        for (var i = 0; i < 100; i++)
        {
            var result = strategy.Next(data);
            // Valid results: 11, 21, 22, 31, 32, 33
            Assert.Contains(result, new[] { 11, 21, 22, 31, 32, 33 });
        }
    }

    [Fact]
    public void SelectMany_RecordsMultipleIRNodes()
    {
        var data = MakeData();
        Gen.Integers<int>(1, 5).SelectMany(n => Gen.Integers<int>(0, n)).Next(data);
        Assert.True(data.IRNodes.Count >= 2, $"Expected >= 2 IR nodes, got {data.IRNodes.Count}");
    }
}
