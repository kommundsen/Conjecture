// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Abstractions.Strategies;
using Conjecture.Core;
using Conjecture.Core.Internal;

namespace Conjecture.Core.Tests.Internal;

public class EnhancedCounterexampleFormatterTests
{
    [Fact]
    public void Format_IncludesExampleCount_InOutput()
    {
        var parameters = new[] { ("x", (object)1) };

        var result = CounterexampleFormatter.Format(parameters, seed: 0UL, exampleCount: 42, shrinkCount: 0);

        Assert.Contains("Falsifying example found after 42 examples", result);
    }

    [Fact]
    public void Format_IncludesShrinkCount_InOutput()
    {
        var parameters = new[] { ("x", (object)1) };

        var result = CounterexampleFormatter.Format(parameters, seed: 0UL, exampleCount: 1, shrinkCount: 5);

        Assert.Contains("Shrunk 5 times from original", result);
    }

    [Fact]
    public void Format_ZeroShrinks_IncludesZeroShrinkLine()
    {
        var parameters = new[] { ("x", (object)1) };

        var result = CounterexampleFormatter.Format(parameters, seed: 0UL, exampleCount: 1, shrinkCount: 0);

        Assert.Contains("Shrunk 0 times from original", result);
    }

    [Fact]
    public void Format_UsesFormatterRegistry_WhenRegistered()
    {
        FormatterRegistry.Register<FormattedValue>(new FormattedValueFormatter());
        var value = new FormattedValue(99);
        var parameters = new[] { ("v", (object)value) };

        var result = CounterexampleFormatter.Format(parameters, seed: 0UL, exampleCount: 1, shrinkCount: 0);

        FormatterRegistry.Register<FormattedValue>(null);
        Assert.Contains("v = FORMATTED(99)", result);
    }

    [Fact]
    public void Format_FallsBackToToString_WhenNoFormatterRegistered()
    {
        FormatterRegistry.Register<UnformattedValue>(null);
        var value = new UnformattedValue("hello");
        var parameters = new[] { ("v", (object)value) };

        var result = CounterexampleFormatter.Format(parameters, seed: 0UL, exampleCount: 1, shrinkCount: 0);

        Assert.Contains("v = hello", result);
    }

    [Fact]
    public void Format_IncludesSeedLine()
    {
        var parameters = new[] { ("x", (object)1) };

        var result = CounterexampleFormatter.Format(parameters, seed: 0xDEADBEEFUL, exampleCount: 1, shrinkCount: 0);

        Assert.Contains("Reproduce with: [Property(Seed = 0xDEADBEEF)]", result);
    }

    private sealed class FormattedValue(int n)
    {
        public int N => n;
    }

    private sealed class FormattedValueFormatter : IStrategyFormatter<FormattedValue>
    {
        public string Format(FormattedValue value) => $"FORMATTED({value.N})";
    }

    private sealed class UnformattedValue(string s)
    {
        public override string ToString() => s;
    }
}