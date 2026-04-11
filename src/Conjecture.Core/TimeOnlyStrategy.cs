// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core.Internal;

namespace Conjecture.Core;

internal sealed class TimeOnlyStrategy : TickRangeStrategy<TimeOnly>
{
    internal TimeOnlyStrategy(TimeOnly min, TimeOnly max) : base(min.Ticks, max.Ticks)
    {
        if (min > max)
        {
            throw new ArgumentOutOfRangeException(nameof(min), "min must be less than or equal to max.");
        }
    }

    protected override TimeOnly FromTicks(long ticks) => new(ticks);
}