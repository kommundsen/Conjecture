// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

namespace Conjecture.Core.Internal;

internal abstract class TickRangeStrategy<T>(long minTicks, long maxTicks) : Strategy<T>
{
    internal sealed override T Generate(ConjectureData data)
    {
        ulong rangeMinus1 = unchecked((ulong)(maxTicks - minTicks));
        ulong raw = data.NextInteger(0UL, rangeMinus1);
        return FromTicks(minTicks + (long)raw);
    }

    protected abstract T FromTicks(long ticks);
}