// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core.Internal;

namespace Conjecture.Core;

internal sealed class ArrayStrategy<T>(Strategy<T> inner, int minSize, int maxSize) : Strategy<T[]>
{
    private readonly ulong ulongMinSize = (ulong)minSize;
    private readonly ulong ulongMaxSize = (ulong)maxSize;

    internal override T[] Generate(ConjectureData data)
    {
        int size = (int)data.NextInteger(ulongMinSize, ulongMaxSize);
        T[] array = new T[size];
        for (int i = 0; i < size; i++)
        {
            array[i] = inner.Generate(data);
        }
        return array;
    }
}
