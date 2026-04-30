// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;

using Conjecture.Core;
using Conjecture.Regex;

using DotNetRegex = System.Text.RegularExpressions.Regex;
using RegexOptions = System.Text.RegularExpressions.RegexOptions;

namespace Conjecture.Regex.Tests;

public sealed class ReDoSHunterTests
{
    private const ulong Seed = 42UL;
    private const int SampleSize = 200;

    // ── 1. Known-vulnerable — nested quantifier ───────────────────────────────

    [Fact]
    public void ReDoSHunter_NestedQuantifier_FindsTimingViolatingInput()
    {
        string pattern = @"(a+)+$";
        DotNetRegex slowRegex = new(pattern, RegexOptions.None, TimeSpan.FromMilliseconds(10));
        Strategy<string> strategy = Strategy.ReDoSHunter(pattern, maxMatchMs: 5);

        IReadOnlyList<string> samples = strategy.WithSeed(Seed).Sample(SampleSize);

        Assert.Contains(samples, s =>
        {
            try { slowRegex.IsMatch(s); return false; }
            catch (RegexMatchTimeoutException) { return true; }
        });
    }

    // ── 2. Known-vulnerable — alternation loop ────────────────────────────────

    [Fact]
    public void ReDoSHunter_AlternationLoop_FindsTimingViolatingInput()
    {
        string pattern = @"([a-zA-Z]+)*$";
        DotNetRegex slowRegex = new(pattern, RegexOptions.None, TimeSpan.FromMilliseconds(10));
        Strategy<string> strategy = Strategy.ReDoSHunter(pattern, maxMatchMs: 5);

        IReadOnlyList<string> samples = strategy.WithSeed(Seed).Sample(SampleSize);

        Assert.Contains(samples, s =>
        {
            try { slowRegex.IsMatch(s); return false; }
            catch (RegexMatchTimeoutException) { return true; }
        });
    }

    // ── 3. Known-vulnerable — nested alternation ──────────────────────────────

    [Fact]
    public void ReDoSHunter_NestedAlternation_FindsTimingViolatingInput()
    {
        string pattern = @"(a|aa)+$";
        DotNetRegex slowRegex = new(pattern, RegexOptions.None, TimeSpan.FromMilliseconds(10));
        Strategy<string> strategy = Strategy.ReDoSHunter(pattern, maxMatchMs: 5);

        IReadOnlyList<string> samples = strategy.WithSeed(Seed).Sample(SampleSize);

        Assert.Contains(samples, s =>
        {
            try { slowRegex.IsMatch(s); return false; }
            catch (RegexMatchTimeoutException) { return true; }
        });
    }

    // ── 4. Safe regex — no spurious failures ──────────────────────────────────

    [Fact]
    public void ReDoSHunter_SafePattern_AllGeneratedStringsMatchPattern()
    {
        string pattern = @"^[a-z]+$";
        DotNetRegex regex = new(pattern);
        Strategy<string> strategy = Strategy.ReDoSHunter(pattern, maxMatchMs: 5);

        IReadOnlyList<string> samples = strategy.WithSeed(Seed).Sample(SampleSize);

        Assert.All(samples, s => Assert.Matches(regex, s));
    }

    // ── 5. NonBacktracking fallback — label ───────────────────────────────────

    [Fact]
    public void ReDoSHunter_NonBacktrackingRegex_LabelContainsNonBacktracking()
    {
        DotNetRegex regex = new("a+", RegexOptions.NonBacktracking);

        Strategy<string> strategy = Strategy.ReDoSHunter(regex, maxMatchMs: 5);

        Assert.Contains("non-backtracking", strategy.Label, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReDoSHunter_NonBacktrackingRegex_AllGeneratedStringsMatchPattern()
    {
        DotNetRegex regex = new("a+", RegexOptions.NonBacktracking);
        DotNetRegex verifyRegex = new("a+");
        Strategy<string> strategy = Strategy.ReDoSHunter(regex, maxMatchMs: 5);

        IReadOnlyList<string> samples = strategy.WithSeed(Seed).Sample(SampleSize);

        Assert.All(samples, s => Assert.Matches(verifyRegex, s));
    }

    // ── 6. Seeded run finds a timing-violating input ─────────────────────────

    [Fact]
    public void ReDoSHunter_NestedQuantifierSeededRun_FindsTimingViolatingInput()
    {
        string pattern = @"(a+)+$";
        DotNetRegex slowRegex = new(pattern, RegexOptions.None, TimeSpan.FromMilliseconds(5));
        Strategy<string> strategy = Strategy.ReDoSHunter(pattern, maxMatchMs: 5);

        IReadOnlyList<string> samples = strategy.WithSeed(Seed).Sample(SampleSize);

        string? counterexample = null;
        foreach (string s in samples)
        {
            try
            {
                slowRegex.IsMatch(s);
            }
            catch (RegexMatchTimeoutException)
            {
                counterexample = s;
                break;
            }
        }

        Assert.NotNull(counterexample);
    }
}