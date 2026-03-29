namespace Conjecture.Core;

/// <summary>
/// Specifies that a property-based test parameter should be generated using
/// <typeparamref name="TProvider"/> instead of the default type-inferred strategy.
/// </summary>
/// <typeparam name="TProvider">
/// An <see cref="IStrategyProvider"/> implementation with a public parameterless constructor.
/// </typeparam>
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class FromAttribute<TProvider> : Attribute
    where TProvider : IStrategyProvider, new()
{
    /// <summary>The provider type used to create the strategy for this parameter.</summary>
    public Type ProviderType => typeof(TProvider);
}
