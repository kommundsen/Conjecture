// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

using Conjecture.Core.Internal;

namespace Conjecture.Core;

/// <summary>
/// AOT-safe registry for <see cref="IStrategyProvider"/> factories populated by the
/// <c>GenerateForGenerator</c> source generator via <c>[ModuleInitializer]</c>.
/// Must be <see langword="public"/> so generated code in user assemblies can register types.
/// </summary>
public static class GenerateForRegistry
{
    private static readonly ConcurrentDictionary<Type, Func<IStrategyProvider>> Providers = new();

    private static readonly ConcurrentDictionary<Type, Func<object, object>> OverrideProviders = new();

    // Parallel dictionary holding AOT-safe boxed strategies for use via ResolveBoxed.
    private static readonly ConcurrentDictionary<Type, Strategy<object?>> BoxedStrategies = new();

    /// <summary>Registers an override-aware factory for <paramref name="type"/>. Called by source-generated module initializers.</summary>
    public static void RegisterOverride(Type type, Func<object, object> factory)
    {
        OverrideProviders[type] = factory;
    }

    /// <summary>Registers a provider factory for <paramref name="type"/>. Called by source-generated module initializers.</summary>
    public static void Register(Type type, Func<IStrategyProvider> factory)
    {
        Providers[type] = factory;
    }

    /// <summary>
    /// Registers a provider factory together with a pre-built boxed strategy for <paramref name="type"/>.
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

    /// <summary>Returns <see langword="true"/> when a provider factory for <paramref name="type"/> has been registered.</summary>
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

    /// <summary>Resolves an override-aware strategy for <typeparamref name="T"/> using the registered factory and the provided <paramref name="cfg"/>. Throws if the type is not registered.</summary>
    public static Strategy<T> ResolveWithOverrides<T>(ForConfiguration<T> cfg)
    {
        Type key = typeof(T);
        return !OverrideProviders.TryGetValue(key, out Func<object, object>? factory)
            ? throw new InvalidOperationException(
                $"No IStrategyProvider<T> is registered for '{key.FullName}'. Decorate the type with [Arbitrary].")
            : (Strategy<T>)factory(cfg);
    }
}