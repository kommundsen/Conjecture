// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

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