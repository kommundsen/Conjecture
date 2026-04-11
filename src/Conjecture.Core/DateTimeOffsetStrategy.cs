// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core.Internal;

namespace Conjecture.Core;

internal sealed class DateTimeOffsetStrategy : TickRangeStrategy<DateTimeOffset>
{
    internal DateTimeOffsetStrategy(DateTimeOffset min, DateTimeOffset max) : base(min.Ticks, max.Ticks)
    {
        if (min > max)
        {
            throw new ArgumentOutOfRangeException(nameof(min), "min must be less than or equal to max.");
        }
    }

    protected override DateTimeOffset FromTicks(long ticks) => new(ticks, TimeSpan.Zero);
}