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
    }
}
