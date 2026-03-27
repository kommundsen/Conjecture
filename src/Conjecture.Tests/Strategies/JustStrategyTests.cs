using Conjecture.Core;
using Conjecture.Core.Internal;
using Conjecture.Core.Generation;

namespace Conjecture.Tests.Generation;

public class JustStrategyTests
{
    private static ConjectureData MakeData(ulong seed = 42UL) =>
        ConjectureData.ForGeneration(new SplittableRandom(seed));

    [Fact]
    public void Just_Int_AlwaysReturnsSameValue()
    {
        var strategy = Gen.Just(42);
        var data = MakeData();

        for (var i = 0; i < 20; i++)
            Assert.Equal(42, strategy.Next(data));
    }

    [Fact]
    public void Just_String_AlwaysReturnsSameValue()
    {
        var strategy = Gen.Just("hello");
        var data = MakeData();

        for (var i = 0; i < 20; i++)
            Assert.Equal("hello", strategy.Next(data));
    }

    [Fact]
    public void Just_DrawsZeroIRNodes()
    {
        var strategy = Gen.Just(42);
        var data = MakeData();

        strategy.Next(data);

        Assert.Empty(data.IRNodes);
    }

    [Fact]
    public void Just_WorksWithSelectCombinator()
    {
        var strategy = Gen.Just(42).Select(x => x * 2);
        var data = MakeData();

        var result = strategy.Next(data);

        Assert.Equal(84, result);
    }
}
