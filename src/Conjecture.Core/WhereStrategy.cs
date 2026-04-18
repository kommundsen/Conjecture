// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

// Derived from the Python Hypothesis library.
// Original copyright: Copyright (c) 2013-present, David R. MacIver and contributors.

using Conjecture.Core.Internal;

namespace Conjecture.Core;

internal sealed class WhereStrategy<T>(Strategy<T> source, Func<T, bool> predicate) : Strategy<T>
{
    private const int MaxAttempts = 200;

    internal override T Generate(ConjectureData data)
    {
        for (int i = 0; i < MaxAttempts; i++)
        {
            int snapshot = data.NodeCount;
            T value = source.Generate(data);
            if (predicate(value))
            {
                return value;
            }
            data.TruncateNodes(snapshot);
        }
        data.MarkInvalid();
        throw new UnsatisfiedAssumptionException();
    }
}