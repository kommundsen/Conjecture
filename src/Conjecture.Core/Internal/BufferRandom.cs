// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Runtime.InteropServices;

namespace Conjecture.Core.Internal;

internal sealed class BufferRandom : IRandom
{
    private readonly byte[] buffer;
    private int position;

    internal BufferRandom(byte[] buffer) => this.buffer = buffer;

    public ulong NextUInt64()
    {
        Span<byte> scratch = stackalloc byte[8];
        NextBytes(scratch);
        return MemoryMarshal.Read<ulong>(scratch);
    }

    public void NextBytes(Span<byte> output)
    {
        int available = buffer.Length - position;
        int fromBuffer = Math.Min(available, output.Length);
        if (fromBuffer > 0)
        {
            buffer.AsSpan(position, fromBuffer).CopyTo(output);
            position += fromBuffer;
        }

        output[fromBuffer..].Clear();
    }

    public IRandom Split() => new BufferRandom(buffer);
}