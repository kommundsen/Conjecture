// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

using Conjecture.Core.Internal;

namespace Conjecture.Core;

/// <summary>
/// AOT-safe registry for <see cref="IStrategyProvider"/> factories populated by the
/// <c>GenerateForGenerator</c> source generator via <c>[ModuleInitializer]</c>.
/// Must be <see langword="public"/> so generated code in user assemblies can register types.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class GenerateForRegistry
{
    private static readonly ConcurrentDictionary<Type, Func<IStrategyProvider>> Providers = InitProviders();

    private static readonly ConcurrentDictionary<Type, Func<object, object>> OverrideProviders = new();

    // Parallel dictionary holding AOT-safe boxed strategies for use via ResolveBoxed.
    private static readonly ConcurrentDictionary<Type, Strategy<object?>> BoxedStrategies = new();

    /// <summary>
    /// Registers built-in primitive types whose factory has no required arguments so
    /// <see cref="Strategy.For{T}()"/> works out of the box for those types without requiring
    /// <c>[Arbitrary]</c> decoration or source-generator output.
    /// Only types with fully-defaulted factory signatures (e.g. <see cref="Version"/>) belong here;
    /// types like <see cref="Guid"/> that have no strategy overload are intentionally excluded.
    /// </summary>
    private static ConcurrentDictionary<Type, Func<IStrategyProvider>> InitProviders()
    {
        ConcurrentDictionary<Type, Func<IStrategyProvider>> dict = new();
        dict[typeof(Version)] = static () => new VersionStrategyProvider();
        dict[typeof(Half)] = static () => new HalfStrategyProvider();
        dict[typeof(Rune)] = static () => new RuneStrategyProvider();
        return dict;
    }

    static GenerateForRegistry()
    {
        IPAddressStrategyRegistration.Register();
        IPEndPointStrategyRegistration.Register();
        UriStrategyRegistration.Register();
        MailAddressStrategyRegistration.Register();
    }

    private sealed class VersionStrategyProvider : IStrategyProvider<Version>
    {
        public Strategy<Version> Create() => Strategy.Versions();
    }

    private sealed class HalfStrategyProvider : IStrategyProvider<Half>
    {
        public Strategy<Half> Create() => Strategy.Halves();
    }

    private sealed class RuneStrategyProvider : IStrategyProvider<Rune>
    {
        public Strategy<Rune> Create() => Strategy.Runes();
    }

    /// <summary>Registers an override-aware <see cref="IStrategyProvider"/> for <paramref name="type"/>. Called by source-generated module initializers.</summary>
    public static void RegisterOverride(Type type, Func<object, object> factory)
    {
        OverrideProviders[type] = factory;
    }

    /// <summary>Registers an <see cref="IStrategyProvider"/> for <paramref name="type"/>. Called by source-generated module initializers.</summary>
    public static void Register(Type type, Func<IStrategyProvider> factory)
    {
        Providers[type] = factory;
    }

    /// <summary>
    /// Registers an <see cref="IStrategyProvider"/> together with a pre-built boxed strategy for <paramref name="type"/>.
    /// Enables <see cref="ResolveBoxed"/> for callers that only know the type at runtime.
    /// </summary>
    public static void Register(Type type, Func<IStrategyProvider> factory, Strategy<object?> boxedStrategy)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(boxedStrategy);
        Providers[type] = factory;
        BoxedStrategies[type] = boxedStrategy;
    }

    /// <summary>Returns <see langword="true"/> when an <see cref="IStrategyProvider"/> for <paramref name="type"/> has been registered.</summary>
    public static bool IsRegistered(Type type) => Providers.ContainsKey(type);

    /// <summary>
    /// Returns the boxed strategy registered for <paramref name="type"/>.
    /// Throws <see cref="InvalidOperationException"/> if the type is not registered or has no boxed strategy.
    /// </summary>
    public static Strategy<object?> ResolveBoxed(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        return BoxedStrategies.TryGetValue(type, out Strategy<object?>? strategy)
            ? strategy
            : throw new InvalidOperationException(
                $"No boxed strategy is registered for '{type.FullName}'. " +
                $"Decorate the type with [Arbitrary] and use the Conjecture.Generators source generator, " +
                $"or register it manually via GenerateForRegistry.Register(type, factory, boxedStrategy).");
    }

    internal static Strategy<T> Resolve<T>()
    {
        return !Providers.TryGetValue(typeof(T), out Func<IStrategyProvider>? factory)
            ? throw new InvalidOperationException(
                $"No IStrategyProvider<T> is registered for '{typeof(T).FullName}'. Decorate the type with [Arbitrary].")
            : ((IStrategyProvider<T>)factory()).Create();
    }

    /// <summary>Resolves an override-aware strategy for <typeparamref name="T"/> using the registered <see cref="IStrategyProvider"/> and the provided <paramref name="cfg"/>. Throws if the type is not registered.</summary>
    public static Strategy<T> ResolveWithOverrides<T>(ForConfiguration<T> cfg)
    {
        Type key = typeof(T);
        return !OverrideProviders.TryGetValue(key, out Func<object, object>? factory)
            ? throw new InvalidOperationException(
                $"No IStrategyProvider<T> is registered for '{key.FullName}'. Decorate the type with [Arbitrary].")
            : (Strategy<T>)factory(cfg);
    }
}