// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

// Derived from the Python Hypothesis library.
// Original copyright: Copyright (c) 2013-present, David R. MacIver and contributors.

using Conjecture.Core.Internal;

namespace Conjecture.Core;

/// <summary>LINQ-style extension methods for composing strategies.</summary>
public static class StrategyExtensions
{
    extension<T>(Strategy<T> source)
    {
        /// <summary>Projects each generated value through <paramref name="selector"/>.</summary>
        public Strategy<TResult> Select<TResult>(Func<T, TResult> selector)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(selector);
            return new SelectStrategy<T, TResult>(source, selector);
        }

        /// <summary>Filters generated values to those satisfying <paramref name="predicate"/>.</summary>
        public Strategy<T> Where(Func<T, bool> predicate)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(predicate);
            return new WhereStrategy<T>(source, predicate);
        }

        /// <summary>Projects each generated value to a strategy and flattens the result.</summary>
        public Strategy<TResult> SelectMany<TResult>(Func<T, Strategy<TResult>> selector)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(selector);
            return new SelectManyStrategy<T, TResult, TResult>(source, selector, (_, r) => r);
        }

        /// <summary>Projects each generated value to a strategy, flattens, and applies a result selector (enables C# query syntax).</summary>
        public Strategy<TResult> SelectMany<TCollection, TResult>(
            Func<T, Strategy<TCollection>> collectionSelector,
            Func<T, TCollection, TResult> resultSelector)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(collectionSelector);
            ArgumentNullException.ThrowIfNull(resultSelector);
            return new SelectManyStrategy<T, TCollection, TResult>(source, collectionSelector, resultSelector);
        }

        /// <summary>Combines two strategies into a strategy of tuples.</summary>
        public Strategy<(T, TSecond)> Zip<TSecond>(Strategy<TSecond> second)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(second);
            return new ZipStrategy<T, TSecond, (T, TSecond)>(source, second, (a, b) => (a, b));
        }

        /// <summary>Combines two strategies using a result selector.</summary>
        public Strategy<TResult> Zip<TSecond, TResult>(Strategy<TSecond> second, Func<T, TSecond, TResult> resultSelector)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(second);
            ArgumentNullException.ThrowIfNull(resultSelector);
            return new ZipStrategy<T, TSecond, TResult>(source, second, resultSelector);
        }

        /// <summary>Annotates the strategy with a label used in counterexample output.</summary>
        public Strategy<T> WithLabel(string label)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(label);
            return new LabeledStrategy<T>(source, label);
        }

        /// <summary>Projects each generated value through a direct generator that receives both the source value and the data stream. Internal hot-path overload — avoids per-Generate Strategy allocation.</summary>
        internal Strategy<TResult> SelectMany<TResult>(Func<T, ConjectureData, TResult> directGenerator)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(directGenerator);
            return new SelectManyDirectStrategy<T, TResult>(source, directGenerator);
        }
    }

    extension<T>(Strategy<T> source) where T : struct
    {
        /// <summary>Wraps the strategy so it may also produce null, with ~10% null probability.</summary>
        public Strategy<T?> OrNull()
        {
            ArgumentNullException.ThrowIfNull(source);
            return new NullableStrategy<T>(source);
        }
    }
}
