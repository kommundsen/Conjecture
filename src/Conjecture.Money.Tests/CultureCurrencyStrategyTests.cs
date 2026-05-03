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

    [Fact]
    public void CulturesByCurrencyCode_USD_All_Use_USD()
    {
        Strategy<CultureInfo> strategy = Strategy.CulturesByCurrencyCode("USD");
        IReadOnlyList<CultureInfo> samples = strategy.WithSeed(10UL).Sample(100);

        Assert.All(samples, culture =>
        {
            RegionInfo region = new(culture.Name);
            Assert.Equal("USD", region.ISOCurrencySymbol);
        });
    }

    [Fact]
    public void CulturesByCurrencyCode_EUR_All_Use_EUR()
    {
        Strategy<CultureInfo> strategy = Strategy.CulturesByCurrencyCode("EUR");
        IReadOnlyList<CultureInfo> samples = strategy.WithSeed(11UL).Sample(100);

        Assert.All(samples, culture =>
        {
            RegionInfo region = new(culture.Name);
            Assert.Equal("EUR", region.ISOCurrencySymbol);
        });
    }

    [Fact]
    public void CulturesByCurrencyCode_Unknown_Throws()
    {
        ArgumentException ex = Assert.Throws<ArgumentException>(() => Strategy.CulturesByCurrencyCode("ZZZ"));
        Assert.Contains("ZZZ", ex.Message);
    }

    [Fact]
    public void CulturesByCurrencyCode_LowerCase_ThrowsArgumentException()
    {
        // Case-sensitive to match Iso4217Data keys directly — "usd" is not a known code.
        Assert.Throws<ArgumentException>(() => Strategy.CulturesByCurrencyCode("usd"));
    }

    [Fact]
    public async Task CulturesByCurrencyCode_USD_ShrinksToEnUs()
    {
        Strategy<CultureInfo> strategy = Strategy.CulturesByCurrencyCode("USD");
        ConjectureSettings settings = new() { MaxExamples = 200, Seed = 12UL };

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            CultureInfo culture = strategy.Generate(data);
            throw new Exception($"always fails: {culture.Name}");
        });

        Assert.False(result.Passed);
        ConjectureData replay = ConjectureData.ForRecord(result.Counterexample!);
        CultureInfo shrunk = strategy.Generate(replay);
        Assert.Equal("en-US", shrunk.Name);
    }

    [Fact]
    public async Task CulturesByCurrencyCode_USD_AmountRoundTripsViaToString()
    {
        Strategy<(decimal Amount, CultureInfo Culture)> strategy =
            Strategy.Amounts("USD").Zip(Strategy.CulturesByCurrencyCode("USD"));
        ConjectureSettings settings = new() { MaxExamples = 100, Seed = 13UL };

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            (decimal amount, CultureInfo culture) = strategy.Generate(data);
            decimal parsed = decimal.Parse(
                amount.ToString("C", culture),
                System.Globalization.NumberStyles.Currency,
                culture);
            if (parsed != amount)
            {
                throw new Exception($"Round-trip failed: {amount} -> '{amount.ToString("C", culture)}' -> {parsed} for culture '{culture.Name}'");
            }
        });

        Assert.True(result.Passed);
    }
}
