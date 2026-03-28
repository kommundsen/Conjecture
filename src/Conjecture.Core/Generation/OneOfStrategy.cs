using Conjecture.Core.Internal;

namespace Conjecture.Core.Generation;

internal sealed class OneOfStrategy<T> : Strategy<T>
{
    private readonly Strategy<T>[] strategies;

    internal OneOfStrategy(Strategy<T>[] strategies)
    {
        if (strategies.Length == 0)
        {
            throw new ArgumentException("At least one strategy is required.", nameof(strategies));
        }


        this.strategies = strategies;
    }

    internal override T Next(ConjectureData data)
    {
        var index = (int)data.DrawInteger(0, (ulong)(strategies.Length - 1));
        return strategies[index].Next(data);
    }
}
