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
    private ulong _state;

    internal SplittableRandom(ulong seed) => _state = seed;

    public ulong NextUInt64()
    {
        _state += 0x9e3779b97f4a7c15UL;
        var z = _state;
        z = (z ^ (z >> 30)) * 0xbf58476d1ce4e5b9UL;
        z = (z ^ (z >> 27)) * 0x94d049bb133111ebUL;
        return z ^ (z >> 31);
    }

    public void NextBytes(Span<byte> buffer)
    {
        var words = MemoryMarshal.Cast<byte, ulong>(buffer);
        for (var i = 0; i < words.Length; i++)
            words[i] = NextUInt64();

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
