using Conjecture.Core;
using Conjecture.Core.Generation;
using Conjecture.Core.Internal;

namespace Conjecture.Tests.Strategies;

public class BooleanStrategyTests
{
    private static ConjectureData MakeData(ulong seed = 42UL) =>
        ConjectureData.ForGeneration(new SplittableRandom(seed));

    [Fact]
    public void Booleans_ReturnsStrategy()
    {
        var strategy = Gen.Booleans();
        Assert.NotNull(strategy);
        Assert.IsAssignableFrom<Strategy<bool>>(strategy);
    }

    [Fact]
    public void Booleans_ReturnsBothValues()
    {
        var strategy = Gen.Booleans();
        var data = MakeData();
        var seenTrue = false;
        var seenFalse = false;

        for (var i = 0; i < 1000; i++)
        {
            if (strategy.Next(data)) { seenTrue = true; }
            else { seenFalse = true; }
            if (seenTrue && seenFalse) { break; }
        }

        Assert.True(seenTrue, "Booleans() never produced true");
        Assert.True(seenFalse, "Booleans() never produced false");
    }

    [Fact]
    public void BooleanStrategy_Next_RecordsIRNode()
    {
        var strategy = Gen.Booleans();
        var data = MakeData();

        strategy.Next(data);

        var node = Assert.Single(data.IRNodes);
        Assert.Equal(IRNodeKind.Boolean, node.Kind);
    }
}
