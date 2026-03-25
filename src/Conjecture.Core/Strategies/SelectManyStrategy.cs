using Conjecture.Core.Internal;

namespace Conjecture.Core.Generation;

internal sealed class SelectManyStrategy<TSource, TCollection, TResult> : Strategy<TResult>
{
    private readonly Strategy<TSource> _source;
    private readonly Func<TSource, Strategy<TCollection>> _collectionSelector;
    private readonly Func<TSource, TCollection, TResult> _resultSelector;

    internal SelectManyStrategy(
        Strategy<TSource> source,
        Func<TSource, Strategy<TCollection>> collectionSelector,
        Func<TSource, TCollection, TResult> resultSelector)
    {
        _source = source;
        _collectionSelector = collectionSelector;
        _resultSelector = resultSelector;
    }

    internal override TResult Next(ConjectureData data)
    {
        var s = _source.Next(data);
        var c = _collectionSelector(s).Next(data);
        return _resultSelector(s, c);
    }
}
