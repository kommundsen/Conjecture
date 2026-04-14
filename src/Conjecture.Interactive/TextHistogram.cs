// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Conjecture.Interactive;

/// <summary>Renders a histogram of numeric values as a text bar chart.</summary>
public static class TextHistogram
{
    private const int MaxBarWidth = 30;

    /// <summary>Renders a histogram of <paramref name="values"/> as a text bar chart.</summary>
    public static string Render(IReadOnlyList<double> values, int bucketCount = 20)
    {
        if (values.Count == 0)
        {
            return string.Empty;
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

        int[] counts = new int[bucketCount];
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

        int maxCount = 0;
        foreach (int c in counts)
        {
            if (c > maxCount)
            {
                maxCount = c;
            }
        }

        // Determine label width for alignment.
        string maxLabel = FormatRange(
            min + (bucketCount - 1) * range / bucketCount,
            min + bucketCount * range / bucketCount);
        int labelWidth = maxLabel.Length;

        StringBuilder sb = new();
        for (int i = 0; i < bucketCount; i++)
        {
            double bucketMin = min + i * range / bucketCount;
            double bucketMax = min + (i + 1) * range / bucketCount;
            string label = FormatRange(bucketMin, bucketMax);

            sb.Append(label.PadLeft(labelWidth));
            sb.Append(" │");

            if (counts[i] > 0 && maxCount > 0)
            {
                int barLen = (int)Math.Ceiling((double)counts[i] / maxCount * MaxBarWidth);
                sb.Append(new string('█', barLen));
            }

            sb.Append(' ');
            sb.Append(counts[i]);

            if (i < bucketCount - 1)
            {
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }

    private static string FormatRange(double lo, double hi)
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            "{0:F2} – {1:F2}",
            lo,
            hi);
    }
}