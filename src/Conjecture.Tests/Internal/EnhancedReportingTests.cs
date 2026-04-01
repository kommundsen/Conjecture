// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core;
using Conjecture.Core.Internal;

namespace Conjecture.Tests.Internal;

public class EnhancedReportingTests
{
    [Fact]
    public void Format_WithShrunkCountGtZero_ShowsBothFalsifyingAndMinimalSections()
    {
        (string, object)[] original = [("x", (object)100)];
        (string, object)[] shrunk = [("x", (object)1)];

        string result = CounterexampleFormatter.Format(original, shrunk, 0UL, 5, 3);

        Assert.Contains("Falsifying example", result);
        Assert.Contains("Minimal counterexample", result);
    }

    [Fact]
    public void Format_WithShrunkCountGtZero_OriginalValueAppearsBeforeMinimalSection()
    {
        (string, object)[] original = [("x", (object)100)];
        (string, object)[] shrunk = [("x", (object)1)];

        string result = CounterexampleFormatter.Format(original, shrunk, 0UL, 5, 3);

        int falsifyingIdx = result.IndexOf("Falsifying example", StringComparison.Ordinal);
        int minimalIdx = result.IndexOf("Minimal counterexample", StringComparison.Ordinal);
        Assert.True(falsifyingIdx >= 0 && minimalIdx > falsifyingIdx);
        string falsifyingSection = result[falsifyingIdx..minimalIdx];
        Assert.Contains("x = 100", falsifyingSection);
    }

    [Fact]
    public void Format_WithShrunkCountGtZero_ShrunkValueAppearsInMinimalSection()
    {
        (string, object)[] original = [("x", (object)100)];
        (string, object)[] shrunk = [("x", (object)1)];

        string result = CounterexampleFormatter.Format(original, shrunk, 0UL, 5, 3);

        int minimalIdx = result.IndexOf("Minimal counterexample", StringComparison.Ordinal);
        string minimalSection = result[minimalIdx..];
        Assert.Contains("x = 1", minimalSection);
    }

    [Fact]
    public void Format_WithZeroShrinks_ShowsOnlyFalsifyingSection()
    {
        (string, object)[] parameters = [("x", (object)42)];

        string result = CounterexampleFormatter.Format(parameters, parameters, 0UL, 5, 0);

        Assert.Contains("Falsifying example", result);
        Assert.DoesNotContain("Minimal counterexample", result);
    }

    [Fact]
    public void Format_IncludesFoundAfterNExamplesShrunkMTimes()
    {
        (string, object)[] parameters = [("x", (object)1)];

        string result = CounterexampleFormatter.Format(parameters, parameters, 0UL, 23, 14);

        Assert.Contains("found after 23 examples (shrunk 14 times)", result);
    }

    [Fact]
    public void Format_AlwaysIncludesReproduceLine()
    {
        (string, object)[] parameters = [("x", (object)1)];

        string result = CounterexampleFormatter.Format(parameters, parameters, 0xDEADBEEFUL, 1, 0);

        Assert.Contains("Reproduce with: [Property(Seed = 0xDEADBEEF)]", result);
    }

    [Fact]
    public void Format_UsesFormatterRegistry_ForShrunkValues()
    {
        FormatterRegistry.Register<TaggedInt>(new TaggedIntFormatter());
        (string, object)[] original = [("n", (object)new TaggedInt(100))];
        (string, object)[] shrunk = [("n", (object)new TaggedInt(42))];

        string result = CounterexampleFormatter.Format(original, shrunk, 0UL, 1, 5);

        FormatterRegistry.Register<TaggedInt>(null);
        int minimalIdx = result.IndexOf("Minimal counterexample", StringComparison.Ordinal);
        string minimalSection = result[minimalIdx..];
        Assert.Contains("n = TAGGED(42)", minimalSection);
    }

    [Fact]
    public void Format_UsesFormatterRegistry_ForOriginalValues()
    {
        FormatterRegistry.Register<TaggedInt>(new TaggedIntFormatter());
        (string, object)[] original = [("n", (object)new TaggedInt(100))];
        (string, object)[] shrunk = [("n", (object)new TaggedInt(42))];

        string result = CounterexampleFormatter.Format(original, shrunk, 0UL, 1, 5);

        FormatterRegistry.Register<TaggedInt>(null);
        int falsifyingIdx = result.IndexOf("Falsifying example", StringComparison.Ordinal);
        int minimalIdx = result.IndexOf("Minimal counterexample", StringComparison.Ordinal);
        string falsifyingSection = result[falsifyingIdx..minimalIdx];
        Assert.Contains("n = TAGGED(100)", falsifyingSection);
    }

    private sealed class TaggedInt(int n)
    {
        public int N => n;
    }

    private sealed class TaggedIntFormatter : IStrategyFormatter<TaggedInt>
    {
        public string Format(TaggedInt value) => $"TAGGED({value.N})";
    }
}