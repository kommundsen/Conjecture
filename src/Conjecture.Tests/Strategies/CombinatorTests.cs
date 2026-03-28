using Conjecture.Core;
using Conjecture.Core.Internal;
using Conjecture.Core.Generation;

namespace Conjecture.Tests.Strategies;

public class CombinatorTests
{
    private static ConjectureData MakeData(ulong seed = 42UL) =>
        ConjectureData.ForGeneration(new SplittableRandom(seed));

    [Fact]
    public void Select_TransformsValue()
    {
        var strategy = Gen.Integers<int>(1, 10).Select(x => x * 2);
        var data = MakeData();
        for (var i = 0; i < 100; i++)
        {
            var value = strategy.Next(data);
            Assert.True(value % 2 == 0, "Expected even value");
            Assert.InRange(value, 2, 20);
        }
    }

    [Fact]
    public void Select_ChainedSelect()
    {
        var strategy = Gen.Integers<int>(0, 5).Select(x => x + 1).Select(x => x.ToString());
        var data = MakeData();
        var valid = new[] { "1", "2", "3", "4", "5", "6" };
        for (var i = 0; i < 50; i++)
        {
            Assert.Contains(strategy.Next(data), valid);
        }
    }

    [Fact]
    public void Select_PreservesIRNodeCount()
    {
        var data = MakeData();
        Gen.Integers<int>(0, 9).Select(x => x * 2).Next(data);
        var node = Assert.Single(data.IRNodes);
        Assert.Equal(IRNodeKind.Integer, node.Kind);
    }
}
