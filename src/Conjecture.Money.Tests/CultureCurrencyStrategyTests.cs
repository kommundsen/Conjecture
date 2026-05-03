// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Globalization;

using Conjecture.Core;
using Conjecture.Core.Internal;

namespace Conjecture.Money.Tests;

public class CultureCurrencyStrategyTests
{
    [Fact]
    public void CulturesWithCurrency_All_Have_NonEmpty_ISOCurrencySymbol()
    {
        Strategy<CultureInfo> strategy = Strategy.CulturesWithCurrency();
        IReadOnlyList<CultureInfo> samples = strategy.WithSeed(1UL).Sample(200);

        Assert.All(samples, culture =>
        {
            RegionInfo region = new(culture.Name);
            Assert.False(string.IsNullOrEmpty(region.ISOCurrencySymbol),
                $"Culture '{culture.Name}' has an empty ISOCurrencySymbol");
        });
    }

    [Fact]
    public void CulturesWithCurrency_All_Are_Specific_Cultures()
    {
        Strategy<CultureInfo> strategy = Strategy.CulturesWithCurrency();
        IReadOnlyList<CultureInfo> samples = strategy.WithSeed(2UL).Sample(200);

        Assert.All(samples, culture =>
            Assert.False(culture.IsNeutralCulture,
                $"Culture '{culture.Name}' is a neutral culture"));
    }

    [Fact]
    public void CulturesWithCurrency_ShrinksToEnUs()
    {
        // en-US is placed at index 0 so SampledFrom shrinks toward it.
        // The smallest draw from SampledFrom always resolves to index 0.
        Strategy<CultureInfo> strategy = Strategy.CulturesWithCurrency();
        IReadOnlyList<CultureInfo> samples = strategy.WithSeed(3UL).Sample(500);

        string expectedName = CultureInfo.GetCultures(CultureTypes.SpecificCultures)
            .Any(static c => c.Name == "en-US")
            ? "en-US"
            : CultureInfo.InvariantCulture.Name;

        bool found = samples.Any(c => c.Name == expectedName);
        Assert.True(found, $"Expected '{expectedName}' (shrink target at index 0) to appear in 500 samples");
    }

    [Fact]
    public async Task CulturesWithCurrency_ShrinksToEnUsViaTestRunner()
    {
        Strategy<CultureInfo> strategy = Strategy.CulturesWithCurrency();
        ConjectureSettings settings = new() { MaxExamples = 200, Seed = 5UL };

        string expectedName = CultureInfo.GetCultures(CultureTypes.SpecificCultures)
            .Any(static c => c.Name == "en-US")
            ? "en-US"
            : CultureInfo.InvariantCulture.Name;

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            CultureInfo culture = strategy.Generate(data);
            throw new Exception($"always fails: {culture.Name}");
        });

        Assert.False(result.Passed);
        ConjectureData replay = ConjectureData.ForRecord(result.Counterexample!);
        CultureInfo shrunk = strategy.Generate(replay);
        Assert.Equal(expectedName, shrunk.Name);
    }

    [Fact]
    public void CulturesWithCurrency_IsDeterministic()
    {
        Strategy<CultureInfo> strategy = Strategy.CulturesWithCurrency();
        IReadOnlyList<CultureInfo> first = strategy.WithSeed(4UL).Sample(100);
        IReadOnlyList<CultureInfo> second = strategy.WithSeed(4UL).Sample(100);

        Assert.Equal(
            first.Select(static c => c.Name).ToList(),
            second.Select(static c => c.Name).ToList());
    }
}
