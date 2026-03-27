using System.Numerics;
using System.Runtime.CompilerServices;
using Conjecture.Core.Internal;

namespace Conjecture.Core.Generation;

internal sealed class FloatingPointStrategy<T> : Strategy<T>
    where T : IBinaryFloatingPointIeee754<T>
{
    internal override T Next(ConjectureData data)
    {
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
