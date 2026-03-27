using Conjecture.Core.Internal;

namespace Conjecture.Core.Generation;

internal sealed class WhereStrategy<T> : Strategy<T>
{
    private const int MaxAttempts = 200;
    private readonly Strategy<T> source;
    private readonly Func<T, bool> predicate;

    internal WhereStrategy(Strategy<T> source, Func<T, bool> predicate)
    {
        this.source = source;
        this.predicate = predicate;
    }

    internal override T Next(ConjectureData data)
    {
        for (var i = 0; i < MaxAttempts; i++)
        {
            var value = source.Next(data);
            if (predicate(value)) return value;
        }
        data.MarkInvalid();
        throw new UnsatisfiedAssumptionException();
    }
}
