// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

namespace Conjecture.Core;

/// <summary>Convenience extension properties for common strategy filters.</summary>
public static class StrategyExtensionProperties
{
    extension(Strategy<int> s)
    {
        /// <summary>
        /// Filters to positive integers only (x &gt; 0).
        /// </summary>
        /// <remarks>
        /// For tight value ranges, prefer a targeted strategy such as
        /// <c>Generate.Integers(1, 100)</c> over chaining <c>.Where()</c>,
        /// which may exhaust the filter budget on sparse distributions.
        /// </remarks>
        public Strategy<int> Positive => s.Where(static x => x > 0);

        /// <summary>
        /// Filters to negative integers only (x &lt; 0).
        /// </summary>
        /// <remarks>
        /// For tight value ranges, prefer a targeted strategy such as
        /// <c>Generate.Integers(-100, -1)</c> over chaining <c>.Where()</c>,
        /// which may exhaust the filter budget on sparse distributions.
        /// </remarks>
        public Strategy<int> Negative => s.Where(static x => x < 0);

        /// <summary>
        /// Filters out zero (x != 0).
        /// </summary>
        /// <remarks>
        /// For tight value ranges, prefer a targeted strategy such as
        /// <c>Generate.Integers(1, 100)</c> over chaining <c>.Where()</c>,
        /// which may exhaust the filter budget on sparse distributions.
        /// </remarks>
        public Strategy<int> NonZero => s.Where(static x => x is not 0);
    }

    extension(Strategy<string> s)
    {
        /// <summary>
        /// Filters to non-empty strings only (Length &gt; 0).
        /// </summary>
        /// <remarks>
        /// For tight value ranges, prefer a targeted strategy such as
        /// <c>Generate.Strings(minLength: 1)</c> over chaining <c>.Where()</c>,
        /// which may exhaust the filter budget on sparse distributions.
        /// </remarks>
        public Strategy<string> NonEmpty => s.Where(static x => x.Length > 0);
    }

    extension<T>(Strategy<List<T>> s)
    {
        /// <summary>
        /// Filters to non-empty lists only (Count &gt; 0).
        /// </summary>
        /// <remarks>
        /// For tight value ranges, prefer a targeted strategy such as
        /// <c>Generate.Lists(inner, minSize: 1)</c> over chaining <c>.Where()</c>,
        /// which may exhaust the filter budget on sparse distributions.
        /// </remarks>
        public Strategy<List<T>> NonEmpty => s.Where(static x => x.Count > 0);
    }

    extension<T>(Strategy<T> _)
    {
        /// <summary>Combines two strategies into a single strategy that draws from either, chosen uniformly at random.</summary>
        public static Strategy<T> operator |(Strategy<T> left, Strategy<T> right)
            => Generate.OneOf(left, right);
    }
}