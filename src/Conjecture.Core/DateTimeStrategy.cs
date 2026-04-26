// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core.Internal;

namespace Conjecture.Core;

internal sealed class DateTimeStrategy : Strategy<DateTime>
{
    private readonly DateTime min;
    private readonly DateTime max;

    internal DateTimeStrategy(DateTime min, DateTime max)
    {
        if (min > max)
        {
            throw new ArgumentOutOfRangeException(nameof(min), "min must be less than or equal to max.");
        }

        this.min = min;
        this.max = max;
    }

    internal override DateTime Generate(ConjectureData data)
    {
        ulong rangeMinus1 = (ulong)(max.Ticks - min.Ticks);
        ulong raw = data.NextInteger(0UL, rangeMinus1);
        return new DateTime(min.Ticks + (long)raw, min.Kind);
    }
}