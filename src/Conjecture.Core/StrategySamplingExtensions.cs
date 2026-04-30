// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core.Internal;

namespace Conjecture.Core;

/// <summary>Extensions for sampling values from <see cref="Strategy{T}"/> outside of a property test.</summary>
public static class StrategySamplingExtensions
{
    extension<T>(Strategy<T> strategy)
    {
        /// <summary>Returns a single value drawn from <paramref name="strategy"/> using a fresh seed.</summary>
        public T Sample()
        {
            ArgumentNullException.ThrowIfNull(strategy);
            return SampleCore(strategy, FreshSeed());
        }

        /// <summary>Returns a list of <paramref name="count"/> values drawn from <paramref name="strategy"/> using a fresh seed.</summary>
        public IReadOnlyList<T> Sample(int count)
        {
            ArgumentNullException.ThrowIfNull(strategy);
            ArgumentOutOfRangeException.ThrowIfNegative(count);
            return StreamCore(strategy, FreshSeed(), count).ToList();
        }

        /// <summary>Returns an unbounded lazy sequence of values drawn from <paramref name="strategy"/> using a fresh seed.</summary>
        public IEnumerable<T> Stream()
        {
            ArgumentNullException.ThrowIfNull(strategy);
            return StreamCore(strategy, FreshSeed(), count: null);
        }

        /// <summary>Returns a lazy sequence of <paramref name="count"/> values drawn from <paramref name="strategy"/> using a fresh seed.</summary>
        public IEnumerable<T> Stream(int count)
        {
            ArgumentNullException.ThrowIfNull(strategy);
            ArgumentOutOfRangeException.ThrowIfNegative(count);
            return StreamCore(strategy, FreshSeed(), count);
        }

        /// <summary>Returns a deterministic <see cref="SeededStrategy{T}"/> view of <paramref name="strategy"/> bound to <paramref name="seed"/>.</summary>
        public SeededStrategy<T> WithSeed(ulong seed) => new(strategy, seed);
    }

    extension<T>(SeededStrategy<T> seeded)
    {
        /// <summary>Returns a single value drawn from the seeded strategy.</summary>
        public T Sample() => SampleCore(seeded.Strategy, seeded.Seed);

        /// <summary>Returns a list of <paramref name="count"/> values drawn from the seeded strategy.</summary>
        public IReadOnlyList<T> Sample(int count)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(count);
            return StreamCore(seeded.Strategy, seeded.Seed, count).ToList();
        }

        /// <summary>Returns an unbounded lazy sequence of values drawn from the seeded strategy.</summary>
        public IEnumerable<T> Stream() => StreamCore(seeded.Strategy, seeded.Seed, count: null);

        /// <summary>Returns a lazy sequence of <paramref name="count"/> values drawn from the seeded strategy.</summary>
        public IEnumerable<T> Stream(int count)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(count);
            return StreamCore(seeded.Strategy, seeded.Seed, count);
        }
    }

    private static T SampleCore<T>(Strategy<T> strategy, ulong seed)
    {
        SplittableRandom rng = new(seed);
        return strategy.Generate(ConjectureData.ForGeneration(rng.Split()));
    }

    private static IEnumerable<T> StreamCore<T>(Strategy<T> strategy, ulong seed, int? count)
    {
        SplittableRandom rng = new(seed);
        if (count is { } c)
        {
            for (int i = 0; i < c; i++)
            {
                yield return strategy.Generate(ConjectureData.ForGeneration(rng.Split()));
            }
        }
        else
        {
            while (true)
            {
                yield return strategy.Generate(ConjectureData.ForGeneration(rng.Split()));
            }
        }
    }

    private static ulong FreshSeed() => (ulong)Random.Shared.NextInt64();
}
