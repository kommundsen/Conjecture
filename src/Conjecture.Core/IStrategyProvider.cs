// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/


namespace Conjecture.Core;

/// <summary>Non-generic marker interface used as a generic constraint on <see cref="FromAttribute{TProvider}"/>.</summary>
public interface IStrategyProvider { }

/// <summary>Provides a <see cref="Strategy{T}"/> instance for use in property-based tests.</summary>
/// <typeparam name="T">The type of value the strategy generates.</typeparam>
public interface IStrategyProvider<T> : IStrategyProvider
{
    /// <summary>Creates and returns a strategy for generating <typeparamref name="T"/> values.</summary>
    Strategy<T> Create();
}