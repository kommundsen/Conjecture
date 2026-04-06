// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

// Derived from the Python Hypothesis library.
// Original copyright: Copyright (c) 2013-present, David R. MacIver and contributors.

namespace Conjecture.Core;

/// <summary>Provides assumption helpers for filtering property test inputs.</summary>
public static class Assume
{
    /// <summary>Skips the current example if <paramref name="condition"/> is <see langword="false"/>.</summary>
    public static void That(bool condition)
    {
        if (!condition)
        {
            throw new UnsatisfiedAssumptionException();
        }
    }
}