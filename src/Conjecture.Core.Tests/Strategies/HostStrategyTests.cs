// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using Conjecture.Core;
using Conjecture.Core.Internal;

namespace Conjecture.Core.Tests.Strategies;

public class HostStrategyTests
{
    // --- Behaviour 1: all samples match DNS host shape ---

    [Fact]
    public void Hosts_AllSamplesMatchHostShape()
    {
        Strategy<string> strategy = Strategy.Hosts();
        Assert.All(strategy.WithSeed(42UL).Sample(200), host =>
        {
            bool matchesMultiLabel = Regex.IsMatch(host, @"^[a-z0-9]+(\.[a-z0-9]+)*\.[a-z]{2,6}$");
            bool matchesSingleLabel = Regex.IsMatch(host, @"^[a-z]{2,6}$");
            Assert.True(matchesMultiLabel || matchesSingleLabel,
                $"Host '{host}' does not match expected DNS shape");
        });
    }

    // --- Behaviour 2: default label count within [1, 3] ---

    [Fact]
    public void Hosts_DefaultLabelCountWithinRange()
    {
        Strategy<string> strategy = Strategy.Hosts();
        Assert.All(strategy.WithSeed(77UL).Sample(200), host =>
        {
            int labelCount = host.Split('.').Length;
            Assert.InRange(labelCount, 1, 3);
        });
    }

    // --- Behaviour 3: custom range respected ---

    [Fact]
    public void Hosts_CustomRange_Respected()
    {
        Strategy<string> strategy = Strategy.Hosts(2, 4);
        Assert.All(strategy.WithSeed(55UL).Sample(200), host =>
        {
            int labelCount = host.Split('.').Length;
            Assert.InRange(labelCount, 2, 4);
        });
    }

    // --- Behaviour 4: TLD label (final) has no digits ---

    [Fact]
    public void Hosts_TldLabelHasNoDigits()
    {
        Strategy<string> strategy = Strategy.Hosts();
        Assert.All(strategy.WithSeed(13UL).Sample(200), host =>
        {
            string[] labels = host.Split('.');
            string tld = labels[^1];
            Assert.Matches(@"^[a-z]+$", tld);
        });
    }

    // --- Behaviour 5: round-trips through UriBuilder.Host ---

    [Fact]
    public void Hosts_RoundTripsThroughUriHost()
    {
        Strategy<string> strategy = Strategy.Hosts();
        Assert.All(strategy.WithSeed(99UL).Sample(100), host =>
        {
            UriBuilder builder = new() { Host = host };
            Assert.Equal(host, builder.Host);
        });
    }

    // --- Behaviour 6: shrinks toward minimal ---

    [Fact]
    public async Task Hosts_ShrinksTowardMinimal_SingleLabel()
    {
        Strategy<string> strategy = Strategy.Hosts(minLabels: 1, maxLabels: 1);
        ConjectureSettings settings = new() { MaxExamples = 500, Seed = 1UL };

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            string host = strategy.Generate(data);
            if (host.Length > 0)
            {
                throw new Exception("any host fails");
            }
        });

        Assert.False(result.Passed);
        ConjectureData replay = ConjectureData.ForRecord(result.Counterexample!);
        string shrunk = strategy.Generate(replay);
        Assert.Equal(2, shrunk.Length);
        Assert.Matches(@"^[a-z]{2}$", shrunk);
    }

    [Fact]
    public async Task Hosts_ShrinksTowardMinimal_DefaultRange()
    {
        Strategy<string> strategy = Strategy.Hosts();
        ConjectureSettings settings = new() { MaxExamples = 500, Seed = 2UL };

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            string host = strategy.Generate(data);
            if (host.Length > 0)
            {
                throw new Exception("any host fails");
            }
        });

        Assert.False(result.Passed);
        ConjectureData replay = ConjectureData.ForRecord(result.Counterexample!);
        string shrunk = strategy.Generate(replay);
        // Deterministic minimum for default (minLabels=1, maxLabels=3) with seed=2
        Assert.Equal("aaa.aa", shrunk);
    }

    // --- Behaviour 7: invalid args throw ArgumentOutOfRangeException ---

    [Theory]
    [InlineData(0, 3)]
    [InlineData(3, 1)]
    public void Hosts_InvalidArgs_Throw(int minLabels, int maxLabels)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Strategy.Hosts(minLabels, maxLabels));
    }

    // --- Behaviour 8: deterministic with seed ---

    [Fact]
    public void Hosts_DeterministicWithSeed()
    {
        Strategy<string> strategy = Strategy.Hosts();
        IReadOnlyList<string> results1 = strategy.WithSeed(123UL).Sample(50);
        IReadOnlyList<string> results2 = strategy.WithSeed(123UL).Sample(50);
        Assert.Equal(results1, results2);
    }
}
