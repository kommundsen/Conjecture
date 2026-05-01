// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Globalization;
using System.Text;

using Conjecture.Abstractions.Strategies;

namespace Conjecture.Core;

/// <summary>Built-in <see cref="IStrategyFormatter{T}"/> instances for common primitive types.</summary>
public static class BuiltInFormatters
{
    /// <summary>Formatter for <see cref="int"/> values.</summary>
    public static IStrategyFormatter<int> Int32 { get; } = new Int32Formatter();
    /// <summary>Formatter for <see cref="bool"/> values.</summary>
    public static IStrategyFormatter<bool> Boolean { get; } = new BooleanFormatter();
    /// <summary>Formatter for <see cref="double"/> values.</summary>
    public static IStrategyFormatter<double> Double { get; } = new DoubleFormatter();
    /// <summary>Formatter for <see cref="float"/> values.</summary>
    public static IStrategyFormatter<float> Single { get; } = new SingleFormatter();
    /// <summary>Formatter for <see cref="string"/> values.</summary>
    public static IStrategyFormatter<string> String { get; } = new StringFormatter();
    /// <summary>Formatter for <see cref="byte"/> array values.</summary>
    public static IStrategyFormatter<byte[]> ByteArray { get; } = new ByteArrayFormatter();

    private sealed class Int32Formatter : IStrategyFormatter<int>
    {
        public string Format(int value) => value.ToString();
    }

    private sealed class BooleanFormatter : IStrategyFormatter<bool>
    {
        public string Format(bool value) => value ? "true" : "false";
    }

    private sealed class DoubleFormatter : IStrategyFormatter<double>
    {
        public string Format(double value) => value.ToString("G", CultureInfo.InvariantCulture);
    }

    private sealed class SingleFormatter : IStrategyFormatter<float>
    {
        public string Format(float value) => value.ToString("G", CultureInfo.InvariantCulture) + "f";
    }

    private sealed class StringFormatter : IStrategyFormatter<string>
    {
        public string Format(string value)
        {
            var sb = new StringBuilder("\"");
            foreach (var c in value)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default: sb.Append(c); break;
                }
            }
            sb.Append('"');
            return sb.ToString();
        }
    }

    private sealed class ByteArrayFormatter : IStrategyFormatter<byte[]>
    {
        public string Format(byte[] value) =>
            $"new byte[] {{ {string.Join(", ", value.Select(b => $"0x{b:X2}"))} }}";
    }

    private static string FormatElement<T>(T element) =>
        FormatterRegistry.Get<T>()?.Format(element!) ?? element?.ToString() ?? "null";

    private static string FormatSequence<T>(IEnumerable<T> items, char open, char close) =>
        $"{open}{string.Join(", ", items.Select(FormatElement))}{close}";

    internal sealed class ListFormatter<T> : IStrategyFormatter<List<T>>
    {
        public string Format(List<T> value) => FormatSequence(value, '[', ']');
    }

    internal sealed class HashSetFormatter<T> : IStrategyFormatter<HashSet<T>>
    {
        public string Format(HashSet<T> value) => FormatSequence(value, '{', '}');
    }

    internal sealed class DictionaryFormatter<TKey, TValue> : IStrategyFormatter<Dictionary<TKey, TValue>>
        where TKey : notnull
    {
        public string Format(Dictionary<TKey, TValue> value) =>
            $"{{{string.Join(", ", value.Select(kv => $"{FormatElement(kv.Key)}: {FormatElement(kv.Value)}"))}}}";
    }

    internal sealed class TupleFormatter<T1, T2> : IStrategyFormatter<(T1, T2)>
    {
        public string Format((T1, T2) value) =>
            $"({FormatElement(value.Item1)}, {FormatElement(value.Item2)})";
    }
}