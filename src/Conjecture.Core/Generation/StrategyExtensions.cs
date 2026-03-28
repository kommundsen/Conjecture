namespace Conjecture.Core.Generation;

/// <summary>LINQ-style extension methods for composing strategies.</summary>
public static class StrategyExtensions
{
    /// <summary>Projects each generated value through <paramref name="selector"/>.</summary>
    public static Strategy<TResult> Select<TSource, TResult>(
        this Strategy<TSource> source, Func<TSource, TResult> selector) =>
        new SelectStrategy<TSource, TResult>(source, selector);

    /// <summary>Filters generated values to those satisfying <paramref name="predicate"/>.</summary>
    public static Strategy<T> Where<T>(this Strategy<T> source, Func<T, bool> predicate) =>
        new WhereStrategy<T>(source, predicate);

    /// <summary>Projects each generated value to a strategy and flattens the result.</summary>
    public static Strategy<TResult> SelectMany<TSource, TResult>(
        this Strategy<TSource> source,
        Func<TSource, Strategy<TResult>> selector) =>
        new SelectManyStrategy<TSource, TResult, TResult>(source, selector, (_, r) => r);

    /// <summary>Projects each generated value to a strategy, flattens, and applies a result selector (enables C# query syntax).</summary>
    public static Strategy<TResult> SelectMany<TSource, TCollection, TResult>(
        this Strategy<TSource> source,
        Func<TSource, Strategy<TCollection>> collectionSelector,
        Func<TSource, TCollection, TResult> resultSelector) =>
        new SelectManyStrategy<TSource, TCollection, TResult>(source, collectionSelector, resultSelector);

    /// <summary>Combines two strategies into a strategy of tuples.</summary>
    public static Strategy<(TFirst, TSecond)> Zip<TFirst, TSecond>(
        this Strategy<TFirst> first, Strategy<TSecond> second) =>
        new ZipStrategy<TFirst, TSecond, (TFirst, TSecond)>(first, second, (a, b) => (a, b));

    /// <summary>Combines two strategies using a result selector.</summary>
    public static Strategy<TResult> Zip<TFirst, TSecond, TResult>(
        this Strategy<TFirst> first, Strategy<TSecond> second, Func<TFirst, TSecond, TResult> resultSelector) =>
        new ZipStrategy<TFirst, TSecond, TResult>(first, second, resultSelector);

    /// <summary>Wraps the strategy so it may also produce null, with ~10% null probability.</summary>
    public static Strategy<T?> OrNull<T>(this Strategy<T> source) where T : struct =>
        new NullableStrategy<T>(source);
}
