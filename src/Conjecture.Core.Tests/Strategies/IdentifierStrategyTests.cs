// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Text.RegularExpressions;

using Conjecture.Core;
using Conjecture.Core.Internal;

namespace Conjecture.Core.Tests.Strategies;

public class IdentifierStrategyTests
{
    private static ConjectureData MakeData(ulong seed = 42UL) =>
        ConjectureData.ForGeneration(new SplittableRandom(seed));

    // --- Behaviour 1: all generated values match [a-z]+\d+ ---

    [Fact]
    public void Identifiers_AllValuesMatchPattern()
    {
        Strategy<string> strategy = Strategy.Identifiers();
        ConjectureData data = MakeData();

        for (int i = 0; i < 200; i++)
        {
            string value = strategy.Generate(data);
            Assert.Matches("^[a-z]+[0-9]+$", value);
        }
    }

    // --- Behaviour 2: prefix length bounded by minPrefixLength/maxPrefixLength ---

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 4)]
    [InlineData(3, 3)]
    public void Identifiers_PrefixLengthWithinBounds(int minPrefixLength, int maxPrefixLength)
    {
        Strategy<string> strategy = Strategy.Identifiers(
            minPrefixLength: minPrefixLength,
            maxPrefixLength: maxPrefixLength,
            minDigits: 1,
            maxDigits: 1);
        ConjectureData data = MakeData(77UL);

        for (int i = 0; i < 100; i++)
        {
            string value = strategy.Generate(data);
            // strip trailing digit(s) — here exactly 1 digit
            string prefix = value[..^1];
            Assert.InRange(prefix.Length, minPrefixLength, maxPrefixLength);
        }
    }

    // --- Behaviour 3: digit count bounded by minDigits/maxDigits ---

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 5)]
    [InlineData(4, 4)]
    public void Identifiers_DigitCountWithinBounds(int minDigits, int maxDigits)
    {
        Strategy<string> strategy = Strategy.Identifiers(
            minPrefixLength: 1,
            maxPrefixLength: 1,
            minDigits: minDigits,
            maxDigits: maxDigits);
        ConjectureData data = MakeData(55UL);

        for (int i = 0; i < 100; i++)
        {
            string value = strategy.Generate(data);
            // strip leading alpha — exactly 1 prefix char
            string digits = value[1..];
            Assert.InRange(digits.Length, minDigits, maxDigits);
        }
    }

    // --- Behaviour 4: replay with all-minimum draws produces "a0" ---

    [Fact]
    public void Identifiers_AllMinimumReplay_ProducesA0()
    {
        // Minimum replay: prefix length = 1 ('a'), digit length = 1 ('0')
        // IR nodes order: StringLength(1,1,6), StringChar('a','a','z') x1,
        //                 StringLength(1,1,4), StringChar('0','0','9') x1
        IRNode[] nodes =
        [
            IRNode.ForStringLength(1UL, 1UL, 6UL),   // prefix length = 1
            IRNode.ForStringChar((ulong)'a', (ulong)'a', (ulong)'z'), // prefix[0] = 'a'
            IRNode.ForStringLength(1UL, 1UL, 4UL),   // digit length = 1
            IRNode.ForStringChar((ulong)'0', (ulong)'0', (ulong)'9'), // digit[0] = '0'
        ];

        ConjectureData replay = ConjectureData.ForRecord(nodes);
        Strategy<string> strategy = Strategy.Identifiers();
        string value = strategy.Generate(replay);

        Assert.Equal("a0", value);
    }
}