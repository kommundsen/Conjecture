// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Runtime.InteropServices;

namespace Conjecture.Core.Internal;

internal interface IRandom
{
    ulong NextUInt64();
    void NextBytes(Span<byte> buffer);
    IRandom Split();
}

internal sealed class SplittableRandom : IRandom
{
    private ulong state;

    internal SplittableRandom(ulong seed) => state = seed;

    public ulong NextUInt64()
    {
        state += 0x9e3779b97f4a7c15UL;
        var z = state;
        z = (z ^ (z >> 30)) * 0xbf58476d1ce4e5b9UL;
        z = (z ^ (z >> 27)) * 0x94d049bb133111ebUL;
        return z ^ (z >> 31);
    }

    public void NextBytes(Span<byte> buffer)
    {
        var words = MemoryMarshal.Cast<byte, ulong>(buffer);
        for (var i = 0; i < words.Length; i++)
        {
            words[i] = NextUInt64();
        }

        var remainder = buffer.Length % 8;
        if (remainder > 0)
        {
            Span<byte> tail = stackalloc byte[8];
            MemoryMarshal.Write(tail, NextUInt64());
            tail[..remainder].CopyTo(buffer[^remainder..]);
        }
    }

    public IRandom Split() => new SplittableRandom(NextUInt64());
}