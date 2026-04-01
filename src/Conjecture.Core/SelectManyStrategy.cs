using Conjecture.Core.Internal;

namespace Conjecture.Core;

internal sealed class SelectManyStrategy<TSource, TCollection, TResult>(
    Strategy<TSource> source,
    Func<TSource, Strategy<TCollection>> collectionSelector,
    Func<TSource, TCollection, TResult> resultSelector) : Strategy<TResult>
{
    internal override TResult Generate(ConjectureData data)
    {
        var s = source.Generate(data);
        var c = collectionSelector(s).Generate(data);
        return resultSelector(s, c);
    }
}
