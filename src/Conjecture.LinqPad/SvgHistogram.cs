// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

using Conjecture.Core;

namespace Conjecture.LinqPad;

internal static class SvgHistogram
{
    private const int SvgWidth = 600;
    private const int SvgHeight = 200;
    private const int PaddingLeft = 10;
    private const int PaddingRight = 10;
    private const int PaddingTop = 10;
    private const int PaddingBottom = 30;

    internal static string Render<T>(Strategy<T> strategy, int sampleSize = 1000, int bucketCount = 20, int? seed = null)
        where T : IConvertible
    {
        ulong? ulongSeed = SeedHelpers.ToUlong(seed);
        IReadOnlyList<T> samples = ulongSeed is { } s ? strategy.WithSeed(s).Sample(sampleSize) : strategy.Sample(sampleSize);

        List<double> doubles = new(samples.Count);
        foreach (T value in samples)
        {
            doubles.Add(Convert.ToDouble(value, CultureInfo.InvariantCulture));
        }

        if (doubles.Count == 0)
        {
            return "<svg></svg>";
        }

        double min = doubles[0];
        double max = doubles[0];
        foreach (double v in doubles)
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

        int[] counts = BucketHelpers.Compute(doubles, bucketCount);

        int maxCount = 0;
        foreach (int c in counts)
        {
            if (c > maxCount)
            {
                maxCount = c;
            }
        }

        int chartWidth = SvgWidth - PaddingLeft - PaddingRight;
        int chartHeight = SvgHeight - PaddingTop - PaddingBottom;
        double barWidth = (double)chartWidth / bucketCount;

        StringBuilder sb = new();
        sb.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"");
        sb.Append(SvgWidth);
        sb.Append("\" height=\"");
        sb.Append(SvgHeight);
        sb.Append("\">");

        for (int i = 0; i < bucketCount; i++)
        {
            double barHeight = maxCount > 0 ? (double)counts[i] / maxCount * chartHeight : 0;
            double x = PaddingLeft + i * barWidth;
            double y = PaddingTop + (chartHeight - barHeight);

            sb.Append("<rect x=\"");
            sb.Append(x.ToString("F2", CultureInfo.InvariantCulture));
            sb.Append("\" y=\"");
            sb.Append(y.ToString("F2", CultureInfo.InvariantCulture));
            sb.Append("\" width=\"");
            sb.Append((barWidth - 1).ToString("F2", CultureInfo.InvariantCulture));
            sb.Append("\" height=\"");
            sb.Append(barHeight.ToString("F2", CultureInfo.InvariantCulture));
            sb.Append("\" fill=\"steelblue\"/>");

            double bucketMin = min + i * range / bucketCount;
            double bucketMax = min + (i + 1) * range / bucketCount;
            double labelX = x + barWidth / 2;
            double labelY = SvgHeight - 5;

            sb.Append("<text x=\"");
            sb.Append(labelX.ToString("F2", CultureInfo.InvariantCulture));
            sb.Append("\" y=\"");
            sb.Append(labelY.ToString("F2", CultureInfo.InvariantCulture));
            sb.Append("\" font-size=\"8\" text-anchor=\"middle\">");
            sb.Append(bucketMin.ToString("F1", CultureInfo.InvariantCulture));
            sb.Append("-");
            sb.Append(bucketMax.ToString("F1", CultureInfo.InvariantCulture));
            sb.Append("</text>");
        }

        sb.Append("</svg>");
        return sb.ToString();
    }
}