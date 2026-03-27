using Conjecture.Core.Generation;

namespace Conjecture.Core.Formatting;

/// <summary>Formats a counterexample value of type <typeparamref name="T"/> for display in failure messages.</summary>
/// <typeparam name="T">The value type this formatter handles.</typeparam>
public interface IStrategyFormatter<T>
{
    /// <summary>Returns a human-readable string representation of <paramref name="value"/>.</summary>
    string Format(T value);
}
