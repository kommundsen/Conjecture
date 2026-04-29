// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core;

namespace Conjecture.Time;

/// <summary>Extension methods on <see cref="Strategy{T}"/> for <see cref="RecurringEventSample"/>.</summary>
public static class RecurringEventExtensions
{
    extension(Strategy<RecurringEventSample> s)
    {
        /// <summary>
        /// Returns a strategy biased toward windows that overlap a DST transition in <see cref="RecurringEventSample.Zone"/>.
        /// Places the window start just before a randomly chosen transition and regenerates
        /// <see cref="RecurringEventSample.Occurrences"/> for the new window.
        /// Falls back to the base strategy when no transitions are available.
        /// </summary>
        public Strategy<RecurringEventSample> NearDstTransition()
        {
            return Strategy.Compose<RecurringEventSample>(ctx =>
            {
                RecurringEventSample sample = ctx.Generate(s);
                List<DateTimeOffset> transitions = DstTransitionHelper.GetTransitionsUtc(sample.Zone, yearMinus: 2, yearPlus: 2);

                if (transitions.Count == 0)
                {
                    return sample;
                }

                int index = ctx.Generate(Strategy.Integers<int>(0, transitions.Count - 1));
                DateTimeOffset transition = transitions[index];
                TimeSpan windowDuration = sample.WindowEnd - sample.WindowStart;

                // Jitter so the transition lands somewhere inside the window.
                long jitterTicks = ctx.Generate(Strategy.Integers<long>(-windowDuration.Ticks, 0));
                DateTimeOffset newWindowStart = transition.AddTicks(jitterTicks);
                DateTimeOffset newWindowEnd = newWindowStart + windowDuration;

                // Re-walk nextOccurrence so Occurrences reflects the new window.
                List<DateTimeOffset> occurrences = [];
                DateTimeOffset? current = sample.NextOccurrence(newWindowStart);
                int steps = 0;
                while (current is not null && current.Value <= newWindowEnd)
                {
                    if (++steps > 10_000)
                    {
                        throw new InvalidOperationException(
                            "nextOccurrence did not advance past the window after 10 000 steps. " +
                            "Ensure the delegate always returns a value strictly after its input.");
                    }

                    if (current.Value >= newWindowStart)
                    {
                        occurrences.Add(current.Value);
                    }

                    current = sample.NextOccurrence(current.Value);
                }

                return sample with
                {
                    WindowStart = newWindowStart,
                    WindowEnd = newWindowEnd,
                    Occurrences = occurrences.AsReadOnly(),
                };
            });
        }
    }
}