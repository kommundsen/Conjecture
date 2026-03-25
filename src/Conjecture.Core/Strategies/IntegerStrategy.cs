using System.Numerics;
using Conjecture.Core.Internal;

namespace Conjecture.Core.Generation;

internal sealed class IntegerStrategy<T> : Strategy<T> where T : IBinaryInteger<T>
{
    private readonly T _min;
    private readonly T _max;

    internal IntegerStrategy(T min, T max)
    {
        _min = min;
        _max = max;
    }

    internal override T Next(ConjectureData data)
    {
        var rangeMinus1 = ulong.CreateTruncating(_max) - ulong.CreateTruncating(_min);
        var raw = data.DrawInteger(0UL, rangeMinus1);
        return _min + T.CreateTruncating(raw);
    }
}
