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
}
