// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Generic;

namespace Conjecture.Interactive.Tests;

public class TextHistogramTests
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
    public void Render_AnyInput_OutputContainsBarCharacters()
    {
        IReadOnlyList<double> values = SpreadAcross20Buckets();

        string text = TextHistogram.Render(values);

        Assert.Contains("█", text);
        Assert.Contains("│", text);
    }

    [Fact]
    public void Render_ValuesSpreadAcrossAllBuckets_ContainsExactlyBucketCountLines()
    {
        IReadOnlyList<double> values = SpreadAcross20Buckets();
        int bucketCount = 20;

        string text = TextHistogram.Render(values, bucketCount);

        // Each bucket is one line; lines are separated by newlines.
        string[] lines = text.Split('\n');
        Assert.Equal(bucketCount, lines.Length);
    }

    [Fact]
    public void Render_AllSameValue_DoesNotThrow()
    {
        IReadOnlyList<double> values = [5.0, 5.0, 5.0, 5.0, 5.0];

        string text = TextHistogram.Render(values);

        Assert.NotNull(text);
    }

    [Fact]
    public void Render_EmptyInput_DoesNotThrow()
    {
        IReadOnlyList<double> values = [];

        string text = TextHistogram.Render(values);

        Assert.NotNull(text);
    }
}