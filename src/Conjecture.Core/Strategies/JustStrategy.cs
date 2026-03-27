using Conjecture.Core.Internal;

namespace Conjecture.Core.Generation;

internal sealed class JustStrategy<T> : Strategy<T>
{
    private readonly T value;

    internal JustStrategy(T value) => this.value = value;

    internal override T Next(ConjectureData data) => value;
}
