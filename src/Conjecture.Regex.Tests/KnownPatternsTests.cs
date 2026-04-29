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
        IReadOnlyList<string> samples = DataGen.Sample(Strategy.Email(), SampleSize, Seed);
        Assert.All(samples, s => Assert.Matches(KnownRegex.Email, s));
    }

    [Fact]
    public void NotEmail_NoSampleMatchesEmailRegex()
    {
        IReadOnlyList<string> samples = DataGen.Sample(Strategy.NotEmail(), SampleSize, Seed);
        Assert.All(samples, s => Assert.DoesNotMatch(KnownRegex.Email, s));
    }

    // ── Url ───────────────────────────────────────────────────────────────────

    [Fact]
    public void Url_AllSamplesMatchUrlRegex()
    {
        IReadOnlyList<string> samples = DataGen.Sample(Strategy.Url(), SampleSize, Seed);
        Assert.All(samples, s => Assert.Matches(KnownRegex.Url, s));
    }

    [Fact]
    public void NotUrl_NoSampleMatchesUrlRegex()
    {
        IReadOnlyList<string> samples = DataGen.Sample(Strategy.NotUrl(), SampleSize, Seed);
        Assert.All(samples, s => Assert.DoesNotMatch(KnownRegex.Url, s));
    }

    // ── Uuid ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Uuid_AllSamplesMatchUuidRegex()
    {
        IReadOnlyList<string> samples = DataGen.Sample(Strategy.Uuid(), SampleSize, Seed);
        Assert.All(samples, s => Assert.Matches(KnownRegex.Uuid, s));
    }

    [Fact]
    public void NotUuid_NoSampleMatchesUuidRegex()
    {
        IReadOnlyList<string> samples = DataGen.Sample(Strategy.NotUuid(), SampleSize, Seed);
        Assert.All(samples, s => Assert.DoesNotMatch(KnownRegex.Uuid, s));
    }

    // ── IsoDate ───────────────────────────────────────────────────────────────

    [Fact]
    public void IsoDate_AllSamplesMatchIsoDateRegex()
    {
        IReadOnlyList<string> samples = DataGen.Sample(Strategy.IsoDate(), SampleSize, Seed);
        Assert.All(samples, s => Assert.Matches(KnownRegex.IsoDate, s));
    }

    [Fact]
    public void NotIsoDate_NoSampleMatchesIsoDateRegex()
    {
        IReadOnlyList<string> samples = DataGen.Sample(Strategy.NotIsoDate(), SampleSize, Seed);
        Assert.All(samples, s => Assert.DoesNotMatch(KnownRegex.IsoDate, s));
    }

    // ── CreditCard ────────────────────────────────────────────────────────────

    [Fact]
    public void CreditCard_AllSamplesMatchCreditCardRegex()
    {
        IReadOnlyList<string> samples = DataGen.Sample(Strategy.CreditCard(), SampleSize, Seed);
        Assert.All(samples, s => Assert.Matches(KnownRegex.CreditCard, s));
    }

    [Fact]
    public void NotCreditCard_NoSampleMatchesCreditCardRegex()
    {
        IReadOnlyList<string> samples = DataGen.Sample(Strategy.NotCreditCard(), SampleSize, Seed);
        Assert.All(samples, s => Assert.DoesNotMatch(KnownRegex.CreditCard, s));
    }

    // ── Ipv4 ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Ipv4_AllSamplesMatchIpv4Regex()
    {
        IReadOnlyList<string> samples = DataGen.Sample(Strategy.Ipv4(), SampleSize, Seed);
        Assert.All(samples, s => Assert.Matches(KnownRegex.Ipv4, s));
    }

    [Theory]
    [InlineData("0.0.0.0")]
    [InlineData("255.255.255.255")]
    [InlineData("192.168.1.1")]
    [InlineData("10.0.0.1")]
    public void Ipv4_KnownValidAddresses_MatchRegex(string address)
    {
        Assert.Matches(KnownRegex.Ipv4, address);
    }

    [Theory]
    [InlineData("256.0.0.1")]
    [InlineData("192.168.1")]
    [InlineData("not-an-ip")]
    [InlineData("192.168.1.1.1")]
    public void Ipv4_KnownInvalidAddresses_DoNotMatchRegex(string address)
    {
        Assert.DoesNotMatch(KnownRegex.Ipv4, address);
    }

    // ── Ipv6 ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Ipv6_AllSamplesMatchIpv6Regex()
    {
        IReadOnlyList<string> samples = DataGen.Sample(Strategy.Ipv6(), SampleSize, Seed);
        Assert.All(samples, s => Assert.Matches(KnownRegex.Ipv6, s));
    }

    [Theory]
    [InlineData("2001:0db8:85a3:0000:0000:8a2e:0370:7334")]
    [InlineData("::1")]
    [InlineData("::")]
    [InlineData("fe80::1")]
    public void Ipv6_KnownValidAddresses_MatchRegex(string address)
    {
        Assert.Matches(KnownRegex.Ipv6, address);
    }

    [Theory]
    [InlineData("2001:db8::85a3::8a2e")]
    [InlineData("gggg::1")]
    [InlineData("not-an-ipv6")]
    public void Ipv6_KnownInvalidAddresses_DoNotMatchRegex(string address)
    {
        Assert.DoesNotMatch(KnownRegex.Ipv6, address);
    }

    // ── Date ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Date_AllSamplesMatchDateRegex()
    {
        IReadOnlyList<string> samples = DataGen.Sample(Strategy.Date(), SampleSize, Seed);
        Assert.All(samples, s => Assert.Matches(KnownRegex.Date, s));
    }

    [Theory]
    [InlineData("2024-01-01")]
    [InlineData("2000-12-31")]
    [InlineData("1970-06-15")]
    public void Date_KnownValidDates_MatchRegex(string date)
    {
        Assert.Matches(KnownRegex.Date, date);
    }

    [Theory]
    [InlineData("2024-1-1")]
    [InlineData("24-01-01")]
    [InlineData("2024/01/01")]
    [InlineData("2024-13-01")]
    [InlineData("not-a-date")]
    public void Date_KnownInvalidDates_DoNotMatchRegex(string date)
    {
        Assert.DoesNotMatch(KnownRegex.Date, date);
    }

    // ── Time ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Time_AllSamplesMatchTimeRegex()
    {
        IReadOnlyList<string> samples = DataGen.Sample(Strategy.Time(), SampleSize, Seed);
        Assert.All(samples, s => Assert.Matches(KnownRegex.Time, s));
    }

    [Theory]
    [InlineData("00:00:00")]
    [InlineData("23:59:59")]
    [InlineData("12:30:45")]
    public void Time_KnownValidTimes_MatchRegex(string time)
    {
        Assert.Matches(KnownRegex.Time, time);
    }

    [Theory]
    [InlineData("24:00:00")]
    [InlineData("12:60:00")]
    [InlineData("12:30")]
    [InlineData("not-a-time")]
    public void Time_KnownInvalidTimes_DoNotMatchRegex(string time)
    {
        Assert.DoesNotMatch(KnownRegex.Time, time);
    }
}