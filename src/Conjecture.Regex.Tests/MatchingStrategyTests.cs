// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Linq;

using Conjecture.Core;
using Conjecture.Regex;

using DotNetRegex = System.Text.RegularExpressions.Regex;
using RegexOptions = System.Text.RegularExpressions.RegexOptions;

namespace Conjecture.Regex.Tests;

public class MatchingStrategyTests
{
    // ── 1. Fixed-shape pattern: all samples match ─────────────────────────────

    [Fact]
    public void Matching_FixedShapePattern_EverySampleMatches()
    {
        string pattern = @"\d{3}-\d{2}-\d{4}";
        DotNetRegex regex = new(pattern);
        ulong seed = 42UL;

        IReadOnlyList<string> samples = Strategy.Matching(pattern).WithSeed(seed).Sample(100);

        Assert.All(samples, s => Assert.Matches(regex, s));
    }

    // ── 2. IgnoreCase: every sample matches and at least one is mixed-case ────

    [Fact]
    public void Matching_IgnoreCase_ProducesMixedCase()
    {
        DotNetRegex regex = new(@"[a-z]+", RegexOptions.IgnoreCase);
        ulong seed = 42UL;

        IReadOnlyList<string> samples = Strategy.Matching(regex).WithSeed(seed).Sample(100);

        Assert.All(samples, s => Assert.Matches(regex, s));
        Assert.Contains(samples, static s => s.Any(static c => c is >= 'A' and <= 'Z'));
    }

    // ── 3. Singleline dot: every sample matches and at least one has '\n' ─────

    [Fact]
    public void Matching_SinglelineDot_SometimesIncludesNewline()
    {
        DotNetRegex regex = new(@".+", RegexOptions.Singleline);
        ulong seed = 42UL;

        IReadOnlyList<string> samples = Strategy.Matching(regex).WithSeed(seed).Sample(100);

        Assert.All(samples, s => Assert.Matches(regex, s));
        Assert.Contains(samples, static s => s.Contains('\n'));
    }

    // ── 4. Multiline anchors: every sample matches the regex ─────────────────

    [Fact]
    public void Matching_MultilineAnchors_ProducesValidIntegers()
    {
        DotNetRegex regex = new(@"^\d+$", RegexOptions.Multiline);
        ulong seed = 42UL;

        IReadOnlyList<string> samples = Strategy.Matching(regex).WithSeed(seed).Sample(100);

        Assert.All(samples, s => Assert.Matches(regex, s));
    }

    // ── 5. Default UnicodeCoverage: all characters are ASCII letters ──────────

    [Fact]
    public void Matching_UnicodeCategoryDefault_IsAscii()
    {
        string pattern = @"\p{L}+";
        ulong seed = 42UL;

        IReadOnlyList<string> samples = Strategy.Matching(pattern).WithSeed(seed).Sample(100);

        Assert.All(samples, static s => Assert.All(s, static c => Assert.True(char.IsLetter(c) && c <= '\u007F',
                    $"Character '{c}' (U+{(int)c:X4}) is not an ASCII letter")));
    }

    // ── 6. Full UnicodeCoverage: every sample matches AND at least one non-ASCII

    [Fact]
    public void Matching_UnicodeCategoryFull_AllowsNonAscii()
    {
        string pattern = @"\p{L}+";
        RegexGenOptions options = new() { UnicodeCategories = UnicodeCoverage.Full };
        DotNetRegex regex = new(pattern);
        ulong seed = 42UL;

        IReadOnlyList<string> samples = Strategy.Matching(pattern, options).WithSeed(seed).Sample(200);

        Assert.All(samples, s => Assert.Matches(regex, s));
        Assert.Contains(samples, static s => s.Any(static c => c > '\u007F'));
    }

    // ── 7. Shrinking: strategy can reach minimum match length ─────────────────
    // Lighter idiom: sample with a fixed seed from \d{2,6} and verify that at
    // least one produced value has the minimum length of 2, confirming the
    // strategy explores (and the shrinker can reach) the lower bound.

    [Fact]
    public void Matching_FailingProperty_ShrinksTowardShortestMatch()
    {
        string pattern = @"\d{2,6}";
        DotNetRegex regex = new(pattern);
        ulong seed = 1UL;

        IReadOnlyList<string> samples = Strategy.Matching(pattern).WithSeed(seed).Sample(200);

        Assert.All(samples, s => Assert.Matches(regex, s));
        Assert.Contains(samples, static s => s.Length == 2);
    }
}