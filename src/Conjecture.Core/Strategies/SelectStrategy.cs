using Conjecture.Core.Internal;

namespace Conjecture.Core.Generation;

internal sealed class SelectStrategy<TSource, TResult> : Strategy<TResult>
{
    private readonly Strategy<TSource> _source;
    private readonly Func<TSource, TResult> _selector;

    internal SelectStrategy(Strategy<TSource> source, Func<TSource, TResult> selector)
    {
        _source = source;
        _selector = selector;
    }

    internal override TResult Next(ConjectureData data) => _selector(_source.Next(data));
}
