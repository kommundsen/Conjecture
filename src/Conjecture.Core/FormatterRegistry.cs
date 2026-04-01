// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Concurrent;

namespace Conjecture.Core;

/// <summary>Global registry mapping types to their <see cref="IStrategyFormatter{T}"/> instances.</summary>
public static class FormatterRegistry
{
    private static readonly ConcurrentDictionary<Type, Func<object, string>> UntypedFormatters = new();

    static FormatterRegistry()
    {
        Register(BuiltInFormatters.Int32);
        Register(BuiltInFormatters.Boolean);
        Register(BuiltInFormatters.Double);
        Register(BuiltInFormatters.Single);
        Register(BuiltInFormatters.String);
        Register(BuiltInFormatters.ByteArray);
        Register(new BuiltInFormatters.ListFormatter<int>());
        Register(new BuiltInFormatters.ListFormatter<object>());
        Register(new BuiltInFormatters.HashSetFormatter<string>());
        Register(new BuiltInFormatters.DictionaryFormatter<int, string>());
        Register(new BuiltInFormatters.TupleFormatter<int, string>());
    }

    /// <summary>Registers <paramref name="formatter"/> for type <typeparamref name="T"/>, replacing any existing registration.</summary>
    public static void Register<T>(IStrategyFormatter<T>? formatter)
    {
        Holder<T>.Instance = formatter;
        if (formatter is not null)
        {
            UntypedFormatters[typeof(T)] = obj => formatter.Format((T)obj!);
        }
        else
        {
            UntypedFormatters.TryRemove(typeof(T), out _);
        }
    }

    /// <summary>Returns the registered formatter for <typeparamref name="T"/>, or <see langword="null"/> if none is registered.</summary>
    public static IStrategyFormatter<T>? Get<T>() =>
        Holder<T>.Instance;

    internal static Func<object, string>? GetUntyped(Type type) =>
        UntypedFormatters.TryGetValue(type, out var f) ? f : null;

    private static class Holder<T>
    {
        public static volatile IStrategyFormatter<T>? Instance;
    }
}