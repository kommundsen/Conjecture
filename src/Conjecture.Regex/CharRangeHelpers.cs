// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Collections.Generic;

using Conjecture.Core;

namespace Conjecture.Regex;

internal static class CharRangeHelpers
{
    internal static IReadOnlyList<CharRange> ComplementRanges(
        IReadOnlyList<CharRange> ranges,
        char low,
        char high)
    {
        List<(int Lo, int Hi)> sorted = [];
        foreach (CharRange r in ranges)
        {
            sorted.Add((r.Low, r.High));
        }

        sorted.Sort(static (a, b) => a.Lo.CompareTo(b.Lo));

        List<CharRange> result = [];
        int cursor = low;
        foreach ((int rLo, int rHi) in sorted)
        {
            if (rLo > cursor)
            {
                result.Add(new CharRange((char)cursor, (char)(rLo - 1)));
            }

            int next = rHi + 1;
            if (next > cursor)
            {
                cursor = next;
            }

            if (cursor > high)
            {
                break;
            }
        }

        if (cursor <= high)
        {
            result.Add(new CharRange((char)cursor, high));
        }

        return result;
    }

    internal static IReadOnlyList<CharRange> IntersectRangeLists(
        IReadOnlyList<CharRange> a,
        IReadOnlyList<CharRange> b)
    {
        List<CharRange> result = [];
        foreach (CharRange ra in a)
        {
            foreach (CharRange rb in b)
            {
                int lo = ra.Low > rb.Low ? ra.Low : rb.Low;
                int hi = ra.High < rb.High ? ra.High : rb.High;
                if (lo <= hi)
                {
                    result.Add(new CharRange((char)lo, (char)hi));
                }
            }
        }

        return result;
    }

    internal static char SampleFromRanges(IReadOnlyList<CharRange> ranges, IGeneratorContext ctx)
    {
        int total = 0;
        foreach (CharRange r in ranges)
        {
            total += r.High - r.Low + 1;
        }

        if (total == 0)
        {
            return '\0';
        }

        int pick = ctx.Generate(Conjecture.Core.Generate.Integers<int>(0, total - 1));
        foreach (CharRange r in ranges)
        {
            int size = r.High - r.Low + 1;
            if (pick < size)
            {
                return (char)(r.Low + pick);
            }

            pick -= size;
        }

        return ranges[0].Low;
    }

    internal static bool CharInRanges(char ch, IReadOnlyList<CharRange> ranges)
    {
        foreach (CharRange r in ranges)
        {
            if (ch >= r.Low && ch <= r.High)
            {
                return true;
            }
        }

        return false;
    }
}