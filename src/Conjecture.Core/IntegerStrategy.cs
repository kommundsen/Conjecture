using System.Numerics;
using Conjecture.Core.Internal;

namespace Conjecture.Core;

internal sealed class IntegerStrategy<T> : Strategy<T> where T : IBinaryInteger<T>
{
    private readonly T min;
    private readonly T max;

    internal IntegerStrategy(T min, T max)
    {
        this.min = min;
        this.max = max;
    }

    internal override T Generate(ConjectureData data)
    {
        var rangeMinus1 = ulong.CreateTruncating(max) - ulong.CreateTruncating(min);
        var raw = data.NextInteger(0UL, rangeMinus1);
        return min + T.CreateTruncating(raw);
    }
}
