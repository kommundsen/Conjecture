// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Generic;

namespace Conjecture.Interactive.Tests;

public class SvgHistogramTests
{
    // A spread across 20 buckets: 20 evenly-spaced values from 0.5 to 19.5
    // ensures each bucket [0,1), [1,2), … [19,20) gets exactly one hit.
    private static IReadOnlyList<double> SpreadAcross20Buckets()
    {
        double[] values = new double[20];
        for (int i = 0; i < 20; i++)
        {
            values[i] = i + 0.5;
        }

        return values;
    }

    [Fact]
    public void Render_AnyInput_OutputContainsSvgTag()
    {
        IReadOnlyList<double> values = SpreadAcross20Buckets();

        string svg = SvgHistogram.Render(values);

        Assert.Contains("<svg", svg, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Render_ValuesSpreadAcrossAllBuckets_ContainsExactlyBucketCountRects()
    {
        IReadOnlyList<double> values = SpreadAcross20Buckets();
        int bucketCount = 20;

        string svg = SvgHistogram.Render(values, bucketCount);

        int rectCount = CountOccurrences(svg, "<rect");
        Assert.Equal(bucketCount, rectCount);
    }

    [Fact]
    public void Render_AllSameValue_DoesNotThrow()
    {
        IReadOnlyList<double> values = [5.0, 5.0, 5.0, 5.0, 5.0];

        string svg = SvgHistogram.Render(values);

        Assert.NotNull(svg);
    }

    [Fact]
    public void Render_EmptyInput_DoesNotThrow()
    {
        IReadOnlyList<double> values = [];

        string svg = SvgHistogram.Render(values);

        Assert.NotNull(svg);
    }

    private static int CountOccurrences(string source, string token)
    {
        int count = 0;
        int index = 0;
        while ((index = source.IndexOf(token, index, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            count++;
            index += token.Length;
        }

        return count;
    }
}