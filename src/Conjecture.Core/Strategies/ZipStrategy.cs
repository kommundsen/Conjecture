using Conjecture.Core.Internal;

namespace Conjecture.Core.Generation;

internal sealed class ZipStrategy<TFirst, TSecond, TResult> : Strategy<TResult>
{
    private readonly Strategy<TFirst> _first;
    private readonly Strategy<TSecond> _second;
    private readonly Func<TFirst, TSecond, TResult> _resultSelector;

    internal ZipStrategy(
        Strategy<TFirst> first,
        Strategy<TSecond> second,
        Func<TFirst, TSecond, TResult> resultSelector)
    {
        _first = first;
        _second = second;
        _resultSelector = resultSelector;
    }

    internal override TResult Next(ConjectureData data) =>
        _resultSelector(_first.Next(data), _second.Next(data));
}
