using Conjecture.Core.Internal;

namespace Conjecture.Core;

internal sealed class JustStrategy<T>(T value) : Strategy<T>
{
    internal override T Generate(ConjectureData data) => value;
}
