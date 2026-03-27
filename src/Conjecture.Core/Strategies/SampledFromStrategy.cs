using Conjecture.Core.Internal;

namespace Conjecture.Core.Generation;

internal sealed class SampledFromStrategy<T> : Strategy<T>
{
    private readonly IReadOnlyList<T> values;
    private readonly ulong lastIndex;

    internal SampledFromStrategy(IReadOnlyList<T> values)
    {
        if (values.Count == 0)
            throw new ArgumentException("At least one value is required.", nameof(values));
        this.values = values;
        lastIndex = (ulong)(values.Count - 1);
    }

    internal override T Next(ConjectureData data)
    {
        var index = (int)data.DrawInteger(0, lastIndex);
        return values[index];
    }
}
