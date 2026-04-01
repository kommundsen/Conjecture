using Conjecture.Core.Internal;

namespace Conjecture.Core;

internal sealed class SelectManyStrategy<TSource, TCollection, TResult> : Strategy<TResult>
{
    private readonly Strategy<TSource> source;
    private readonly Func<TSource, Strategy<TCollection>> collectionSelector;
    private readonly Func<TSource, TCollection, TResult> resultSelector;

    internal SelectManyStrategy(
        Strategy<TSource> source,
        Func<TSource, Strategy<TCollection>> collectionSelector,
        Func<TSource, TCollection, TResult> resultSelector)
    {
        this.source = source;
        this.collectionSelector = collectionSelector;
        this.resultSelector = resultSelector;
    }

    internal override TResult Generate(ConjectureData data)
    {
        var s = source.Generate(data);
        var c = collectionSelector(s).Generate(data);
        return resultSelector(s, c);
    }
}
