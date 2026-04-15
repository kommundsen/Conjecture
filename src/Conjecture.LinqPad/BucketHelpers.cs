// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Generic;

namespace Conjecture.LinqPad;

internal static class BucketHelpers
{
    internal static int[] Compute(IReadOnlyList<double> values, int bucketCount)
    {
        int[] counts = new int[bucketCount];
        if (values.Count == 0)
        {
            return counts;
        }

        double min = values[0];
        double max = values[0];
        foreach (double v in values)
        {
            if (v < min)
            {
                min = v;
            }

            if (v > max)
            {
                max = v;
            }
        }

        double effectiveMax = min == max ? min + 1.0 : max;
        double range = effectiveMax - min;

        foreach (double v in values)
        {
            int bucket = (int)((v - min) / range * bucketCount);
            if (bucket >= bucketCount)
            {
                bucket = bucketCount - 1;
            }

            counts[bucket]++;
        }

        return counts;
    }
}