using Conjecture.Core.Generation;

namespace Conjecture.Core;

/// <summary>Provides a <see cref="Strategy{T}"/> instance for use in property-based tests.</summary>
/// <typeparam name="T">The type of value the strategy generates.</typeparam>
public interface IStrategyProvider<T>
{
    /// <summary>Creates and returns a strategy for generating <typeparamref name="T"/> values.</summary>
    Strategy<T> Create();
}
