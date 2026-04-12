// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

namespace Conjecture.Core.Internal;

internal sealed class FromBytesStrategy<T>(ReadOnlySpan<byte> buffer, Strategy<T> inner)
    : Strategy<T>("FromBytes")
{
    private readonly byte[] buffer = buffer.ToArray();

    internal override T Generate(ConjectureData data)
    {
        ConjectureData seeded = ConjectureData.FromBuffer(buffer);
        return inner.Generate(seeded);
    }
}