using Conjecture.Core;
using Conjecture.Core.Internal;
using Conjecture.Core.Generation;

namespace Conjecture.Tests.Strategies;

public class StringCharsetTests
{
    private static ConjectureData MakeData(ulong seed = 42UL) =>
        ConjectureData.ForGeneration(new SplittableRandom(seed));

    [Fact]
    public void Strings_Alphabet_OnlyProducesCharsFromAlphabet()
    {
        var strategy = Gen.Strings(alphabet: "abc");
        var data = MakeData();

        for (var i = 0; i < 200; i++)
        {
            var s = strategy.Next(data);
            foreach (var c in s)
            {
                Assert.Contains(c, "abc");
            }
        }
    }

    [Fact]
    public void Strings_Alphabet_CoversAllAlphabetCharsOverManyDraws()
    {
        var strategy = Gen.Strings(alphabet: "abc", minLength: 10, maxLength: 20);
        var seen = new HashSet<char>();

        for (var seed = 0UL; seed < 500UL && seen.Count < 3; seed++)
        {
            var s = strategy.Next(MakeData(seed));
            foreach (var c in s)
            {
                seen.Add(c);
            }
        }

        Assert.Contains('a', seen);
        Assert.Contains('b', seen);
        Assert.Contains('c', seen);
    }

    [Fact]
    public void Strings_Alphabet_SingleChar_AlwaysProducesThatChar()
    {
        var strategy = Gen.Strings(alphabet: "z", minLength: 3, maxLength: 3);
        var data = MakeData();

        for (var i = 0; i < 50; i++)
        {
            var s = strategy.Next(data);
            Assert.Equal("zzz", s);
        }
    }

    [Fact]
    public void Strings_EmptyAlphabet_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => Gen.Strings(alphabet: ""));
    }

    [Fact]
    public void Strings_MinMaxCodepoint_RespectsRange()
    {
        // Latin Extended-A range: U+0100–U+017F
        var strategy = Gen.Strings(minCodepoint: 0x0100, maxCodepoint: 0x017F, minLength: 5, maxLength: 10);
        var data = MakeData();

        for (var i = 0; i < 100; i++)
        {
            var s = strategy.Next(data);
            foreach (var c in s)
            {
                Assert.InRange((int)c, 0x0100, 0x017F);
            }
        }
    }

    [Fact]
    public void Strings_MinMaxCodepoint_LengthBoundsRespected()
    {
        var strategy = Gen.Strings(minCodepoint: 65, maxCodepoint: 90, minLength: 3, maxLength: 7);
        var data = MakeData();

        for (var i = 0; i < 100; i++)
        {
            var s = strategy.Next(data);
            Assert.InRange(s.Length, 3, 7);
        }
    }

    [Fact]
    public void Strings_UnicodeRange_CanProduceHighCodepointChars()
    {
        // Greek letters: U+0391–U+03C9
        var strategy = Gen.Strings(minCodepoint: 0x0391, maxCodepoint: 0x03C9, minLength: 1, maxLength: 5);
        var data = MakeData();

        var sawHighCodepoint = false;
        for (var i = 0; i < 200; i++)
        {
            var s = strategy.Next(data);
            if (s.Any(c => (int)c > 127))
            {
                sawHighCodepoint = true;
                break;
            }
        }

        Assert.True(sawHighCodepoint, "Expected chars above ASCII range from unicode codepoint range");
    }
}
