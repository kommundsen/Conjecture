// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Money.Internal;

namespace Conjecture.Core;

/// <summary>Extension methods on <see cref="Generate"/> for monetary value generation.</summary>
public static class MoneyGenerateExtensions
{
    extension(Generate)
    {
        /// <summary>Returns a strategy that generates <see cref="decimal"/> values within [<paramref name="min"/>, <paramref name="max"/>], optionally rounded to <paramref name="scale"/> decimal places.</summary>
        public static Strategy<decimal> Decimal(
            decimal min,
            decimal max,
            int? scale = null)
        {
            if (min > max)
            {
                throw new ArgumentException("min must be less than or equal to max.", nameof(min));
            }

            if (scale is < 0 or > 28)
            {
                throw new ArgumentOutOfRangeException(nameof(scale), scale, "scale must be between 0 and 28.");
            }

            return DecimalStrategy.Create(min, max, scale);
        }

        /// <summary>Returns a strategy that samples uniformly from all active ISO 4217 currency codes.</summary>
        public static Strategy<string> Iso4217Codes()
        {
            return Generate.SampledFrom(Iso4217Data.DecimalPlacesByCurrency.Keys.ToArray());
        }

        /// <summary>Returns a strategy that generates a decimal amount for <paramref name="currencyCode"/> within [<paramref name="min"/>, <paramref name="max"/>].</summary>
        public static Strategy<decimal> Amounts(string currencyCode, decimal min = 0m, decimal max = 10_000m)
        {
            if (!Iso4217Data.DecimalPlacesByCurrency.TryGetValue(currencyCode, out int scale))
            {
                throw new ArgumentException($"Unknown ISO 4217 currency code: '{currencyCode}'.", nameof(currencyCode));
            }

            return DecimalStrategy.Create(min, max, scale);
        }

        /// <summary>Returns a strategy that samples uniformly from all <see cref="MidpointRounding"/> values.</summary>
        public static Strategy<MidpointRounding> RoundingModes()
        {
            return Generate.Enums<MidpointRounding>();
        }
    }
}
