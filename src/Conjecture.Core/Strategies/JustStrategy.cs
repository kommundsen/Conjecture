using Conjecture.Core.Internal;

namespace Conjecture.Core.Generation;

internal sealed class JustStrategy<T> : Strategy<T>
{
    private readonly T _value;

    internal JustStrategy(T value) => _value = value;

    internal override T Next(ConjectureData data) => _value;
}
