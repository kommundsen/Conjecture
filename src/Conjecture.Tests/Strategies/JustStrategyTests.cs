using Conjecture.Core;
using Conjecture.Core.Internal;

namespace Conjecture.Tests.Strategies;

public class JustStrategyTests
{
    private static ConjectureData MakeData(ulong seed = 42UL) =>
        ConjectureData.ForGeneration(new SplittableRandom(seed));

    [Fact]
    public void Just_Int_AlwaysReturnsSameValue()
    {
        var strategy = Generate.Just(42);
        var data = MakeData();

        for (var i = 0; i < 20; i++)
        {
            Assert.Equal(42, strategy.Generate(data));
        }
    }

    [Fact]
    public void Just_String_AlwaysReturnsSameValue()
    {
        var strategy = Generate.Just("hello");
        var data = MakeData();

        for (var i = 0; i < 20; i++)
        {
            Assert.Equal("hello", strategy.Generate(data));
        }
    }

    [Fact]
    public void Just_DrawsZeroIRNodes()
    {
        var strategy = Generate.Just(42);
        var data = MakeData();

        strategy.Generate(data);

        Assert.Empty(data.IRNodes);
    }

    [Fact]
    public void Just_WorksWithSelectCombinator()
    {
        var strategy = Generate.Just(42).Select(x => x * 2);
        var data = MakeData();

        var result = strategy.Generate(data);

        Assert.Equal(84, result);
    }
}
