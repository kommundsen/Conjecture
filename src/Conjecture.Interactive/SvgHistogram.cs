// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Conjecture.Interactive;

/// <summary>Renders a histogram of numeric values as an SVG string.</summary>
public static class SvgHistogram
{
    private const int SvgWidth = 400;
    private const int SvgHeight = 150;

    /// <summary>Renders a histogram of <paramref name="values"/> as an SVG string.</summary>
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

        double barWidth = (double)SvgWidth / bucketCount;

        StringBuilder sb = new();
        sb.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"");
        sb.Append(SvgWidth);
        sb.Append("\" height=\"");
        sb.Append(SvgHeight);
        sb.Append("\">");

        for (int i = 0; i < bucketCount; i++)
        {
            if (counts[i] == 0)
            {
                continue;
            }

            double barHeight = (double)counts[i] / maxCount * SvgHeight;
            double x = i * barWidth;
            double y = SvgHeight - barHeight;
            double bucketMin = min + i * range / bucketCount;
            double bucketMax = min + (i + 1) * range / bucketCount;

            sb.Append("<rect x=\"");
            sb.Append(x.ToString("F2", CultureInfo.InvariantCulture));
            sb.Append("\" y=\"");
            sb.Append(y.ToString("F2", CultureInfo.InvariantCulture));
            sb.Append("\" width=\"");
            sb.Append(barWidth.ToString("F2", CultureInfo.InvariantCulture));
            sb.Append("\" height=\"");
            sb.Append(barHeight.ToString("F2", CultureInfo.InvariantCulture));
            sb.Append("\" fill=\"steelblue\">");
            sb.Append("<title>");
            sb.Append(bucketMin.ToString("F2", CultureInfo.InvariantCulture));
            sb.Append(" – ");
            sb.Append(bucketMax.ToString("F2", CultureInfo.InvariantCulture));
            sb.Append(": ");
            sb.Append(counts[i]);
            sb.Append("</title></rect>");
        }

        sb.Append("</svg>");
        return sb.ToString();
    }
}