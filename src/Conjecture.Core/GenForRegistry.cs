// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Conjecture.Core;

/// <summary>
/// AOT-safe registry for <see cref="IStrategyProvider"/> factories populated by the
/// <c>GenForGenerator</c> source generator via <c>[ModuleInitializer]</c>.
/// Must be <see langword="public"/> so generated code in user assemblies can call <see cref="Register"/>.
/// </summary>
public static class GenForRegistry
{
    private static readonly ConcurrentDictionary<Type, Func<IStrategyProvider>> Providers = new();

    /// <summary>Registers a provider factory for <paramref name="type"/>. Called by source-generated module initializers.</summary>
    public static void Register(Type type, Func<IStrategyProvider> factory)
    {
        Providers[type] = factory;
    }

    internal static Strategy<T> Resolve<T>()
    {
        if (!Providers.TryGetValue(typeof(T), out Func<IStrategyProvider>? factory))
        {
            throw new InvalidOperationException(
                $"No IStrategyProvider<T> is registered for '{typeof(T).FullName}'. Decorate the type with [Arbitrary].");
        }

        return ((IStrategyProvider<T>)factory()).Create();
    }
}
