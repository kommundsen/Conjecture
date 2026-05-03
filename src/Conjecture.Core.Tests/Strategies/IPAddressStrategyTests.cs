// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

using Conjecture.Core;
using Conjecture.Core.Internal;

namespace Conjecture.Core.Tests.Strategies;

public class IPAddressStrategyTests
{
    // --- Behaviour 1: V4 only generates IPv4 ---

    [Fact]
    public void IPAddresses_V4_Only_GeneratesIPv4Family()
    {
        Strategy<IPAddress> strategy = Strategy.IPAddresses(IPAddressKind.V4);
        Assert.All(strategy.WithSeed(42UL).Sample(200), addr =>
            Assert.Equal(AddressFamily.InterNetwork, addr.AddressFamily));
    }

    // --- Behaviour 2: V6 only generates IPv6 ---

    [Fact]
    public void IPAddresses_V6_Only_GeneratesIPv6Family()
    {
        Strategy<IPAddress> strategy = Strategy.IPAddresses(IPAddressKind.V6);
        Assert.All(strategy.WithSeed(42UL).Sample(200), addr =>
            Assert.Equal(AddressFamily.InterNetworkV6, addr.AddressFamily));
    }

    // --- Behaviour 3: Both generates a mix of families ---

    [Fact]
    public void IPAddresses_Both_GeneratesMixOfFamilies()
    {
        Strategy<IPAddress> strategy = Strategy.IPAddresses();
        IReadOnlyList<IPAddress> samples = strategy.WithSeed(99UL).Sample(200);
        bool hasV4 = false;
        bool hasV6 = false;
        foreach (IPAddress addr in samples)
        {
            if (addr.AddressFamily == AddressFamily.InterNetwork)
            {
                hasV4 = true;
            }
            else if (addr.AddressFamily == AddressFamily.InterNetworkV6)
            {
                hasV6 = true;
            }
        }
        Assert.True(hasV4, "Expected at least one IPv4 address in 200 samples");
        Assert.True(hasV6, "Expected at least one IPv6 address in 200 samples");
    }

    // --- Behaviour 4: round-trips through parse ---

    [Fact]
    public void IPAddresses_RoundTripsThroughParse()
    {
        Strategy<IPAddress> strategy = Strategy.IPAddresses();
        Assert.All(strategy.WithSeed(77UL).Sample(100), addr =>
            Assert.Equal(IPAddress.Parse(addr.ToString()), addr));
    }

    // --- Behaviour 5: shrinks toward 0.0.0.0 for V4 ---

    [Fact]
    public async Task IPAddresses_ShrinksTowardLoopback()
    {
        Strategy<IPAddress> strategy = Strategy.IPAddresses(IPAddressKind.V4);
        ConjectureSettings settings = new() { MaxExamples = 500, Seed = 1UL };

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            IPAddress addr = strategy.Generate(data);
            throw new Exception($"address {addr} always fails");
        });

        Assert.False(result.Passed);
        ConjectureData replay = ConjectureData.ForRecord(result.Counterexample!);
        IPAddress shrunk = strategy.Generate(replay);
        Assert.Equal(IPAddress.Parse("0.0.0.0"), shrunk);
    }

    // --- Behaviour 6: zero flags throws ArgumentException ---

    [Fact]
    public void IPAddresses_NoFlags_Throws()
    {
        Assert.Throws<ArgumentException>(() => Strategy.IPAddresses((IPAddressKind)0));
    }

    // --- Behaviour 7: Strategy.For<IPAddress>() returns a working strategy ---

    [Fact]
    public void IPAddresses_DefaultResolvesViaForT()
    {
        Strategy<IPAddress> strategy = Strategy.For<IPAddress>();
        IReadOnlyList<IPAddress> samples = strategy.WithSeed(42UL).Sample(10);
        Assert.Equal(10, samples.Count);
    }

    // --- Behaviour 8: deterministic with seed ---

    [Fact]
    public void IPAddresses_DeterministicWithSeed()
    {
        Strategy<IPAddress> strategy = Strategy.IPAddresses();
        IReadOnlyList<IPAddress> results1 = strategy.WithSeed(123UL).Sample(50);
        IReadOnlyList<IPAddress> results2 = strategy.WithSeed(123UL).Sample(50);
        Assert.Equal(results1, results2);
    }
}
