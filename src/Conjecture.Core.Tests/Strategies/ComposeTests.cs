// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core;
using Conjecture.Core.Internal;


namespace Conjecture.Core.Tests.Strategies;

public class ComposeTests
{
    private static ConjectureData MakeData(ulong seed = 42UL) =>
        ConjectureData.ForGeneration(new SplittableRandom(seed));

    [Fact]
    public void Compose_ImperativeDraw()
    {
        var strategy = Generate.Compose(gen => gen.Generate(Generate.Integers<int>(1, 10)));
        var data = MakeData();
        for (var i = 0; i < 100; i++)
        {
            Assert.InRange(strategy.Generate(data), 1, 10);
        }
    }

    [Fact]
    public void Compose_DependentDraws()
    {
        var strategy = Generate.Compose(gen =>
        {
            var x = gen.Generate(Generate.Integers<int>(1, 5));
            var y = gen.Generate(Generate.Integers<int>(1, x));
            return x * 10 + y;
        });
        var data = MakeData();
        for (var i = 0; i < 100; i++)
        {
            var result = strategy.Generate(data);
            var tens = result / 10;
            var ones = result % 10;
            Assert.InRange(tens, 1, 5);
            Assert.InRange(ones, 1, tens);
        }
    }

    [Fact]
    public void Compose_Assume_RejectsInvalid()
    {
        var strategy = Generate.Compose(gen =>
        {
            var x = gen.Generate(Generate.Integers<int>(0, 10));
            gen.Assume(x % 2 == 0);
            return x;
        });
        var data = MakeData();
        for (var i = 0; i < 20; i++)
        {
            Assert.True(strategy.Generate(data) % 2 == 0);
        }
    }

    [Fact]
    public void Compose_Assume_False_MarksInvalid()
    {
        var strategy = Generate.Compose(gen => { gen.Assume(false); return 0; });
        var data = MakeData();
        Assert.ThrowsAny<Exception>((Action)(() => strategy.Generate(data)));
        Assert.Equal(Status.Invalid, data.Status);
    }
}