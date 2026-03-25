using Conjecture.Core;
using Conjecture.Core.Internal;
using Conjecture.Core.Generation;

namespace Conjecture.Tests.Generation;

public class ComposeTests
{
    private static ConjectureData MakeData(ulong seed = 42UL) =>
        ConjectureData.ForGeneration(new SplittableRandom(seed));

    [Fact]
    public void Compose_ImperativeDraw()
    {
        var strategy = Strategies.Compose(gen => gen.Next(Gen.Integers<int>(1, 10)));
        var data = MakeData();
        for (var i = 0; i < 100; i++)
            Assert.InRange(strategy.Next(data), 1, 10);
    }

    [Fact]
    public void Compose_DependentDraws()
    {
        var strategy = Strategies.Compose(gen =>
        {
            var x = gen.Next(Gen.Integers<int>(1, 5));
            var y = gen.Next(Gen.Integers<int>(1, x));
            return x * 10 + y;
        });
        var data = MakeData();
        for (var i = 0; i < 100; i++)
        {
            var result = strategy.Next(data);
            var tens = result / 10;
            var ones = result % 10;
            Assert.InRange(tens, 1, 5);
            Assert.InRange(ones, 1, tens);
        }
    }

    [Fact]
    public void Compose_Assume_RejectsInvalid()
    {
        var strategy = Strategies.Compose(gen =>
        {
            var x = gen.Next(Gen.Integers<int>(0, 10));
            gen.Assume(x % 2 == 0);
            return x;
        });
        var data = MakeData();
        for (var i = 0; i < 20; i++)
            Assert.True(strategy.Next(data) % 2 == 0);
    }

    [Fact]
    public void Compose_Assume_False_MarksInvalid()
    {
        var strategy = Strategies.Compose(gen => { gen.Assume(false); return 0; });
        var data = MakeData();
        Assert.ThrowsAny<Exception>((Action)(() => strategy.Next(data)));
        Assert.Equal(Status.Invalid, data.Status);
    }
}
