// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Concurrent;
using System.Globalization;

using Conjecture.Money.Internal;

namespace Conjecture.Core;

/// <summary>Extension methods on <see cref="Strategy"/> for monetary value generation.</summary>
public static class MoneyStrategyExtensions
{
    extension(Strategy)
    {
        /// <summary>Returns a strategy that generates <see cref="decimal"/> values within [<paramref name="min"/>, <paramref name="max"/>], optionally rounded to <paramref name="scale"/> decimal places.</summary>
        public static Strategy<decimal> Decimal(
            decimal min,
            decimal max,
            int? scale = null)
        {
            return min > max
                ? throw new ArgumentException("min must be less than or equal to max.", nameof(min))
                : scale is < 0 or > 28
                ? throw new ArgumentOutOfRangeException(nameof(scale), scale, "scale must be between 0 and 28.")
                : DecimalStrategy.Create(min, max, scale);
        }

        /// <summary>Returns a strategy that samples uniformly from all active ISO 4217 currency codes.</summary>
        public static Strategy<string> Iso4217Codes()
        {
            return Strategy.SampledFrom(Iso4217Data.DecimalPlacesByCurrency.Keys.ToArray());
        }

        /// <summary>Returns a strategy that generates a decimal amount for <paramref name="currencyCode"/> within [<paramref name="min"/>, <paramref name="max"/>].</summary>
        public static Strategy<decimal> Amounts(string currencyCode, decimal min = 0m, decimal max = 10_000m)
        {
            return !Iso4217Data.DecimalPlacesByCurrency.TryGetValue(currencyCode, out int scale)
                ? throw new ArgumentException($"Unknown ISO 4217 currency code: '{currencyCode}'.", nameof(currencyCode))
                : DecimalStrategy.Create(min, max, scale);
        }

        /// <summary>Returns a strategy that samples uniformly from all <see cref="MidpointRounding"/> values.</summary>
        public static Strategy<MidpointRounding> RoundingModes()
        {
            return Strategy.Enums<MidpointRounding>();
        }

        /// <summary>Returns a strategy that samples cultures whose <see cref="RegionInfo"/> exposes a non-empty <c>ISOCurrencySymbol</c>. Shrinks toward <c>en-US</c> (placed at index 0).</summary>
        public static Strategy<CultureInfo> CulturesWithCurrency()
        {
            return Strategy.SampledFrom(CulturesWithCurrencyCache.Value);
        }

        /// <summary>Returns a strategy that samples cultures whose <see cref="RegionInfo.ISOCurrencySymbol"/> equals <paramref name="currencyCode"/>. Shrinks toward the first matching culture (e.g. <c>en-US</c> for USD).</summary>
        public static Strategy<CultureInfo> CulturesByCurrencyCode(string currencyCode)
        {
            if (!Iso4217Data.DecimalPlacesByCurrency.ContainsKey(currencyCode))
            {
                throw new ArgumentException($"Unknown ISO 4217 currency code: '{currencyCode}'.", nameof(currencyCode));
            }

            CultureInfo[] cultures = CulturesByCurrencyCodeCache.GetOrAdd(currencyCode, BuildCulturesByCurrencyCode);
            return cultures.Length == 0
                ? throw new ArgumentException($"No culture on this host uses currency '{currencyCode}'.", nameof(currencyCode))
                : Strategy.SampledFrom(cultures);
        }
    }

    private static readonly Lazy<(CultureInfo Culture, string CurrencyCode)[]> SpecificCulturesWithRegionsCache =
        new(BuildSpecificCulturesWithRegions);

    private static readonly Lazy<CultureInfo[]> CulturesWithCurrencyCache = new(BuildCulturesWithCurrency);

    private static readonly ConcurrentDictionary<string, CultureInfo[]> CulturesByCurrencyCodeCache = new();

    private static CultureInfo[] BuildCulturesWithCurrency()
    {
        (CultureInfo Culture, string CurrencyCode)[] allCultures = SpecificCulturesWithRegionsCache.Value;

        CultureInfo? enUs = null;
        try
        {
            enUs = CultureInfo.GetCultureInfo("en-US");
        }
        catch (CultureNotFoundException)
        {
            // fall back to null; handled below
        }

        CultureInfo shrinkTarget = enUs ?? CultureInfo.InvariantCulture;

        List<CultureInfo> result = [shrinkTarget];
        foreach ((CultureInfo culture, string _) in allCultures)
        {
            if (culture.Name != shrinkTarget.Name)
            {
                result.Add(culture);
            }
        }

        return [.. result];
    }

    private static (CultureInfo Culture, string CurrencyCode)[] BuildSpecificCulturesWithRegions()
    {
        CultureInfo[] specificCultures = CultureInfo.GetCultures(CultureTypes.SpecificCultures);
        List<(CultureInfo Culture, string CurrencyCode)> result = new(specificCultures.Length);

        foreach (CultureInfo culture in specificCultures)
        {
            try
            {
                RegionInfo region = new(culture.Name);
                if (!string.IsNullOrEmpty(region.ISOCurrencySymbol))
                {
                    result.Add((culture, region.ISOCurrencySymbol));
                }
            }
            catch (ArgumentException)
            {
                // skip cultures that don't have a valid RegionInfo
            }
        }

        return [.. result];
    }

    private static readonly Dictionary<string, string> PreferredCultureByCurrencyCode = new()
    {
        ["USD"] = "en-US",
        ["GBP"] = "en-GB",
        ["EUR"] = "de-DE",
        ["JPY"] = "ja-JP",
        ["CAD"] = "en-CA",
        ["AUD"] = "en-AU",
        ["CHF"] = "de-CH",
        ["CNY"] = "zh-CN",
        ["HKD"] = "zh-HK",
        ["NZD"] = "en-NZ",
        ["SEK"] = "sv-SE",
        ["NOK"] = "nb-NO",
        ["DKK"] = "da-DK",
        ["MXN"] = "es-MX",
        ["SGD"] = "zh-SG",
        ["INR"] = "hi-IN",
        ["BRL"] = "pt-BR",
        ["ZAR"] = "af-ZA",
        ["KRW"] = "ko-KR",
        ["RUB"] = "ru-RU",
    };

    private static CultureInfo[] BuildCulturesByCurrencyCode(string currencyCode)
    {
        (CultureInfo Culture, string CurrencyCode)[] allCultures = SpecificCulturesWithRegionsCache.Value;
        List<CultureInfo> matching = [];

        foreach ((CultureInfo culture, string code) in allCultures)
        {
            if (code == currencyCode)
            {
                matching.Add(culture);
            }
        }

        if (matching.Count == 0)
        {
            return [];
        }

        // Place preferred culture at index 0 for shrink direction
        if (PreferredCultureByCurrencyCode.TryGetValue(currencyCode, out string? preferredName))
        {
            int preferredIndex = matching.FindIndex(c => c.Name == preferredName);
            if (preferredIndex > 0)
            {
                CultureInfo preferred = matching[preferredIndex];
                matching.RemoveAt(preferredIndex);
                matching.Insert(0, preferred);
            }
            else if (preferredIndex < 0)
            {
                // Preferred not found — sort by name and use first
                matching.Sort(static (a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));
            }
        }
        else
        {
            // No preferred culture for this code — sort by name and use first
            matching.Sort(static (a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));
        }

        return [.. matching];
    }
}