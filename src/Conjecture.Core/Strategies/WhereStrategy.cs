using Conjecture.Core.Internal;

namespace Conjecture.Core.Generation;

internal sealed class WhereStrategy<T> : Strategy<T>
{
    private const int MaxAttempts = 200;
    private readonly Strategy<T> _source;
    private readonly Func<T, bool> _predicate;

    internal WhereStrategy(Strategy<T> source, Func<T, bool> predicate)
    {
        _source = source;
        _predicate = predicate;
    }

    internal override T Next(ConjectureData data)
    {
        for (var i = 0; i < MaxAttempts; i++)
        {
            var value = _source.Next(data);
            if (_predicate(value)) return value;
        }
        data.MarkInvalid();
        throw new UnsatisfiedAssumptionException();
    }
}
