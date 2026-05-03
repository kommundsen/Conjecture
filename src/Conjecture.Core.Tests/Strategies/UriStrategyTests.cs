// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Collections.Generic;

using Conjecture.Core;

namespace Conjecture.Core.Tests.Strategies;

public class UriStrategyTests
{
    // --- Behaviour 1: default produces absolute URIs ---

    [Fact]
    public void Uris_Default_AreAbsolute()
    {
        Strategy<Uri> strategy = Strategy.Uris();
        Assert.All(strategy.WithSeed(42UL).Sample(100), uri =>
        {
            Assert.True(uri.IsAbsoluteUri, $"Expected absolute URI but got: {uri.OriginalString}");
        });
    }

    // --- Behaviour 2: default scheme is http or https ---

    [Fact]
    public void Uris_Default_SchemeIsHttpOrHttps()
    {
        Strategy<Uri> strategy = Strategy.Uris();
        Assert.All(strategy.WithSeed(42UL).Sample(100), uri =>
        {
            Assert.Contains(uri.Scheme, new[] { "http", "https" });
        });
    }

    // --- Behaviour 3: relative kind produces non-absolute URIs ---

    [Fact]
    public void Uris_Relative_AreNotAbsolute()
    {
        Strategy<Uri> strategy = Strategy.Uris(UriKind.Relative);
        Assert.All(strategy.WithSeed(42UL).Sample(100), uri =>
        {
            Assert.False(uri.IsAbsoluteUri, $"Expected relative URI but got: {uri.OriginalString}");
        });
    }

    // --- Behaviour 4: RelativeOrAbsolute generates a mix ---

    [Fact]
    public void Uris_RelativeOrAbsolute_GeneratesMix()
    {
        Strategy<Uri> strategy = Strategy.Uris(UriKind.RelativeOrAbsolute);
        IReadOnlyList<Uri> samples = strategy.WithSeed(99UL).Sample(200);
        bool hasAbsolute = false;
        bool hasRelative = false;
        foreach (Uri uri in samples)
        {
            if (uri.IsAbsoluteUri)
            {
                hasAbsolute = true;
            }
            else
            {
                hasRelative = true;
            }
        }
        Assert.True(hasAbsolute, "Expected at least one absolute URI in 200 samples");
        Assert.True(hasRelative, "Expected at least one relative URI in 200 samples");
    }

    // --- Behaviour 5: custom schemes are respected ---

    [Fact]
    public void Uris_CustomSchemes_Respected()
    {
        Strategy<Uri> strategy = Strategy.Uris(schemes: new[] { "ftp", "ws" });
        Assert.All(strategy.WithSeed(42UL).Sample(100), uri =>
        {
            Assert.Contains(uri.Scheme, new[] { "ftp", "ws" });
        });
    }

    // --- Behaviour 6: all samples are well-formed ---

    [Fact]
    public void Uris_AreWellFormed()
    {
        Strategy<Uri> strategyAbsolute = Strategy.Uris(UriKind.Absolute);
        Strategy<Uri> strategyRelative = Strategy.Uris(UriKind.Relative);
        Assert.All(strategyAbsolute.WithSeed(42UL).Sample(100), uri =>
        {
            UriKind kind = uri.IsAbsoluteUri ? UriKind.Absolute : UriKind.Relative;
            Assert.True(Uri.IsWellFormedUriString(uri.OriginalString, kind),
                $"URI '{uri.OriginalString}' is not well-formed ({kind})");
        });
        Assert.All(strategyRelative.WithSeed(42UL).Sample(100), uri =>
        {
            UriKind kind = uri.IsAbsoluteUri ? UriKind.Absolute : UriKind.Relative;
            Assert.True(Uri.IsWellFormedUriString(uri.OriginalString, kind),
                $"URI '{uri.OriginalString}' is not well-formed ({kind})");
        });
    }

    // --- Behaviour 7: host family includes IPv4, IPv6 (bracketed), and DNS ---

    [Fact]
    public void Uris_HostFamily_IncludesIPv4_IPv6_AndDns()
    {
        Strategy<Uri> strategy = Strategy.Uris(UriKind.Absolute);
        IReadOnlyList<Uri> samples = strategy.WithSeed(7UL).Sample(500);
        bool hasIPv4 = false;
        bool hasIPv6Bracketed = false;
        bool hasDns = false;
        foreach (Uri uri in samples)
        {
            string host = uri.Host;
            if (host.Contains('[') && host.Contains(']'))
            {
                hasIPv6Bracketed = true;
            }
            else if (System.Net.IPAddress.TryParse(host, out System.Net.IPAddress? addr)
                && addr.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            {
                hasIPv4 = true;
            }
            else
            {
                hasDns = true;
            }
        }
        Assert.True(hasIPv4, "Expected at least one IPv4-host URI in 500 samples");
        Assert.True(hasIPv6Bracketed, "Expected at least one IPv6-bracketed-host URI in 500 samples");
        Assert.True(hasDns, "Expected at least one DNS-name URI in 500 samples");
    }

    // --- Behaviour 8: empty schemes throws ArgumentException ---

    [Fact]
    public void Uris_EmptySchemes_Throws()
    {
        Assert.Throws<ArgumentException>(() => Strategy.Uris(schemes: Array.Empty<string>()));
    }

    // --- Behaviour 9: Strategy.For<Uri>() resolves ---

    [Fact]
    public void Uris_DefaultResolvesViaForT()
    {
        Strategy<Uri> strategy = Strategy.For<Uri>();
        IReadOnlyList<Uri> samples = strategy.WithSeed(42UL).Sample(10);
        Assert.Equal(10, samples.Count);
    }

    // --- Behaviour 10: deterministic with seed ---

    [Fact]
    public void Uris_DeterministicWithSeed()
    {
        Strategy<Uri> strategy = Strategy.Uris();
        IReadOnlyList<Uri> results1 = strategy.WithSeed(123UL).Sample(50);
        IReadOnlyList<Uri> results2 = strategy.WithSeed(123UL).Sample(50);
        Assert.Equal(results1, results2);
    }
}
