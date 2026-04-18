// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

// Derived from the Python Hypothesis library.
// Original copyright: Copyright (c) 2013-present, David R. MacIver and contributors.

using Conjecture.Core.Internal;

namespace Conjecture.Core;

internal sealed class RecursiveStrategy<T> : Strategy<T>
{
    private readonly Strategy<T>[] levels;

    internal RecursiveStrategy(Strategy<T> baseCase, Func<Strategy<T>, Strategy<T>> recursive, int maxDepth)
    {
        if (maxDepth < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxDepth), "maxDepth must be >= 0.");
        }

        this.levels = new Strategy<T>[maxDepth + 1];
        this.levels[0] = baseCase;
        for (int i = 1; i <= maxDepth; i++)
        {
            this.levels[i] = recursive(this.levels[i - 1]);
        }
    }

    internal override T Generate(ConjectureData data)
    {
        int depth = (int)data.NextInteger(0, (ulong)(this.levels.Length - 1));
        return this.levels[depth].Generate(data);
    }
}