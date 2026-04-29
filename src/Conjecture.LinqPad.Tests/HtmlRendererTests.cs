// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Text.RegularExpressions;

using Conjecture.Core;

namespace Conjecture.LinqPad.Tests;

public class HtmlRendererTests
{
    // --- HtmlSampleTable ---

    [Fact]
    public void HtmlSampleTable_Render_OutputContainsTableElement()
    {
        Strategy<int> strategy = Strategy.Just(1);

        string html = HtmlSampleTable.Render(strategy, count: 5, seed: 0);

        Assert.Contains("<table", html);
    }

    [Fact]
    public void HtmlSampleTable_Render_OutputContainsThElement()
    {
        Strategy<int> strategy = Strategy.Just(1);

        string html = HtmlSampleTable.Render(strategy, count: 5, seed: 0);

        Assert.Contains("<th", html);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    public void HtmlSampleTable_Render_ContainsExactlyCountDataRows(int count)
    {
        Strategy<int> strategy = Strategy.Integers<int>(1, 1000);

        string html = HtmlSampleTable.Render(strategy, count: count, seed: 0);

        int trCount = Regex.Matches(html, "<tr", RegexOptions.IgnoreCase).Count;
        // One <tr> per data row; header row uses <th>, not an extra <tr> with data.
        Assert.Equal(count, trCount);
    }

    [Fact]
    public void HtmlSampleTable_Render_CountAbove50_CapsAt50Rows()
    {
        Strategy<int> strategy = Strategy.Integers<int>(1, 1000);

        string html = HtmlSampleTable.Render(strategy, count: 75, seed: 0);

        int trCount = Regex.Matches(html, "<tr", RegexOptions.IgnoreCase).Count;
        Assert.Equal(50, trCount);
    }

    [Fact]
    public void HtmlSampleTable_Render_CountAbove50_ContainsTruncationNotice()
    {
        Strategy<int> strategy = Strategy.Integers<int>(1, 1000);

        string html = HtmlSampleTable.Render(strategy, count: 75, seed: 0);

        Assert.Contains("truncat", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void HtmlSampleTable_Render_SameSeedTwice_ProducesIdenticalOutput()
    {
        Strategy<int> strategy = Strategy.Integers<int>(1, 1000);

        string first = HtmlSampleTable.Render(strategy, count: 10, seed: 99);
        string second = HtmlSampleTable.Render(strategy, count: 10, seed: 99);

        Assert.Equal(first, second);
    }

    // --- HtmlPreview ---

    [Fact]
    public void HtmlPreview_Render_OutputIsSpanElement()
    {
        Strategy<int> strategy = Strategy.Just(7);

        string html = HtmlPreview.Render(strategy, count: 5, seed: 0);

        Assert.StartsWith("<span", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void HtmlPreview_Render_OutputContainsCommaSeparatedValues()
    {
        Strategy<int> strategy = Strategy.Just(42);

        string html = HtmlPreview.Render(strategy, count: 5, seed: 0);

        Assert.Contains(",", html);
        Assert.Contains("42", html);
    }

    [Fact]
    public void HtmlPreview_Render_DefaultCount_Contains20Values()
    {
        Strategy<int> strategy = Strategy.Integers<int>(1, 1000);

        string html = HtmlPreview.Render(strategy, seed: 0);

        // 20 values produce 19 commas inside the span content.
        int commaCount = html.Split(',').Length - 1;
        Assert.Equal(19, commaCount);
    }

    [Fact]
    public void HtmlPreview_Render_CountAbove100_CapsAt100Values()
    {
        Strategy<int> strategy = Strategy.Integers<int>(1, 1000);

        string html = HtmlPreview.Render(strategy, count: 150, seed: 0);

        // The truncation notice may contain commas too; strip it first.
        // We verify the values portion has exactly 99 commas (100 items).
        int commaCount = html.Split(',').Length - 1;
        Assert.True(commaCount >= 99, $"Expected at least 99 commas (100 values), got {commaCount}");
    }

    [Fact]
    public void HtmlPreview_Render_CountAbove100_ContainsTruncationNotice()
    {
        Strategy<int> strategy = Strategy.Integers<int>(1, 1000);

        string html = HtmlPreview.Render(strategy, count: 150, seed: 0);

        Assert.Contains("truncat", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void HtmlPreview_Render_SameSeedTwice_ProducesIdenticalOutput()
    {
        Strategy<int> strategy = Strategy.Integers<int>(1, 1000);

        string first = HtmlPreview.Render(strategy, count: 20, seed: 7);
        string second = HtmlPreview.Render(strategy, count: 20, seed: 7);

        Assert.Equal(first, second);
    }

    // --- SvgHistogram ---

    [Fact]
    public void SvgHistogram_Render_OutputContainsSvgElement()
    {
        Strategy<int> strategy = Strategy.Integers<int>(1, 100);

        string svg = SvgHistogram.Render(strategy, sampleSize: 100, bucketCount: 10, seed: 0);

        Assert.Contains("<svg", svg);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(20)]
    public void SvgHistogram_Render_ContainsExactlyBucketCountRectElements(int bucketCount)
    {
        Strategy<int> strategy = Strategy.Integers<int>(1, 1000);

        string svg = SvgHistogram.Render(strategy, sampleSize: 200, bucketCount: bucketCount, seed: 0);

        int rectCount = Regex.Matches(svg, "<rect", RegexOptions.IgnoreCase).Count;
        Assert.Equal(bucketCount, rectCount);
    }

    [Fact]
    public void SvgHistogram_Render_SameSeedTwice_ProducesIdenticalOutput()
    {
        Strategy<int> strategy = Strategy.Integers<int>(1, 1000);

        string first = SvgHistogram.Render(strategy, sampleSize: 100, bucketCount: 20, seed: 5);
        string second = SvgHistogram.Render(strategy, sampleSize: 100, bucketCount: 20, seed: 5);

        Assert.Equal(first, second);
    }

    [Fact]
    public void SvgHistogram_Render_OutputContainsBucketRangeLabels()
    {
        Strategy<int> strategy = Strategy.Integers<int>(1, 100);

        string svg = SvgHistogram.Render(strategy, sampleSize: 100, bucketCount: 10, seed: 0);

        // Range labels appear as text elements in the SVG.
        Assert.Contains("<text", svg, StringComparison.OrdinalIgnoreCase);
    }

    // --- Zero-sample edge cases ---

    [Fact]
    public void HtmlSampleTable_Render_ZeroCount_ReturnsTableWithNoDataRows()
    {
        Strategy<int> strategy = Strategy.Integers<int>(1, 1000);

        string html = HtmlSampleTable.Render(strategy, count: 0, seed: 0);

        Assert.Contains("<table", html);
        Assert.DoesNotContain("<tr>", html);
    }

    [Fact]
    public void HtmlPreview_Render_ZeroCount_ReturnsEmptySpan()
    {
        Strategy<int> strategy = Strategy.Integers<int>(1, 1000);

        string html = HtmlPreview.Render(strategy, count: 0, seed: 0);

        Assert.StartsWith("<span", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(",", html);
    }

    [Fact]
    public void SvgHistogram_Render_ZeroSampleSize_ReturnsSvgElement()
    {
        Strategy<int> strategy = Strategy.Integers<int>(1, 1000);

        string svg = SvgHistogram.Render(strategy, sampleSize: 0, seed: 0);

        Assert.Contains("<svg", svg);
    }
}