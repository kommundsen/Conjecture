using System.Numerics;
using Conjecture.Core.Strategies;

namespace Conjecture.Core;

/// <summary>Factory methods for creating built-in Conjecture strategies.</summary>
public static class Gen
{
    /// <summary>Returns a strategy that generates random <see cref="bool"/> values.</summary>
    public static Strategy<bool> Booleans() => new BooleanStrategy();

    /// <summary>Returns a strategy that generates random <typeparamref name="T"/> values across the full range of the type.</summary>
    public static Strategy<T> Integers<T>() where T : IBinaryInteger<T>, IMinMaxValue<T>
        => new IntegerStrategy<T>(T.MinValue, T.MaxValue);

    /// <summary>Returns a strategy that generates random <typeparamref name="T"/> values in [<paramref name="min"/>, <paramref name="max"/>].</summary>
    public static Strategy<T> Integers<T>(T min, T max) where T : IBinaryInteger<T>
        => new IntegerStrategy<T>(min, max);
}
