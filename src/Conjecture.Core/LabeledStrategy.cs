using Conjecture.Core.Internal;

namespace Conjecture.Core;

internal sealed class LabeledStrategy<T>(Strategy<T> inner, string label) : Strategy<T>(label)
{
    internal override T Generate(ConjectureData data) => inner.Generate(data);
}
