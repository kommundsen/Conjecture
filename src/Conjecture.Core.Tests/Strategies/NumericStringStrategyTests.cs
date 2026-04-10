// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core;
using Conjecture.Core.Internal;

namespace Conjecture.Core.Tests.Strategies;

public class NumericStringStrategyTests
{
    private static ConjectureData MakeData(ulong seed = 42UL) =>
        ConjectureData.ForGeneration(new SplittableRandom(seed));

    // --- Behaviour 1: all generated values contain only digit characters ---

    [Fact]
    public void NumericStrings_NoAffixes_AllCharsAreDigits()
    {
        Strategy<string> strategy = Generate.NumericStrings();
        ConjectureData data = MakeData();

        for (int i = 0; i < 200; i++)
        {
            string value = strategy.Generate(data);
            foreach (char c in value)
            {
                Assert.True(char.IsDigit(c), $"Expected only digit chars but got '{c}' in \"{value}\"");
            }
        }
    }

    // --- Behaviour 2: minDigits/maxDigits bounds are respected ---

    [Theory]
    [InlineData(1, 1)]
    [InlineData(3, 5)]
    [InlineData(4, 4)]
    [InlineData(1, 10)]
    public void NumericStrings_BoundedDigits_LengthWithinRange(int minDigits, int maxDigits)
    {
        Strategy<string> strategy = Generate.NumericStrings(minDigits: minDigits, maxDigits: maxDigits);
        ConjectureData data = MakeData(77UL);

        for (int i = 0; i < 100; i++)
        {
            string value = strategy.Generate(data);
            Assert.InRange(value.Length, minDigits, maxDigits);
        }
    }

    // --- Behaviour 3: prefix is prepended before digits ---

    [Fact]
    public void NumericStrings_WithPrefix_AllValuesStartWithPrefix()
    {
        Strategy<string> strategy = Generate.NumericStrings(prefix: "item");
        ConjectureData data = MakeData();

        for (int i = 0; i < 100; i++)
        {
            string value = strategy.Generate(data);
            Assert.True(value.StartsWith("item", StringComparison.Ordinal),
                $"Expected value to start with 'item' but got \"{value}\"");
        }
    }

    [Fact]
    public void NumericStrings_WithPrefix_DigitPartAfterPrefixContainsOnlyDigits()
    {
        Strategy<string> strategy = Generate.NumericStrings(prefix: "item");
        ConjectureData data = MakeData();

        for (int i = 0; i < 100; i++)
        {
            string value = strategy.Generate(data);
            string digitPart = value["item".Length..];
            foreach (char c in digitPart)
            {
                Assert.True(char.IsDigit(c), $"Expected only digits after prefix but got '{c}' in \"{value}\"");
            }
        }
    }

    // --- Behaviour 4: suffix is appended after digits ---

    [Fact]
    public void NumericStrings_WithSuffix_AllValuesEndWithSuffix()
    {
        Strategy<string> strategy = Generate.NumericStrings(suffix: "_end");
        ConjectureData data = MakeData();

        for (int i = 0; i < 100; i++)
        {
            string value = strategy.Generate(data);
            Assert.True(value.EndsWith("_end", StringComparison.Ordinal),
                $"Expected value to end with '_end' but got \"{value}\"");
        }
    }

    [Fact]
    public void NumericStrings_WithSuffix_DigitPartBeforeSuffixContainsOnlyDigits()
    {
        Strategy<string> strategy = Generate.NumericStrings(suffix: "_end");
        ConjectureData data = MakeData();

        for (int i = 0; i < 100; i++)
        {
            string value = strategy.Generate(data);
            string digitPart = value[..^"_end".Length];
            foreach (char c in digitPart)
            {
                Assert.True(char.IsDigit(c), $"Expected only digits before suffix but got '{c}' in \"{value}\"");
            }
        }
    }

    // --- Behaviour 6: defaults are minDigits=1, maxDigits=6 ---

    [Fact]
    public void NumericStrings_DefaultBounds_GeneratesValuesWithin1To6Digits()
    {
        Strategy<string> strategy = Generate.NumericStrings();
        ConjectureData data = MakeData();

        for (int i = 0; i < 200; i++)
        {
            string value = strategy.Generate(data);
            Assert.InRange(value.Length, 1, 6);
        }
    }

    [Fact]
    public void NumericStrings_DefaultBounds_CanGenerateSingleDigitValue()
    {
        Strategy<string> strategy = Generate.NumericStrings();
        bool sawSingleDigit = false;

        for (ulong seed = 0UL; seed < 500UL && !sawSingleDigit; seed++)
        {
            string value = strategy.Generate(MakeData(seed));
            if (value.Length == 1)
            {
                sawSingleDigit = true;
            }
        }

        Assert.True(sawSingleDigit, "Expected at least one single-digit value with default bounds");
    }
}