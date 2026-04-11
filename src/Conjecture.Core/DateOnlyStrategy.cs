// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core.Internal;

namespace Conjecture.Core;

internal sealed class DateOnlyStrategy : Strategy<DateOnly>
{
    private readonly DateOnly min;
    private readonly DateOnly max;

    internal DateOnlyStrategy(DateOnly min, DateOnly max)
    {
        if (min > max)
        {
            throw new ArgumentOutOfRangeException(nameof(min), "min must be less than or equal to max.");
        }

        this.min = min;
        this.max = max;
    }

    internal override DateOnly Generate(ConjectureData data)
    {
        ulong rangeMinus1 = (ulong)(max.DayNumber - min.DayNumber);
        ulong raw = data.NextInteger(0UL, rangeMinus1);
        return DateOnly.FromDayNumber(min.DayNumber + (int)raw);
    }
}