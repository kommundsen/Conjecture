using Conjecture.Core.Internal;

namespace Conjecture.Core;

internal sealed class JustStrategy<T> : Strategy<T>
{
    private readonly T value;

    internal JustStrategy(T value) => this.value = value;

    internal override T Generate(ConjectureData data) => value;
}
