// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

// Derived from the Python Hypothesis library.
// Original copyright: Copyright (c) 2013-present, David R. MacIver and contributors.

using Conjecture.Core.Internal;

namespace Conjecture.Core;

/// <summary>Base class for all Conjecture strategies that generate values of type <typeparamref name="T"/>.</summary>
/// <typeparam name="T">The type of value produced by this strategy.</typeparam>
/// <param name="label">Optional label for counterexample output.</param>
public abstract class Strategy<T>(string? label = null) : IGeneratableStrategy
{
    /// <summary>Label used in counterexample output, or null if unlabeled.</summary>
    public string? Label { get; } = label;

    internal abstract T Generate(ConjectureData data);

    object? IGeneratableStrategy.GenerateBoxed(ConjectureData data) => Generate(data);
}