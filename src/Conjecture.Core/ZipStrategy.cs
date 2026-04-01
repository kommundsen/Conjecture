using Conjecture.Core.Internal;

namespace Conjecture.Core;

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

    internal override TResult Generate(ConjectureData data) =>
        resultSelector(first.Generate(data), second.Generate(data));
}
