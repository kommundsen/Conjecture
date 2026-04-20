// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core.Internal;

namespace Conjecture.Core;

internal sealed class DecimalStrategy : Strategy<decimal>
{
    private readonly decimal? min;
    private readonly decimal? max;

    internal DecimalStrategy() { }

    internal DecimalStrategy(decimal rangeMin, decimal rangeMax)
    {
        min = rangeMin;
        max = rangeMax;
    }

    internal override decimal Generate(ConjectureData data)
    {
        int lo = (int)(data.NextInteger(0UL, (ulong)int.MaxValue));
        int mid = (int)(data.NextInteger(0UL, (ulong)int.MaxValue));
        int hi = (int)(data.NextInteger(0UL, (ulong)int.MaxValue));
        bool isNegative = data.NextInteger(0UL, 1UL) == 1UL;
        byte scale = (byte)data.NextInteger(0UL, 28UL);
        decimal value = new(lo, mid, hi, isNegative, scale);
        return min.HasValue && max.HasValue ? Math.Clamp(value, min.Value, max.Value) : value;
    }
}

