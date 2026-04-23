// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core;

namespace Conjecture.Time;

/// <summary>Extension methods on <see cref="Strategy{T}"/> for <see cref="DateTime"/> test value generation.</summary>
public static class DateTimeExtensions
{
    extension(Strategy<DateTime> s)
    {
        /// <summary>
        /// Returns a strategy that pairs each generated <see cref="DateTime"/> with a randomly chosen <see cref="DateTimeKind"/>,
        /// covering all three kinds uniformly. Shrinks toward <see cref="DateTimeKind.Utc"/> (index 0 in the underlying OneOf call).
        /// </summary>
        public Strategy<(DateTime Value, DateTimeKind Kind)> WithKinds()
        {
            return Generate.OneOf(
                s.Select(static dt => (DateTime.SpecifyKind(dt, DateTimeKind.Utc), DateTimeKind.Utc)),
                s.Select(static dt => (DateTime.SpecifyKind(dt, DateTimeKind.Local), DateTimeKind.Local)),
                s.Select(static dt => (DateTime.SpecifyKind(dt, DateTimeKind.Unspecified), DateTimeKind.Unspecified)));
        }
    }
}