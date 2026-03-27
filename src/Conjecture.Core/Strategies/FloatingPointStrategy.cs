using System.Numerics;
using System.Runtime.CompilerServices;
using Conjecture.Core.Internal;

namespace Conjecture.Core.Generation;

internal sealed class FloatingPointStrategy<T> : Strategy<T>
    where T : struct, IBinaryFloatingPointIeee754<T>
{
    // Cached once per T instantiation; avoids recomputing on every bounded draw.
    private static readonly T MaxUlong = T.CreateSaturating(ulong.MaxValue);

    private readonly bool bounded;
    private readonly T min;
    private readonly T range;

    internal FloatingPointStrategy() { }

    internal FloatingPointStrategy(T min, T max)
    {
        if (max < min)
            throw new ArgumentException($"max ({max}) must be >= min ({min}).", nameof(max));
        bounded = true;
        this.min = min;
        range = max - min;
    }

    internal override T Next(ConjectureData data)
    {
        if (bounded)
        {
            var raw = data.DrawInteger(0UL, ulong.MaxValue);
            var t = T.CreateSaturating(raw) / MaxUlong;
            return min + range * t;
        }

        if (Unsafe.SizeOf<T>() == sizeof(double))
        {
            var bits = data.DrawInteger(0UL, ulong.MaxValue);
            return Unsafe.BitCast<ulong, T>(bits);
        }
        else if (Unsafe.SizeOf<T>() == sizeof(float))
        {
            var bits = (uint)data.DrawInteger(0UL, uint.MaxValue);
            return Unsafe.BitCast<uint, T>(bits);
        }
        else
        {
            throw new NotSupportedException($"FloatingPointStrategy does not support {typeof(T).Name} (size={Unsafe.SizeOf<T>()}).");
        }
    }
}
