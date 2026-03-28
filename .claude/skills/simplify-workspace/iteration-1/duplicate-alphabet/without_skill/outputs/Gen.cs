using System.Linq;
using System.Numerics;
using Conjecture.Core.Generation;

namespace Conjecture.Core;

/// <summary>Factory methods for creating built-in Hypothesis strategies.</summary>
public static class Gen
{
    internal static readonly char[] PrintableAscii =
        Enumerable.Range(32, 95).Select(i => (char)i).ToArray();
    /// <summary>Returns a strategy that generates random <see cref="bool"/> values.</summary>
    public static Strategy<bool> Booleans() => new BooleanStrategy();

    /// <summary>Returns a strategy that generates random <typeparamref name="T"/> values across the full range of the type.</summary>
    public static Strategy<T> Integers<T>() where T : IBinaryInteger<T>, IMinMaxValue<T>
        => new IntegerStrategy<T>(T.MinValue, T.MaxValue);

    /// <summary>Returns a strategy that generates random <typeparamref name="T"/> values in [<paramref name="min"/>, <paramref name="max"/>].</summary>
    public static Strategy<T> Integers<T>(T min, T max) where T : IBinaryInteger<T>
        => new IntegerStrategy<T>(min, max);

    /// <summary>Returns a strategy that generates random byte arrays of length <paramref name="size"/>.</summary>
    public static Strategy<byte[]> Bytes(int size) => new BytesStrategy(size);

    /// <summary>Returns a strategy that always produces <paramref name="value"/>.</summary>
    public static Strategy<T> Just<T>(T value) => new JustStrategy<T>(value);

    /// <summary>Returns a strategy that picks uniformly among <paramref name="strategies"/>.</summary>
    public static Strategy<T> OneOf<T>(params Strategy<T>[] strategies) => new OneOfStrategy<T>(strategies);

    /// <summary>Returns a strategy that picks uniformly from <paramref name="values"/>.</summary>
    public static Strategy<T> SampledFrom<T>(IReadOnlyList<T> values) => new SampledFromStrategy<T>(values);

    /// <summary>Returns a strategy that generates random <typeparamref name="T"/> enum values.</summary>
    public static Strategy<T> Enums<T>() where T : struct, Enum => SampledFrom(Enum.GetValues<T>());

    /// <summary>Returns a strategy that generates random <see cref="double"/> values across the full range.</summary>
    public static Strategy<double> Doubles() => new FloatingPointStrategy<double>();

    /// <summary>Returns a strategy that generates random <see cref="double"/> values in [<paramref name="min"/>, <paramref name="max"/>].</summary>
    public static Strategy<double> Doubles(double min, double max) => new FloatingPointStrategy<double>(min, max);

    /// <summary>Returns a strategy that generates random <see cref="float"/> values across the full range.</summary>
    public static Strategy<float> Floats() => new FloatingPointStrategy<float>();

    /// <summary>Returns a strategy that generates random <see cref="float"/> values in [<paramref name="min"/>, <paramref name="max"/>].</summary>
    public static Strategy<float> Floats(float min, float max) => new FloatingPointStrategy<float>(min, max);

    /// <summary>Returns a strategy that generates random printable ASCII characters.</summary>
    public static Strategy<char> Chars()
        => SampledFrom(PrintableAscii);

    /// <summary>Returns a strategy that generates characters from <paramref name="alphabet"/>.</summary>
    public static Strategy<char> Chars(char[] alphabet) => SampledFrom(alphabet);

    /// <summary>Returns a strategy that generates random strings.</summary>
    public static Strategy<string> Strings(char[]? alphabet = null, int minLength = 0, int maxLength = 20)
        => new StringStrategy(alphabet, minLength, maxLength);
}
