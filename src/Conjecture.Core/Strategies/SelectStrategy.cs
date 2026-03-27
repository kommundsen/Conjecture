using Conjecture.Core.Internal;

namespace Conjecture.Core.Generation;

internal sealed class SelectStrategy<TSource, TResult> : Strategy<TResult>
{
    private readonly Strategy<TSource> source;
    private readonly Func<TSource, TResult> selector;

    internal SelectStrategy(Strategy<TSource> source, Func<TSource, TResult> selector)
    {
        this.source = source;
        this.selector = selector;
    }

    internal override TResult Next(ConjectureData data) => selector(source.Next(data));
}
