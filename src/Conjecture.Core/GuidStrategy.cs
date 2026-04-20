// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core.Internal;

namespace Conjecture.Core;

internal sealed class GuidStrategy : Strategy<Guid>
{
    internal override Guid Generate(ConjectureData data)
    {
        ulong hi = data.NextInteger(0UL, ulong.MaxValue);
        ulong lo = data.NextInteger(0UL, ulong.MaxValue);
        Span<byte> bytes = stackalloc byte[16];
        BitConverter.TryWriteBytes(bytes, hi);
        BitConverter.TryWriteBytes(bytes[8..], lo);
        return new Guid(bytes);
    }
}
