// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core;

namespace Conjecture.Money.Internal;

internal static class DecimalStrategy
{
    private const int DefaultScale = 6;

    internal static Strategy<decimal> Create(decimal min, decimal max, int? scale)
    {
        int effectiveScale = scale ?? DefaultScale;
        decimal multiplier = DecimalPow10(effectiveScale);
        decimal scaledMin = Math.Truncate(min * multiplier);
        decimal scaledMax = Math.Truncate(max * multiplier);

        if (scaledMin > scaledMax)
        {
            scaledMax = scaledMin;
        }

        if (scaledMin < (decimal)long.MinValue || scaledMax > (decimal)long.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(min),
                "The combination of min, max, and scale produces a range that exceeds the supported precision. Reduce the range or scale.");
        }

        long iMin = (long)scaledMin;
        long iMax = (long)scaledMax;
        Strategy<long> inner = Strategy.Integers<long>(iMin, iMax);

        return Strategy.Compose<decimal>(ctx =>
        {
            long raw = ctx.Generate(inner);
            decimal result = (decimal)raw / multiplier;

            return scale.HasValue
                ? Math.Round(result, scale.Value, MidpointRounding.AwayFromZero)
                : result;
        });
    }

    private static decimal DecimalPow10(int exponent)
    {
        decimal result = 1m;
        for (int i = 0; i < exponent; i++)
        {
            result *= 10m;
        }

        return result;
    }
}