// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

// Derived from the Python Hypothesis library.
// Original copyright: Copyright (c) 2013-present, David R. MacIver and contributors.


namespace Conjecture.Core;

/// <summary>Provides imperative generation and assumption operations within a <c>Strategy.Compose</c> body.</summary>
public interface IGenerationContext
{
    /// <summary>Generates a value from <paramref name="strategy"/>.</summary>
    T Generate<T>(Strategy<T> strategy);

    /// <summary>Rejects the current example if <paramref name="condition"/> is false.</summary>
    void Assume(bool condition);

    /// <summary>Records a numeric observation to guide targeted generation.</summary>
    /// <param name="observation">The value to maximize. Must be finite (not NaN or Infinity).</param>
    /// <param name="label">Identifies this observation label. Multiple labels are targeted independently.</param>
    void Target(double observation, string label = "default");
}