using Conjecture.Core;
using Conjecture.Core.Internal;

namespace Conjecture.Tests.Strategies;

public class StringStrategyTests
{
    private static ConjectureData MakeData(ulong seed = 42UL) =>
        ConjectureData.ForGeneration(new SplittableRandom(seed));

    [Fact]
    public void Strings_Default_ReturnsNonNullString()
    {
        var strategy = Gen.Strings();
        var data = MakeData();
        Assert.NotNull(strategy.Next(data));
    }

    [Fact]
    public void Strings_BoundedLength_ReturnsWithinBounds()
    {
        var strategy = Gen.Strings(minLength: 3, maxLength: 7);
        for (var i = 0; i < 50; i++)
        {
            var s = strategy.Next(MakeData((ulong)i));
            Assert.InRange(s.Length, 3, 7);
        }
    }

    [Fact]
    public void Strings_CustomAlphabet_ReturnsOnlyThoseChars()
    {
        char[] alphabet = ['a', 'b', 'c'];
        var strategy = Gen.Strings(alphabet: alphabet, minLength: 1, maxLength: 5);
        for (var i = 0; i < 50; i++)
        {
            var s = strategy.Next(MakeData((ulong)i));
            Assert.All(s, c => Assert.Contains(c, alphabet));
        }
    }

    [Fact]
    public void Strings_ZeroMinLength_AllowsEmptyString()
    {
        var strategy = Gen.Strings(minLength: 0, maxLength: 0);
        var data = MakeData();
        Assert.Equal(string.Empty, strategy.Next(data));
    }
}
