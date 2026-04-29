// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

namespace Conjecture.Core;

/// <summary>Ambient context that flows an <see cref="IGenerationContext"/> to partial constructors via <see cref="AsyncLocal{T}"/>.</summary>
public static class PartialConstructorContext
{
    private static readonly AsyncLocal<IGenerationContext?> Current_ = new();

    /// <summary>Gets the ambient <see cref="IGenerationContext"/> for the current async context.</summary>
    /// <exception cref="InvalidOperationException">Thrown when no scope is active.</exception>
    public static IGenerationContext Current =>
        Current_.Value
            ?? throw new InvalidOperationException(
                "No IGenerationContext is active. Partial constructors may only be called inside a Conjecture test.");

    /// <summary>Sets <paramref name="ctx"/> as the ambient context for the duration of the returned scope.</summary>
    public static IDisposable Use(IGenerationContext ctx)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        IGenerationContext? previous = Current_.Value;
        Current_.Value = ctx;
        return new Scope(previous);
    }

    private sealed class Scope(IGenerationContext? previous) : IDisposable
    {
        public void Dispose()
        {
            Current_.Value = previous;
        }
    }
}
