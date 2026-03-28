using Conjecture.Core.Internal;

namespace Conjecture.Core.Generation;

internal sealed class NullableStrategy<T> : Strategy<T?> where T : struct
{
    private readonly Strategy<T> inner;

    internal NullableStrategy(Strategy<T> inner) => this.inner = inner;

    internal override T? Next(ConjectureData data)
    {
        var isNull = data.DrawInteger(0, 9) == 0;
        return isNull ? null : inner.Next(data);
    }
}
