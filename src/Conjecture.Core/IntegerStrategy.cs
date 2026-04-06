// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

// Derived from the Python Hypothesis library.
// Original copyright: Copyright (c) 2013-present, David R. MacIver and contributors.

using System.Numerics;
using Conjecture.Core.Internal;

namespace Conjecture.Core;

internal sealed class IntegerStrategy<T>(T min, T max) : Strategy<T> where T : IBinaryInteger<T>
{
    internal override T Generate(ConjectureData data)
    {
        var rangeMinus1 = ulong.CreateTruncating(max) - ulong.CreateTruncating(min);
        var raw = data.NextInteger(0UL, rangeMinus1);
        return min + T.CreateTruncating(raw);
    }
}