using Conjecture.Core.Internal;

namespace Conjecture.Core.Generation;

internal sealed class LabeledStrategy<T>(Strategy<T> inner, string label) : Strategy<T>(label)
{
    internal override T Next(ConjectureData data) => inner.Next(data);
}
