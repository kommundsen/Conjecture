// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

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
    }

    private static readonly Lazy<CultureInfo[]> CulturesWithCurrencyCache = new(static () => BuildCulturesWithCurrency());

    private static CultureInfo[] BuildCulturesWithCurrency()
    {
        CultureInfo[] specificCultures = CultureInfo.GetCultures(CultureTypes.SpecificCultures);

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
        foreach (CultureInfo culture in specificCultures)
        {
            if (culture.Name == shrinkTarget.Name)
            {
                continue;
            }

            try
            {
                RegionInfo region = new(culture.Name);
                if (!string.IsNullOrEmpty(region.ISOCurrencySymbol))
                {
                    result.Add(culture);
                }
            }
            catch (ArgumentException)
            {
                // skip cultures that don't have a RegionInfo (includes CultureNotFoundException)
            }
        }

        return [.. result];
    }
}