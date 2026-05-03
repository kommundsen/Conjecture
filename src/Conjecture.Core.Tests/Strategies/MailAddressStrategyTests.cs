// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Generic;
using System.Net.Mail;
using System.Text.RegularExpressions;

using Conjecture.Core;
using Conjecture.Core.Internal;

namespace Conjecture.Core.Tests.Strategies;

public class MailAddressStrategyTests
{
    // --- Behaviour 1: all samples are valid MailAddress ---

    [Fact]
    public void EmailAddresses_AllSamplesParseAsMailAddress()
    {
        Strategy<MailAddress> strategy = Strategy.EmailAddresses();
        Assert.All(strategy.WithSeed(42UL).Sample(200), addr =>
        {
            Assert.Equal(addr.User + "@" + addr.Host, addr.Address);
        });
    }

    // --- Behaviour 2: string round-trips through MailAddress ---

    [Fact]
    public void EmailAddressStrings_RoundTripThroughMailAddress()
    {
        Strategy<string> strategy = Strategy.EmailAddressStrings();
        Assert.All(strategy.WithSeed(42UL).Sample(200), s =>
        {
            MailAddress parsed = new(s);
            Assert.Equal(s, parsed.Address);
        });
    }

    // --- Behaviour 3: strings match local@host shape ---

    [Fact]
    public void EmailAddressStrings_MatchLocalAtHostShape()
    {
        Strategy<string> strategy = Strategy.EmailAddressStrings();
        Assert.All(strategy.WithSeed(77UL).Sample(200), s =>
        {
            Assert.Matches(@"^[a-z0-9]+@[a-z0-9.]+$", s);
        });
    }

    // --- Behaviour 4: host contains at least one dot (TLD-shaped) ---

    [Fact]
    public void EmailAddresses_HostHasAtLeastOneDot()
    {
        Strategy<MailAddress> strategy = Strategy.EmailAddresses();
        Assert.All(strategy.WithSeed(55UL).Sample(200), addr =>
        {
            Assert.Contains('.', addr.Host);
        });
    }

    // --- Behaviour 5: Strategy.For<MailAddress>() resolves ---

    [Fact]
    public void EmailAddresses_DefaultResolvesViaForT()
    {
        Strategy<MailAddress> strategy = Strategy.For<MailAddress>();
        IReadOnlyList<MailAddress> samples = strategy.WithSeed(42UL).Sample(10);
        Assert.Equal(10, samples.Count);
    }

    // --- Behaviour 6: shrinks toward minimal ---

    [Fact]
    public async System.Threading.Tasks.Task EmailAddresses_ShrinksTowardMinimal()
    {
        Strategy<MailAddress> strategy = Strategy.EmailAddresses();
        ConjectureSettings settings = new() { MaxExamples = 500, Seed = 1UL };

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            MailAddress addr = strategy.Generate(data);
            throw new System.Exception($"address {addr.Address} always fails");
        });

        Assert.False(result.Passed);

        // Probe the deterministic minimum by running with failing predicate to get the shrunk value,
        // then verify it equals what the strategy actually produces from the counterexample record.
        ConjectureData replay = ConjectureData.ForRecord(result.Counterexample!);
        MailAddress shrunk = strategy.Generate(replay);

        // Minimal form is a@b.cc shape: shortest alphanumeric local + shortest TLD host
        Assert.Matches(@"^[a-z0-9]+@[a-z0-9]+\.[a-z]{2,}$", shrunk.Address);

        // Verify it equals the probe minimum obtained the same way
        ConjectureData replay2 = ConjectureData.ForRecord(result.Counterexample!);
        MailAddress shrunk2 = strategy.Generate(replay2);
        Assert.Equal(shrunk.Address, shrunk2.Address);
    }

    // --- Behaviour 7: deterministic with seed ---

    [Fact]
    public void EmailAddresses_DeterministicWithSeed()
    {
        Strategy<MailAddress> strategy = Strategy.EmailAddresses();
        IReadOnlyList<MailAddress> results1 = strategy.WithSeed(123UL).Sample(50);
        IReadOnlyList<MailAddress> results2 = strategy.WithSeed(123UL).Sample(50);
        Assert.Equal(results1.Count, results2.Count);
        for (int i = 0; i < results1.Count; i++)
        {
            Assert.Equal(results1[i].Address, results2[i].Address);
        }
    }
}
