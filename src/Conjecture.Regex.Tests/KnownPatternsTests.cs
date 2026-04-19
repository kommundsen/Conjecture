// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core;
using Conjecture.Regex;

using DotNetRegex = System.Text.RegularExpressions.Regex;

namespace Conjecture.Regex.Tests;

public class KnownPatternsTests
{
    private const ulong Seed = 42UL;
    private const int SampleSize = 50;

    // ── Email ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Email_AllSamplesMatchEmailRegex()
    {
        IReadOnlyList<string> samples = DataGen.Sample(RegexGenerate.Email(), SampleSize, Seed);
        Assert.All(samples, s => Assert.Matches(KnownRegex.Email, s));
    }

    [Fact]
    public void NotEmail_NoSampleMatchesEmailRegex()
    {
        IReadOnlyList<string> samples = DataGen.Sample(RegexGenerate.NotEmail(), SampleSize, Seed);
        Assert.All(samples, s => Assert.DoesNotMatch(KnownRegex.Email, s));
    }

    // ── Url ───────────────────────────────────────────────────────────────────

    [Fact]
    public void Url_AllSamplesMatchUrlRegex()
    {
        IReadOnlyList<string> samples = DataGen.Sample(RegexGenerate.Url(), SampleSize, Seed);
        Assert.All(samples, s => Assert.Matches(KnownRegex.Url, s));
    }

    [Fact]
    public void NotUrl_NoSampleMatchesUrlRegex()
    {
        IReadOnlyList<string> samples = DataGen.Sample(RegexGenerate.NotUrl(), SampleSize, Seed);
        Assert.All(samples, s => Assert.DoesNotMatch(KnownRegex.Url, s));
    }

    // ── Uuid ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Uuid_AllSamplesMatchUuidRegex()
    {
        IReadOnlyList<string> samples = DataGen.Sample(RegexGenerate.Uuid(), SampleSize, Seed);
        Assert.All(samples, s => Assert.Matches(KnownRegex.Uuid, s));
    }

    [Fact]
    public void NotUuid_NoSampleMatchesUuidRegex()
    {
        IReadOnlyList<string> samples = DataGen.Sample(RegexGenerate.NotUuid(), SampleSize, Seed);
        Assert.All(samples, s => Assert.DoesNotMatch(KnownRegex.Uuid, s));
    }

    // ── IsoDate ───────────────────────────────────────────────────────────────

    [Fact]
    public void IsoDate_AllSamplesMatchIsoDateRegex()
    {
        IReadOnlyList<string> samples = DataGen.Sample(RegexGenerate.IsoDate(), SampleSize, Seed);
        Assert.All(samples, s => Assert.Matches(KnownRegex.IsoDate, s));
    }

    [Fact]
    public void NotIsoDate_NoSampleMatchesIsoDateRegex()
    {
        IReadOnlyList<string> samples = DataGen.Sample(RegexGenerate.NotIsoDate(), SampleSize, Seed);
        Assert.All(samples, s => Assert.DoesNotMatch(KnownRegex.IsoDate, s));
    }

    // ── CreditCard ────────────────────────────────────────────────────────────

    [Fact]
    public void CreditCard_AllSamplesMatchCreditCardRegex()
    {
        IReadOnlyList<string> samples = DataGen.Sample(RegexGenerate.CreditCard(), SampleSize, Seed);
        Assert.All(samples, s => Assert.Matches(KnownRegex.CreditCard, s));
    }

    [Fact]
    public void NotCreditCard_NoSampleMatchesCreditCardRegex()
    {
        IReadOnlyList<string> samples = DataGen.Sample(RegexGenerate.NotCreditCard(), SampleSize, Seed);
        Assert.All(samples, s => Assert.DoesNotMatch(KnownRegex.CreditCard, s));
    }
}