// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

// Derived from the Python Hypothesis library.
// Original copyright: Copyright (c) 2013-present, David R. MacIver and contributors.

using Conjecture.Core.Internal;

namespace Conjecture.Core;

internal sealed class ListStrategy<T>(Strategy<T> inner, int minSize, int maxSize) : Strategy<List<T>>
{
    private readonly ulong ulongMinSize = (ulong)minSize;
    private readonly ulong ulongMaxSize = (ulong)maxSize;

    internal override List<T> Generate(ConjectureData data)
    {
        var size = (int)data.NextInteger(ulongMinSize, ulongMaxSize);
        var list = new List<T>(size);
        for (var i = 0; i < size; i++)
        {
            list.Add(inner.Generate(data));
        }
        return list;
    }
}