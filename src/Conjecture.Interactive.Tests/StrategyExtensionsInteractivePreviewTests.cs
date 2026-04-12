// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core;

using Conjecture.Interactive;

namespace Conjecture.Interactive.Tests;

public class StrategyExtensionsInteractivePreviewTests
{
    // --- Preview ---

    [Fact]
    public void Preview_DefaultCount_Returns20Values()
    {
        Strategy<int> strategy = Generate.Integers<int>(1, 1000);

        string html = strategy.Preview();

        // 20 <tr> rows for data (thead adds one more, but easiest proxy is counting the value cells)
        int tdCount = CountOccurrences(html, "<td");
        Assert.Equal(20, tdCount);
    }

    [Fact]
    public void Preview_CountAbove100_IsCappedAt100()
    {
        Strategy<int> strategy = Generate.Integers<int>(1, 1000);

        string html = strategy.Preview(150);

        int tdCount = CountOccurrences(html, "<td");
        Assert.Equal(100, tdCount);
    }

    [Fact]
    public void Preview_CountAbove100_IncludesTruncationNotice()
    {
        Strategy<int> strategy = Generate.Integers<int>(1, 1000);

        string html = strategy.Preview(150);

        // The output must mention capping/truncation in some form
        bool hasTruncationNotice =
            html.Contains("100", StringComparison.OrdinalIgnoreCase) &&
            (html.Contains("capped", StringComparison.OrdinalIgnoreCase) ||
             html.Contains("truncat", StringComparison.OrdinalIgnoreCase) ||
             html.Contains("warn", StringComparison.OrdinalIgnoreCase));
        Assert.True(hasTruncationNotice, "Expected a truncation/cap notice in the HTML output.");
    }

    [Fact]
    public void Preview_SameSeedTwice_ProducesIdenticalHtml()
    {
        Strategy<int> strategy = Generate.Integers<int>(1, 1000);

        string first = strategy.Preview(seed: 42UL);
        string second = strategy.Preview(seed: 42UL);

        Assert.Equal(first, second);
    }

    [Fact]
    public void Preview_ReturnsHtmlTableTag()
    {
        Strategy<int> strategy = Generate.Just(7);

        string html = strategy.Preview();

        Assert.Contains("<table", html, StringComparison.OrdinalIgnoreCase);
    }

    // --- SampleTable ---

    [Fact]
    public void SampleTable_DefaultCount_Returns10Values()
    {
        Strategy<int> strategy = Generate.Integers<int>(1, 1000);

        string html = strategy.SampleTable();

        int tdCount = CountOccurrences(html, "<td");
        Assert.Equal(10, tdCount);
    }

    [Fact]
    public void SampleTable_CountAbove50_IsCappedAt50()
    {
        Strategy<int> strategy = Generate.Integers<int>(1, 1000);

        string html = strategy.SampleTable(60);

        int tdCount = CountOccurrences(html, "<td");
        Assert.Equal(50, tdCount);
    }

    [Fact]
    public void SampleTable_ReturnsHtmlTableTag()
    {
        Strategy<int> strategy = Generate.Just(99);

        string html = strategy.SampleTable();

        Assert.Contains("<table", html, StringComparison.OrdinalIgnoreCase);
    }

    // --- helpers ---

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