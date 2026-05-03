// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Globalization;

using Conjecture.Core;
using Conjecture.Core.Internal;

namespace Conjecture.Core.Tests.Strategies;

public class CultureStrategyTests
{
    private static ConjectureData MakeData(ulong seed = 42UL) =>
        ConjectureData.ForGeneration(new SplittableRandom(seed));

    [Fact]
    public void Cultures_GeneratesNonNullValues()
    {
        Strategy<CultureInfo> strategy = Strategy.Cultures();
        ConjectureData data = MakeData();

        for (int i = 0; i < 100; i++)
        {
            CultureInfo value = strategy.Generate(data);
            Assert.True(value is not null);
        }
    }

    [Fact]
    public void Cultures_WithNeutralCulturesFilter_ExcludesSpecificCultures()
    {
        Strategy<CultureInfo> strategy = Strategy.Cultures(CultureTypes.NeutralCultures);
        ConjectureData data = MakeData();

        for (int i = 0; i < 200; i++)
        {
            CultureInfo value = strategy.Generate(data);
            Assert.False(value.CultureTypes == CultureTypes.SpecificCultures,
                $"Expected no specific-only cultures but got: {value.Name}");
        }
    }

    [Fact]
    public async Task Cultures_ShrinksTowardInvariantCulture()
    {
        Strategy<CultureInfo> strategy = Strategy.Cultures();
        ConjectureSettings settings = new() { MaxExamples = 200, Seed = 1UL };

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            CultureInfo culture = strategy.Generate(data);
            throw new Exception($"always fails: {culture.Name}");
        });

        Assert.False(result.Passed);
        ConjectureData replay = ConjectureData.ForRecord(result.Counterexample!);
        CultureInfo shrunk = strategy.Generate(replay);
        Assert.Equal(CultureInfo.InvariantCulture, shrunk);
    }

    [Fact]
    public void Cultures_DefaultIncludesInvariantCulture()
    {
        Strategy<CultureInfo> strategy = Strategy.Cultures();
        ConjectureData data = MakeData();
        bool found = false;

        for (int i = 0; i < 20000; i++)
        {
            CultureInfo value = strategy.Generate(data);
            if (value.Equals(CultureInfo.InvariantCulture))
            {
                found = true;
                break;
            }
        }

        Assert.True(found, "InvariantCulture was not produced in 20000 draws from Cultures()");
    }

    [Fact]
    public void Cultures_DeterministicWithSeed()
    {
        Strategy<CultureInfo> strategy = Strategy.Cultures();

        List<string> results1 = Enumerable.Range(0, 20)
            .Select(_ => strategy.Generate(MakeData(99UL)).Name)
            .ToList();
        List<string> results2 = Enumerable.Range(0, 20)
            .Select(_ => strategy.Generate(MakeData(99UL)).Name)
            .ToList();

        Assert.Equal(results1, results2);
    }
}