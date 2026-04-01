using Conjecture.Core.Internal;

namespace Conjecture.Core;

internal sealed class NullableStrategy<T>(Strategy<T> inner) : Strategy<T?> where T : struct
{
    internal override T? Generate(ConjectureData data)
    {
        var isNull = data.NextInteger(0, 9) == 0;
        return isNull ? null : inner.Generate(data);
    }
}
