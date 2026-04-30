// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

namespace Conjecture.Core;

/// <summary>A <see cref="Strategy{T}"/> paired with a fixed seed for deterministic sampling.</summary>
/// <typeparam name="T">The type of value produced by the underlying strategy.</typeparam>
/// <param name="Strategy">The underlying strategy.</param>
/// <param name="Seed">The seed used by sampling extensions on this view.</param>
public readonly record struct SeededStrategy<T>(Strategy<T> Strategy, ulong Seed);
