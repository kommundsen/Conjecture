using Conjecture.Core.Generation;

namespace Conjecture.Core;

/// <summary>Factory methods for composing strategies imperatively.</summary>
public static class Strategies
{
    /// <summary>Creates a strategy from an imperative factory function using <see cref="IGeneratorContext"/>.</summary>
    public static Strategy<T> Compose<T>(Func<IGeneratorContext, T> factory) =>
        new ComposeStrategy<T>(factory);
}
