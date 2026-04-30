// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;

using Conjecture.Core.Internal;

namespace Conjecture.Core;

/// <summary>
/// Registration hook for source-generated AOT-safe strategy resolvers.
/// The source generator emits a <c>[ModuleInitializer]</c> that calls
/// <see cref="Register(Func{Type, object?})"/> at startup, replacing the
/// runtime-reflection path in <see cref="SharedParameterStrategyResolver"/>.
/// </summary>
public static class ConjectureStrategyRegistrar
{
    private static Func<Type, ConjectureData, object?>? activeResolver;

    /// <summary>Registers a generated <see cref="IStrategyProvider{T}"/>. Called by source-generated module initializers.</summary>
    /// <param name="strategyFactory">Returns a boxed <see cref="Strategy{T}"/> for the given type, or <see langword="null"/> if unsupported.</param>
    public static void Register(Func<Type, object?> strategyFactory)
    {
        activeResolver = (type, data) =>
        {
            object? strategy = strategyFactory(type);
            return strategy is IGeneratableStrategy gen ? gen.GenerateBoxed(data) : null;
        };
    }

    internal static void Register(Func<Type, ConjectureData, object?> resolver)
    {
        activeResolver = resolver;
    }

    internal static object? TryResolve(Type type, ConjectureData data) =>
        activeResolver?.Invoke(type, data);
}