// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/


namespace Conjecture.Core;

/// <summary>Provides imperative draw and assume operations within a <c>Generate.Compose</c> factory.</summary>
public interface IGeneratorContext
{
    /// <summary>Generates a value from <paramref name="strategy"/>.</summary>
    T Generate<T>(Strategy<T> strategy);

    /// <summary>Rejects the current test case if <paramref name="condition"/> is false.</summary>
    void Assume(bool condition);

    /// <summary>Records a numeric observation to guide targeted generation.</summary>
    /// <param name="observation">The value to maximize. Must be finite (not NaN or Infinity).</param>
    /// <param name="label">Identifies this metric. Multiple labels are optimized independently.</param>
    void Target(double observation, string label = "default");
}