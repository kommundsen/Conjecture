// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Generic;

using Conjecture.Core;

namespace Conjecture.Core.Tests.Strategies;

public class NumericStringStrategyTests
{
    // --- Behaviour 1: all generated values contain only digit characters ---

    [Fact]
    public void NumericStrings_NoAffixes_AllCharsAreDigits()
    {
        Strategy<string> strategy = Strategy.NumericStrings();
        Assert.All(strategy.WithSeed(42UL).Sample(200), value =>
        {
            foreach (char c in value)
            {
                Assert.True(char.IsDigit(c), $"Expected only digit chars but got '{c}' in \"{value}\"");
            }
        });
    }

    // --- Behaviour 2: minDigits/maxDigits bounds are respected ---

    [Theory]
    [InlineData(1, 1)]
    [InlineData(3, 5)]
    [InlineData(4, 4)]
    [InlineData(1, 10)]
    public void NumericStrings_BoundedDigits_LengthWithinRange(int minDigits, int maxDigits)
    {
        Strategy<string> strategy = Strategy.NumericStrings(minDigits: minDigits, maxDigits: maxDigits);
        Assert.All(strategy.WithSeed(77UL).Sample(100), value => Assert.InRange(value.Length, minDigits, maxDigits));
    }

    // --- Behaviour 3: prefix is prepended before digits ---

    [Fact]
    public void NumericStrings_WithPrefix_AllValuesStartWithPrefix()
    {
        Strategy<string> strategy = Strategy.NumericStrings(prefix: "item");
        Assert.All(strategy.WithSeed(42UL).Sample(100), value =>
            Assert.True(value.StartsWith("item", StringComparison.Ordinal),
                $"Expected value to start with 'item' but got \"{value}\""));
    }

    [Fact]
    public void NumericStrings_WithPrefix_DigitPartAfterPrefixContainsOnlyDigits()
    {
        Strategy<string> strategy = Strategy.NumericStrings(prefix: "item");
        Assert.All(strategy.WithSeed(42UL).Sample(100), value =>
        {
            string digitPart = value["item".Length..];
            foreach (char c in digitPart)
            {
                Assert.True(char.IsDigit(c), $"Expected only digits after prefix but got '{c}' in \"{value}\"");
            }
        });
    }

    // --- Behaviour 4: suffix is appended after digits ---

    [Fact]
    public void NumericStrings_WithSuffix_AllValuesEndWithSuffix()
    {
        Strategy<string> strategy = Strategy.NumericStrings(suffix: "_end");
        Assert.All(strategy.WithSeed(42UL).Sample(100), value =>
            Assert.True(value.EndsWith("_end", StringComparison.Ordinal),
                $"Expected value to end with '_end' but got \"{value}\""));
    }

    [Fact]
    public void NumericStrings_WithSuffix_DigitPartBeforeSuffixContainsOnlyDigits()
    {
        Strategy<string> strategy = Strategy.NumericStrings(suffix: "_end");
        Assert.All(strategy.WithSeed(42UL).Sample(100), value =>
        {
            string digitPart = value[..^"_end".Length];
            foreach (char c in digitPart)
            {
                Assert.True(char.IsDigit(c), $"Expected only digits before suffix but got '{c}' in \"{value}\"");
            }
        });
    }

    // --- Behaviour 6: defaults are minDigits=1, maxDigits=6 ---

    [Fact]
    public void NumericStrings_DefaultBounds_GeneratesValuesWithin1To6Digits()
    {
        Strategy<string> strategy = Strategy.NumericStrings();
        Assert.All(strategy.WithSeed(42UL).Sample(200), value => Assert.InRange(value.Length, 1, 6));
    }

    [Fact]
    public void NumericStrings_DefaultBounds_CanGenerateSingleDigitValue()
    {
        Strategy<string> strategy = Strategy.NumericStrings();
        IReadOnlyList<string> values = strategy.WithSeed(0UL).Sample(500);
        Assert.Contains(values, v => v.Length == 1);
    }
}
