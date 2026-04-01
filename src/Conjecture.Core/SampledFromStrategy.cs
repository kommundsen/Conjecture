using Conjecture.Core.Internal;

namespace Conjecture.Core;

internal sealed class SampledFromStrategy<T>(IReadOnlyList<T> values) : Strategy<T>
{
    private readonly ulong lastIndex = values.Count > 0
        ? (ulong)(values.Count - 1)
        : throw new ArgumentException("At least one value is required.", nameof(values));

    internal override T Generate(ConjectureData data)
    {
        var index = (int)data.NextInteger(0, lastIndex);
        return values[index];
    }
}
