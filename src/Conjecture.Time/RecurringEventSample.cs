// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

namespace Conjecture.Time;

/// <summary>A sample produced by <see cref="TimeStrategyExtensions.RecurringEvents"/>.</summary>
/// <param name="WindowStart">The inclusive start of the generated window.</param>
/// <param name="WindowEnd">The inclusive end of the generated window.</param>
/// <param name="Occurrences">All occurrences that fall within the window.</param>
/// <param name="Zone">The time zone associated with this sample.</param>
/// <param name="NextOccurrence">
/// The recurrence delegate, stored so that extensions like <c>NearDstTransition()</c> can reposition
/// the window and regenerate <see cref="Occurrences"/> without losing correctness.
/// </param>
public sealed record RecurringEventSample(
    DateTimeOffset WindowStart,
    DateTimeOffset WindowEnd,
    IReadOnlyList<DateTimeOffset> Occurrences,
    TimeZoneInfo Zone,
    Func<DateTimeOffset, DateTimeOffset?> NextOccurrence);