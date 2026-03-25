namespace Conjecture.Core.Strategies;

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
}
