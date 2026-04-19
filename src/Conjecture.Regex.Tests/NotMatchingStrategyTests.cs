// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core;
using Conjecture.Regex;

using DotNetRegex = System.Text.RegularExpressions.Regex;

namespace Conjecture.Regex.Tests;

public class NotMatchingStrategyTests
{
    // ── 1. Fixed-shape pattern: no sample matches ─────────────────────────────

    [Fact]
    public void NotMatching_FixedShapePattern_NoSampleMatches()
    {
        string pattern = @"^\d{3}-\d{2}-\d{4}$";
        DotNetRegex regex = new(pattern);

        IReadOnlyList<string> samples = DataGen.Sample(RegexGenerate.NotMatching(pattern), 50, seed: 1UL);

        Assert.All(samples, s => Assert.False(regex.IsMatch(s), s));
    }

    // ── 2. Lowercase-alpha anchored: every sample contains no lowercase ASCII ──

    [Fact]
    public void NotMatching_LowercaseAlpha_SamplesAreNotAllLowercase()
    {
        string pattern = @"[a-z]+";
        DotNetRegex regex = new(pattern);

        IReadOnlyList<string> samples = DataGen.Sample(RegexGenerate.NotMatching(pattern), 50, seed: 2UL);

        Assert.All(samples, s => Assert.False(regex.IsMatch(s), s));
    }

    // ── 3. Empty-anchor pattern: no sample is the empty string ───────────────

    [Fact]
    public void NotMatching_EmptyAnchor_SamplesAreNonEmpty()
    {
        string pattern = @"^$";
        DotNetRegex regex = new(pattern);

        IReadOnlyList<string> samples = DataGen.Sample(RegexGenerate.NotMatching(pattern), 50, seed: 3UL);

        Assert.All(samples, s =>
        {
            Assert.True(s.Length > 0, $"Expected non-empty string, got empty: '{s}'");
            Assert.False(regex.IsMatch(s), s);
        });
    }

    // ── 4. Hard-to-mutate pattern: filter fallback produces non-matching ──────

    [Fact]
    public void NotMatching_HardToMutatePattern_FilterFallbackProducesNonMatching()
    {
        string pattern = @"^[a-zA-Z0-9]*$";
        DotNetRegex regex = new(pattern);

        IReadOnlyList<string> samples = DataGen.Sample(RegexGenerate.NotMatching(pattern), 20, seed: 4UL);

        Assert.All(samples, s => Assert.False(regex.IsMatch(s), s));
    }

    // ── 5. Sampling terminates within implicit timeout ────────────────────────

    [Fact]
    public void NotMatching_Sampling_TerminatesWithinTimeout()
    {
        IReadOnlyList<string> samples = DataGen.Sample(RegexGenerate.NotMatching(@"^\d{3}$"), 30, seed: 5UL);

        Assert.Equal(30, samples.Count);
    }

    // ── 6. Regex overload: behaves identically to pattern overload ────────────

    [Fact]
    public void NotMatching_RegexOverload_NoSampleMatches()
    {
        DotNetRegex regex = new(@"^\d{3}-\d{2}-\d{4}$");

        IReadOnlyList<string> samples = DataGen.Sample(RegexGenerate.NotMatching(regex), 50, seed: 1UL);

        Assert.All(samples, s => Assert.False(regex.IsMatch(s), s));
    }
}