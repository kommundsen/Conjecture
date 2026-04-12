// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core;

using Microsoft.Extensions.Time.Testing;

namespace Conjecture.Time;

/// <summary>
/// Auto-injected <see cref="IStrategyProvider{T}"/> for <see cref="TimeProvider"/>.
/// Each generated value is a fresh <see cref="FakeTimeProvider"/> with a fixed epoch start,
/// so tests that replay a seed receive an identical, independently-advanceable clock.
/// </summary>
[Arbitrary]
public sealed class TimeProviderArbitrary : IStrategyProvider<TimeProvider>
{
    /// <summary>Returns a strategy that generates fresh <see cref="FakeTimeProvider"/> instances.</summary>
    public static Strategy<TimeProvider> Create()
    {
        return Generate.Compose<TimeProvider>(static _ => new FakeTimeProvider());
    }

    Strategy<TimeProvider> IStrategyProvider<TimeProvider>.Create()
    {
        return Create();
    }
}