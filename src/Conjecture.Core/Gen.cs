using Conjecture.Core.Strategies;

namespace Conjecture.Core;

/// <summary>Factory methods for creating built-in Conjecture strategies.</summary>
public static class Gen
{
    /// <summary>Returns a strategy that generates random <see cref="bool"/> values.</summary>
    public static Strategy<bool> Booleans() => new BooleanStrategy();
}
