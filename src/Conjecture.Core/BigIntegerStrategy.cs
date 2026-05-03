// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Numerics;

using Conjecture.Core.Internal;

namespace Conjecture.Core;

internal sealed class BigIntegerStrategy(BigInteger min, BigInteger max) : Strategy<BigInteger>
{
    private readonly BigInteger min = min > max
        ? throw new ArgumentOutOfRangeException(nameof(min), "min must be less than or equal to max.")
        : min;
    private readonly BigInteger max = max;

    internal override BigInteger Generate(ConjectureData data)
    {
        if (min == max)
        {
            return min;
        }

        BigInteger range = max - min;

        // Use interleaved-around-zero encoding when zero is strictly inside [min, max].
        bool useInterleaved = min < BigInteger.Zero && max > BigInteger.Zero;

        BigInteger raw = DrawRaw(data, range);

        if (useInterleaved)
        {
            return InterleavedDecodeAroundZero(raw);
        }

        // For all-positive ranges, min + raw shrinks toward min (closest to zero).
        // For all-negative ranges, max - raw shrinks toward max (closest to zero).
        return max < BigInteger.Zero ? max - raw : min + raw;
    }

    private static BigInteger DrawRaw(ConjectureData data, BigInteger range)
    {
        // Determine how many bytes are needed to represent range.
        int byteLen = range.GetByteCount(isUnsigned: true);

        // Number of full 8-byte (ulong) chunks needed to cover byteLen bytes.
        int ulongCount = (byteLen + 7) / 8;

        if (ulongCount == 1)
        {
            ulong raw = data.NextInteger(0UL, (ulong)range);
            return (BigInteger)raw;
        }

        // For multi-chunk draws: build the value chunk by chunk (big-endian).
        // The highest chunk is bounded by the high portion of range;
        // lower chunks are bounded by ulong.MaxValue unless the high chunk
        // exactly equals the high bound, in which case they're bounded by
        // their respective parts of range.
        BigInteger result = DrawMultiChunk(data, range, ulongCount);
        return result;
    }

    private static BigInteger DrawMultiChunk(ConjectureData data, BigInteger range, int ulongCount)
    {
        // We decompose range into ulongCount chunks of 64 bits each (big-endian order).
        // chunk[0] is the most-significant chunk.
        ulong[] rangeChunks = new ulong[ulongCount];
        BigInteger remaining = range;
        for (int i = ulongCount - 1; i >= 0; i--)
        {
            rangeChunks[i] = (ulong)(remaining & ulong.MaxValue);
            remaining >>= 64;
        }

        // Draw each chunk, constraining based on whether higher chunks are at their max.
        ulong[] drawnChunks = new ulong[ulongCount];
        bool atMax = true;
        for (int i = 0; i < ulongCount; i++)
        {
            ulong chunkMax = atMax ? rangeChunks[i] : ulong.MaxValue;
            drawnChunks[i] = data.NextInteger(0UL, chunkMax);
            if (drawnChunks[i] < chunkMax)
            {
                atMax = false;
            }
        }

        // Reassemble the BigInteger from drawn chunks (big-endian).
        BigInteger result = BigInteger.Zero;
        for (int i = 0; i < ulongCount; i++)
        {
            result = (result << 64) | (BigInteger)drawnChunks[i];
        }

        return result;
    }

    private BigInteger InterleavedDecodeAroundZero(BigInteger raw)
    {
        if (raw == BigInteger.Zero)
        {
            return BigInteger.Zero;
        }

        BigInteger step = (raw + BigInteger.One) >> 1;
        bool goNegative = !raw.IsEven;

        if (goNegative)
        {
            BigInteger candidate = BigInteger.Zero - step;
            BigInteger stepsBelow = BigInteger.Zero - min;
            return candidate >= min ? candidate : BigInteger.Zero + (step - stepsBelow);
        }

        BigInteger positiveCandidate = BigInteger.Zero + step;
        return positiveCandidate <= max ? positiveCandidate : BigInteger.Zero - (step - max);
    }
}