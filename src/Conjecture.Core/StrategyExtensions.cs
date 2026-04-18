// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

// Derived from the Python Hypothesis library.
// Original copyright: Copyright (c) 2013-present, David R. MacIver and contributors.

using Conjecture.Core.Internal;

namespace Conjecture.Core;

/// <summary>LINQ-style extension methods for composing strategies.</summary>
public static class StrategyExtensions
{
    /// <summary>Projects each generated value through <paramref name="selector"/>.</summary>
    public static Strategy<TResult> Select<TSource, TResult>(
        this Strategy<TSource> source, Func<TSource, TResult> selector)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);
        return new SelectStrategy<TSource, TResult>(source, selector);
    }

    /// <summary>Filters generated values to those satisfying <paramref name="predicate"/>.</summary>
    public static Strategy<T> Where<T>(this Strategy<T> source, Func<T, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);
        return new WhereStrategy<T>(source, predicate);
    }

    /// <summary>Projects each generated value to a strategy and flattens the result.</summary>
    public static Strategy<TResult> SelectMany<TSource, TResult>(
        this Strategy<TSource> source,
        Func<TSource, Strategy<TResult>> selector)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);
        return new SelectManyStrategy<TSource, TResult, TResult>(source, selector, (_, r) => r);
    }

    /// <summary>Projects each generated value to a strategy, flattens, and applies a result selector (enables C# query syntax).</summary>
    public static Strategy<TResult> SelectMany<TSource, TCollection, TResult>(
        this Strategy<TSource> source,
        Func<TSource, Strategy<TCollection>> collectionSelector,
        Func<TSource, TCollection, TResult> resultSelector)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(collectionSelector);
        ArgumentNullException.ThrowIfNull(resultSelector);
        return new SelectManyStrategy<TSource, TCollection, TResult>(source, collectionSelector, resultSelector);
    }

    /// <summary>Combines two strategies into a strategy of tuples.</summary>
    public static Strategy<(TFirst, TSecond)> Zip<TFirst, TSecond>(
        this Strategy<TFirst> first, Strategy<TSecond> second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        return new ZipStrategy<TFirst, TSecond, (TFirst, TSecond)>(first, second, (a, b) => (a, b));
    }

    /// <summary>Combines two strategies using a result selector.</summary>
    public static Strategy<TResult> Zip<TFirst, TSecond, TResult>(
        this Strategy<TFirst> first, Strategy<TSecond> second, Func<TFirst, TSecond, TResult> resultSelector)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        ArgumentNullException.ThrowIfNull(resultSelector);
        return new ZipStrategy<TFirst, TSecond, TResult>(first, second, resultSelector);
    }

    /// <summary>Wraps the strategy so it may also produce null, with ~10% null probability.</summary>
    public static Strategy<T?> OrNull<T>(this Strategy<T> source) where T : struct
    {
        ArgumentNullException.ThrowIfNull(source);
        return new NullableStrategy<T>(source);
    }

    /// <summary>Annotates the strategy with a label used in counterexample output.</summary>
    public static Strategy<T> WithLabel<T>(this Strategy<T> source, string label)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(label);
        return new LabeledStrategy<T>(source, label);
    }

    /// <summary>Projects each generated value through a direct generator that receives both the source value and the data stream. Internal hot-path overload — avoids per-Generate Strategy allocation.</summary>
    internal static Strategy<TResult> SelectMany<TSource, TResult>(
        this Strategy<TSource> source,
        Func<TSource, ConjectureData, TResult> directGenerator)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(directGenerator);
        return new SelectManyDirectStrategy<TSource, TResult>(source, directGenerator);
    }
}

