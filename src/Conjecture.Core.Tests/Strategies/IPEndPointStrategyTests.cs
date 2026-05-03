// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

using Conjecture.Core;
using Conjecture.Core.Internal;

namespace Conjecture.Core.Tests.Strategies;

public class IPEndPointStrategyTests
{
    // --- Behaviour 1: default ports are in [0, 65535] ---

    [Fact]
    public void IPEndPoints_Default_PortInRange()
    {
        Strategy<IPEndPoint> strategy = Strategy.IPEndPoints();
        Assert.All(strategy.WithSeed(42UL).Sample(200), ep =>
        {
            Assert.InRange(ep.Port, 0, 65535);
        });
    }

    // --- Behaviour 2: default generates both address families ---

    [Fact]
    public void IPEndPoints_Default_UsesBothAddressFamilies()
    {
        Strategy<IPEndPoint> strategy = Strategy.IPEndPoints();
        IReadOnlyList<IPEndPoint> samples = strategy.WithSeed(99UL).Sample(200);
        bool hasV4 = false;
        bool hasV6 = false;
        foreach (IPEndPoint ep in samples)
        {
            if (ep.AddressFamily == AddressFamily.InterNetwork)
            {
                hasV4 = true;
            }
            else if (ep.AddressFamily == AddressFamily.InterNetworkV6)
            {
                hasV6 = true;
            }
        }
        Assert.True(hasV4, "Expected at least one IPv4 endpoint in 200 samples");
        Assert.True(hasV6, "Expected at least one IPv6 endpoint in 200 samples");
    }

    // --- Behaviour 3: custom port strategy is respected ---

    [Fact]
    public void IPEndPoints_CustomPortStrategy_RespectsRange()
    {
        Strategy<IPEndPoint> strategy = Strategy.IPEndPoints(ports: Strategy.Integers<int>(80, 90));
        Assert.All(strategy.WithSeed(42UL).Sample(200), ep =>
        {
            Assert.InRange(ep.Port, 80, 90);
        });
    }

    // --- Behaviour 4: custom address strategy is respected ---

    [Fact]
    public void IPEndPoints_CustomAddressStrategy_RespectsFamily()
    {
        Strategy<IPEndPoint> strategy = Strategy.IPEndPoints(addresses: Strategy.IPAddresses(IPAddressKind.V4));
        Assert.All(strategy.WithSeed(42UL).Sample(200), ep =>
        {
            Assert.Equal(AddressFamily.InterNetwork, ep.AddressFamily);
        });
    }

    // --- Behaviour 5: out-of-range port throws ArgumentOutOfRangeException ---

    [Fact]
    public void IPEndPoints_OutOfRangePort_Throws()
    {
        Strategy<IPEndPoint> strategy = Strategy.IPEndPoints(ports: Strategy.Just(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => strategy.WithSeed(1UL).Sample(1));
    }

    // --- Behaviour 6: round-trips through ToString/Parse ---

    [Fact]
    public void IPEndPoints_RoundTripsThroughToString()
    {
        Strategy<IPEndPoint> strategy = Strategy.IPEndPoints(addresses: Strategy.IPAddresses(IPAddressKind.V4));
        Assert.All(strategy.WithSeed(77UL).Sample(100), ep =>
        {
            Assert.Equal(IPEndPoint.Parse(ep.ToString()), ep);
        });
    }

    // --- Behaviour 7: shrinks toward 0.0.0.0:0 ---

    [Fact]
    public async Task IPEndPoints_ShrinksTowardZeroPortAndZeroAddress()
    {
        Strategy<IPEndPoint> strategy = Strategy.IPEndPoints(addresses: Strategy.IPAddresses(IPAddressKind.V4));
        ConjectureSettings settings = new() { MaxExamples = 500, Seed = 1UL };

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            IPEndPoint ep = strategy.Generate(data);
            throw new Exception($"endpoint {ep} always fails");
        });

        Assert.False(result.Passed);
        ConjectureData replay = ConjectureData.ForRecord(result.Counterexample!);
        IPEndPoint shrunk = strategy.Generate(replay);
        Assert.Equal(IPAddress.Parse("0.0.0.0"), shrunk.Address);
        Assert.Equal(0, shrunk.Port);
    }

    // --- Behaviour 8: Strategy.For<IPEndPoint>() resolves ---

    [Fact]
    public void IPEndPoints_DefaultResolvesViaForT()
    {
        Strategy<IPEndPoint> strategy = Strategy.For<IPEndPoint>();
        IReadOnlyList<IPEndPoint> samples = strategy.WithSeed(42UL).Sample(10);
        Assert.Equal(10, samples.Count);
    }
}
