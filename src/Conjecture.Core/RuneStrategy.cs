// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Text;

using Conjecture.Core.Internal;

namespace Conjecture.Core;

internal sealed class RuneStrategy(Rune min, Rune max) : Strategy<Rune>
{
    // Surrogate range: [0xD800, 0xDFFF] — 0x800 codepoints to skip.
    private const int SurrogateFirst = 0xD800;
    private const int SurrogateLast = 0xDFFF;
    private const int SurrogateCount = SurrogateLast - SurrogateFirst + 1;

    internal override Rune Generate(ConjectureData data)
    {
        if (min == max)
        {
            return min;
        }

        int minVal = min.Value;
        int maxVal = max.Value;

        // Count valid (non-surrogate) codepoints in [minVal, maxVal].
        int surrogatesInRange = SurrogatesInRange(minVal, maxVal);
        int validCount = maxVal - minVal + 1 - surrogatesInRange;

        // Draw an offset in [0, validCount - 1]; raw=0 maps to minVal (shrink target).
        ulong raw = data.NextInteger(0UL, (ulong)(validCount - 1));
        int offset = (int)raw;

        // Map offset back to a valid codepoint, skipping the surrogate gap.
        int codepoint = minVal + offset;
        if (surrogatesInRange > 0 && codepoint >= SurrogateFirst)
        {
            codepoint += surrogatesInRange;
        }

        return new Rune(codepoint);
    }

    private static int SurrogatesInRange(int lo, int hi)
    {
        // Number of surrogate codepoints in [lo, hi].
        int overlapLo = Math.Max(lo, SurrogateFirst);
        int overlapHi = Math.Min(hi, SurrogateLast);
        return overlapHi >= overlapLo ? overlapHi - overlapLo + 1 : 0;
    }
}