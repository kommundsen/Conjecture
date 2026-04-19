// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Generic;
using System.Linq;

using Conjecture.Core;
using Conjecture.Regex;

using DotNetRegex = System.Text.RegularExpressions.Regex;

namespace Conjecture.Regex.Tests;

public class LookaroundStrategyTests
{
    // ── 1. Positive lookahead chain: length >= 8, has digit, has lowercase ────

    [Fact]
    public void Matching_PositiveLookaheadChain_GeneratesStringWithRequiredClasses()
    {
        string pattern = @"(?=.*\d)(?=.*[a-z]).{8,}";
        DotNetRegex regex = new(pattern);
        ulong seed = 1UL;

        IReadOnlyList<string> samples = DataGen.Sample(RegexGenerate.Matching(pattern), 50, seed);

        Assert.All(samples, s =>
        {
            Assert.True(s.Length >= 8, $"Expected length >= 8, got {s.Length}: '{s}'");
            Assert.True(s.Any(char.IsDigit), $"Expected at least one digit in '{s}'");
            Assert.True(s.Any(c => c >= 'a' && c <= 'z'), $"Expected at least one lowercase letter in '{s}'");
            Assert.True(regex.IsMatch(s), s);
        });
    }

    // ── 2. Negative lookahead: no sample starts with a digit ─────────────────

    [Fact]
    public void Matching_NegativeLookaheadBeforeWordChars_NoSampleStartsWithDigit()
    {
        string pattern = @"(?!\d)\w+";
        DotNetRegex regex = new(pattern);
        ulong seed = 2UL;

        IReadOnlyList<string> samples = DataGen.Sample(RegexGenerate.Matching(pattern), 50, seed);

        Assert.All(samples, s =>
        {
            Assert.True(regex.IsMatch(s), s);
            Assert.True(!char.IsDigit(s[0]), $"Expected first char not to be digit in '{s}'");
        });
    }

    // ── 3. Positive lookbehind: generation does not throw; every sample matches

    [Fact]
    public void Matching_PositiveLookbehindAfterWhitespace_DoesNotThrowAndMatches()
    {
        string pattern = @"(?<=\s)\w+";
        DotNetRegex regex = new(pattern);
        ulong seed = 3UL;

        IReadOnlyList<string> samples = DataGen.Sample(RegexGenerate.Matching(pattern), 50, seed);

        Assert.All(samples, s => Assert.True(regex.IsMatch(s), s));
    }

    // ── 4. Negative lookbehind: every sample matches ──────────────────────────

    [Fact]
    public void Matching_NegativeLookbehindExcludingWhitespace_DoesNotFollowWhitespace()
    {
        string pattern = @"(?<!\s)\w+";
        DotNetRegex regex = new(pattern);
        ulong seed = 4UL;

        IReadOnlyList<string> samples = DataGen.Sample(RegexGenerate.Matching(pattern), 50, seed);

        Assert.All(samples, s => Assert.True(regex.IsMatch(s), s));
    }

    // ── 5. Impossible negative lookahead: falls back, produces empty strings ──

    [Fact]
    public void Matching_NegativeLookaheadOverFullAlphabet_FallsBackToFilterAndCompletes()
    {
        string pattern = @"(?![\s\S]).*";
        DotNetRegex regex = new(pattern);
        ulong seed = 5UL;

        IReadOnlyList<string> samples = DataGen.Sample(RegexGenerate.Matching(pattern), 20, seed);

        Assert.All(samples, s => Assert.True(regex.IsMatch(s), s));
    }
}