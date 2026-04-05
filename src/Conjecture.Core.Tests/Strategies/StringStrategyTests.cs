// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core;
using Conjecture.Core.Internal;

namespace Conjecture.Core.Tests.Strategies;

public class StringStrategyTests
{
    private static ConjectureData MakeData(ulong seed = 42UL) =>
        ConjectureData.ForGeneration(new SplittableRandom(seed));

    [Fact]
    public void Strings_ProducesString()
    {
        var strategy = Generate.Strings();
        var value = strategy.Generate(MakeData());
        Assert.IsType<string>(value);
    }

    [Fact]
    public void Strings_DefaultCharset_IsPrintableAscii()
    {
        var strategy = Generate.Strings();
        var data = MakeData();

        for (var i = 0; i < 200; i++)
        {
            var s = strategy.Generate(data);
            foreach (var c in s)
            {
                Assert.InRange((int)c, 32, 126);
            }
        }
    }

    [Fact]
    public void Strings_DeterministicWithSeed()
    {
        var strategy = Generate.Strings();

        var results1 = Enumerable.Range(0, 20).Select(_ => strategy.Generate(MakeData(99UL))).ToList();
        var results2 = Enumerable.Range(0, 20).Select(_ => strategy.Generate(MakeData(99UL))).ToList();

        Assert.Equal(results1, results2);
    }

    [Fact]
    public void Strings_RespectsBoundsWhenMinAndMaxLengthSet()
    {
        var strategy = Generate.Strings(minLength: 5, maxLength: 10);
        var data = MakeData();

        for (var i = 0; i < 100; i++)
        {
            var s = strategy.Generate(data);
            Assert.InRange(s.Length, 5, 10);
        }
    }

    [Fact]
    public void Strings_MinLengthZero_CanProduceEmptyString()
    {
        var strategy = Generate.Strings(minLength: 0, maxLength: 5);
        var sawEmpty = false;

        for (var seed = 0UL; seed < 1000UL && !sawEmpty; seed++)
        {
            var s = strategy.Generate(MakeData(seed));
            if (s.Length == 0)
            {
                sawEmpty = true;
            }
        }

        Assert.True(sawEmpty, "Expected empty string to be generated with minLength=0");
    }

    [Fact]
    public void Text_IsAliasForStrings()
    {
        var textStrategy = Generate.Text();
        var data = MakeData(77UL);
        var s = textStrategy.Generate(data);
        Assert.IsType<string>(s);
        foreach (var c in s)
        {
            Assert.InRange((int)c, 32, 126);
        }
    }
}