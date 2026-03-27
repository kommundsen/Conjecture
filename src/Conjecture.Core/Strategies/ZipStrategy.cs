using Conjecture.Core.Internal;

namespace Conjecture.Core.Generation;

internal sealed class ZipStrategy<TFirst, TSecond, TResult> : Strategy<TResult>
{
    private readonly Strategy<TFirst> first;
    private readonly Strategy<TSecond> second;
    private readonly Func<TFirst, TSecond, TResult> resultSelector;

    internal ZipStrategy(
        Strategy<TFirst> first,
        Strategy<TSecond> second,
        Func<TFirst, TSecond, TResult> resultSelector)
    {
        this.first = first;
        this.second = second;
        this.resultSelector = resultSelector;
    }

    internal override TResult Next(ConjectureData data) =>
        resultSelector(first.Next(data), second.Next(data));
}
