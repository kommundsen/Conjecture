// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

// Derived from the Python Hypothesis library.
// Original copyright: Copyright (c) 2013-present, David R. MacIver and contributors.

using Conjecture.Core.Internal;

namespace Conjecture.Core;

internal sealed class RecursiveStrategy<T>(Strategy<T> baseCase, Func<Strategy<T>, Strategy<T>> recursive, int maxDepth) : Strategy<T>
{
    private readonly int maxDepth = maxDepth >= 0
        ? maxDepth
        : throw new ArgumentOutOfRangeException(nameof(maxDepth), "maxDepth must be >= 0.");

    internal override T Generate(ConjectureData data)
    {
        int depth = (int)data.NextInteger(0, (ulong)maxDepth);
        return new DepthLimitedStrategy<T>(baseCase, recursive, depth).Generate(data);
    }
}

internal sealed class DepthLimitedStrategy<T>(Strategy<T> baseCase, Func<Strategy<T>, Strategy<T>> recursive, int remainingDepth) : Strategy<T>
{
    internal override T Generate(ConjectureData data)
    {
        if (remainingDepth == 0)
        {
            return baseCase.Generate(data);
        }

        Strategy<T> next = new DepthLimitedStrategy<T>(baseCase, recursive, remainingDepth - 1);
        Strategy<T> expanded = recursive(next);
        return expanded.Generate(data);
    }
}
