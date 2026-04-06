// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

// Derived from the Python Hypothesis library.
// Original copyright: Copyright (c) 2013-present, David R. MacIver and contributors.

using Conjecture.Core.Internal;

namespace Conjecture.Core;

internal sealed class SampledFromStrategy<T>(IReadOnlyList<T> values) : Strategy<T>
{
    private readonly ulong lastIndex = values.Count > 0
        ? (ulong)(values.Count - 1)
        : throw new ArgumentException("At least one value is required.", nameof(values));

    internal override T Generate(ConjectureData data)
    {
        var index = (int)data.NextInteger(0, lastIndex);
        return values[index];
    }
}