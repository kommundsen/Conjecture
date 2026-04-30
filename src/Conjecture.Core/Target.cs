// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

// Derived from the Python Hypothesis library.
// Original copyright: Copyright (c) 2013-present, David R. MacIver and contributors.

using Conjecture.Core.Internal;

namespace Conjecture.Core;

/// <summary>Provides targeting helpers to guide property test generation toward interesting input regions.</summary>
public static class Target
{
    internal static readonly AsyncLocal<ConjectureData?> CurrentData = new();

    /// <summary>Records a numeric observation to maximize during the targeting phase.</summary>
    /// <param name="observation">The value to maximize. Must be finite (not NaN or Infinity).</param>
    /// <param name="label">Identifies this observation label. Multiple labels are targeted independently.</param>
    public static void Maximize(double observation, string label = "default")
    {
        ConjectureData data = CurrentData.Value
            ?? throw new InvalidOperationException("Target.Maximize can only be called inside a property test body.");
        data.RecordObservation(label, observation);
    }

    /// <summary>Records a numeric observation to minimize during the targeting phase.</summary>
    /// <param name="observation">The value to minimize. Must be finite (not NaN or Infinity).</param>
    /// <param name="label">Identifies this observation label. Multiple labels are targeted independently.</param>
    public static void Minimize(double observation, string label = "default")
    {
        Maximize(-observation, label);
    }
}