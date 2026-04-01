using Conjecture.Core.Internal;

namespace Conjecture.Core;

internal sealed class OneOfStrategy<T>(Strategy<T>[] strategies) : Strategy<T>
{
    private readonly Strategy<T>[] strategies = strategies.Length > 0
        ? strategies
        : throw new ArgumentException("At least one strategy is required.", nameof(strategies));

    internal override T Generate(ConjectureData data)
    {
        var index = (int)data.NextInteger(0, (ulong)(strategies.Length - 1));
        return strategies[index].Generate(data);
    }
}
