using Conjecture.Core.Internal;

namespace Conjecture.Core;

/// <summary>Base class for all Conjecture strategies that generate values of type <typeparamref name="T"/>.</summary>
/// <typeparam name="T">The type of value produced by this strategy.</typeparam>
/// <param name="label">Optional label for counterexample output.</param>
public abstract class Strategy<T>(string? label = null)
{
    /// <summary>Label used in counterexample output, or null if unlabeled.</summary>
    public string? Label { get; } = label;

    internal abstract T Generate(ConjectureData data);
}
