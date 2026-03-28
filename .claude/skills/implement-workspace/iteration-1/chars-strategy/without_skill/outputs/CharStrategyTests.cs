using Conjecture.Core;
using Conjecture.Core.Internal;

namespace Conjecture.Tests.Strategies;

public class CharStrategyTests
{
    private static ConjectureData MakeData(ulong seed = 42UL) =>
        ConjectureData.ForGeneration(new SplittableRandom(seed));

    [Fact]
    public void Chars_DefaultAlphabet_ReturnsPrintableAscii()
    {
        var strategy = Gen.Chars();
        var data = MakeData();
        for (var i = 0; i < 100; i++)
        {
            var c = strategy.Next(data);
            Assert.InRange(c, ' ', '~'); // printable ASCII: 0x20–0x7E
        }
    }

    [Fact]
    public void Chars_CustomAlphabet_ReturnsOnlyFromAlphabet()
    {
        char[] alphabet = ['a', 'b', 'c'];
        var strategy = Gen.Chars(alphabet);
        var data = MakeData();
        for (var i = 0; i < 100; i++)
        {
            Assert.Contains(strategy.Next(data), alphabet);
        }
    }

    [Fact]
    public void Chars_SingleChar_AlwaysReturnsIt()
    {
        var strategy = Gen.Chars(['x']);
        var data = MakeData();
        for (var i = 0; i < 20; i++)
        {
            Assert.Equal('x', strategy.Next(data));
        }
    }
}
