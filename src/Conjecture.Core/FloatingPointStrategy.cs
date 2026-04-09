// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

// Derived from the Python Hypothesis library.
// Original copyright: Copyright (c) 2013-present, David R. MacIver and contributors.

using System.Numerics;
using System.Runtime.CompilerServices;

using Conjecture.Core.Internal;

namespace Conjecture.Core;

internal sealed class FloatingPointStrategy<T> : Strategy<T>
    where T : struct, IBinaryFloatingPointIeee754<T>
{
    // Cached once per T instantiation; avoids recomputing on every bounded draw.
    private static readonly T MaxUlong = T.CreateSaturating(ulong.MaxValue);

    private static readonly ulong[] DoubleSpecialBits =
    [
        Unsafe.BitCast<double, ulong>(double.NaN),
        Unsafe.BitCast<double, ulong>(double.PositiveInfinity),
        Unsafe.BitCast<double, ulong>(double.NegativeInfinity),
        Unsafe.BitCast<double, ulong>(0.0),
        Unsafe.BitCast<double, ulong>(-0.0),
        Unsafe.BitCast<double, ulong>(double.MaxValue),
        Unsafe.BitCast<double, ulong>(double.MinValue),
        Unsafe.BitCast<double, ulong>(double.Epsilon),
    ];

    private static readonly uint[] FloatSpecialBits =
    [
        Unsafe.BitCast<float, uint>(float.NaN),
        Unsafe.BitCast<float, uint>(float.PositiveInfinity),
        Unsafe.BitCast<float, uint>(float.NegativeInfinity),
        Unsafe.BitCast<float, uint>(0f),
        Unsafe.BitCast<float, uint>(-0f),
        Unsafe.BitCast<float, uint>(float.MaxValue),
        Unsafe.BitCast<float, uint>(float.MinValue),
        Unsafe.BitCast<float, uint>(float.Epsilon),
    ];

    private readonly bool bounded;
    private readonly T min;
    private readonly T range;

    internal FloatingPointStrategy() { }

    internal FloatingPointStrategy(T min, T max)
    {
        if (max < min)
        {
            throw new ArgumentException($"max ({max}) must be >= min ({min}).", nameof(max));
        }


        bounded = true;
        this.min = min;
        range = max - min;
    }

    internal override T Generate(ConjectureData data)
    {
        if (bounded)
        {
            ulong raw = Unsafe.SizeOf<T>() == sizeof(double)
                ? data.NextFloat64(0UL, ulong.MaxValue)
                : data.NextFloat32(0UL, uint.MaxValue);
            var t = T.CreateSaturating(raw) / MaxUlong;
            return min + range * t;
        }

        var bias = data.NextInteger(0UL, 63UL);
        return DrawUnbounded(data, useSpecial: bias == 0);
    }

    private T DrawUnbounded(ConjectureData data, bool useSpecial)
    {
        if (Unsafe.SizeOf<T>() == sizeof(double))
        {
            var bits = useSpecial
                ? DoubleSpecialBits[(int)data.NextInteger(0UL, (ulong)(DoubleSpecialBits.Length - 1))]
                : data.NextFloat64(0UL, ulong.MaxValue);
            return Unsafe.BitCast<ulong, T>(bits);
        }
        if (Unsafe.SizeOf<T>() == sizeof(float))
        {
            var bits = useSpecial
                ? FloatSpecialBits[(int)data.NextInteger(0UL, (ulong)(FloatSpecialBits.Length - 1))]
                : (uint)data.NextFloat32(0UL, uint.MaxValue);
            return Unsafe.BitCast<uint, T>(bits);
        }
        throw new NotSupportedException($"FloatingPointStrategy does not support {typeof(T).Name} (size={Unsafe.SizeOf<T>()}).");
    }
}