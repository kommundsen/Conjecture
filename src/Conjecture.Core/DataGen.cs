// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core.Internal;

namespace Conjecture.Core;

/// <summary>Standalone data generation utilities for sampling strategy values outside of a property test.</summary>
public static class DataGen
{
    /// <summary>Returns a list of <paramref name="count"/> values drawn from <paramref name="strategy"/>.</summary>
    public static IReadOnlyList<T> Sample<T>(Strategy<T> strategy, int count, ulong? seed = null)
        => Stream(strategy, count, seed).ToList();

    /// <summary>Returns a single value drawn from <paramref name="strategy"/>.</summary>
    public static T SampleOne<T>(Strategy<T> strategy, ulong? seed = null)
        => Stream(strategy, 1, seed).First();

    /// <summary>Returns a lazy sequence of <paramref name="count"/> values drawn from <paramref name="strategy"/>.</summary>
    public static IEnumerable<T> Stream<T>(Strategy<T> strategy, int count, ulong? seed = null)
    {
        var rng = new SplittableRandom(MakeSeed(seed));
        for (var i = 0; i < count; i++)
        {
            yield return strategy.Generate(ConjectureData.ForGeneration(rng.Split()));
        }
    }

    private static ulong MakeSeed(ulong? seed) => seed ?? (ulong)Random.Shared.NextInt64();
}