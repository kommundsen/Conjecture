// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using Conjecture.Core;

using LINQPad;

namespace Conjecture.LinqPad.Tests;

public class ShrinkTraceLinqPadTests
{
    // --- HtmlShrinkTrace.Render ---

    [Fact]
    public void HtmlShrinkTrace_Render_OutputContainsTableElement()
    {
        List<int> steps = [10, 5, 3];

        string html = HtmlShrinkTrace.Render<int>(steps);

        Assert.Contains("<table", html);
    }

    [Fact]
    public void HtmlShrinkTrace_Render_OutputContainsStepColumnHeader()
    {
        List<int> steps = [10, 5, 3];

        string html = HtmlShrinkTrace.Render<int>(steps);

        Assert.Contains("Step", html);
    }

    [Fact]
    public void HtmlShrinkTrace_Render_OutputContainsValueColumnHeader()
    {
        List<int> steps = [10, 5, 3];

        string html = HtmlShrinkTrace.Render<int>(steps);

        Assert.Contains("Value", html);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    public void HtmlShrinkTrace_Render_ContainsExactlyStepCountRows(int count)
    {
        List<int> steps = [];
        for (int i = 0; i < count; i++)
        {
            steps.Add(i);
        }

        string html = HtmlShrinkTrace.Render<int>(steps);

        int trCount = Regex.Matches(html, "<tr", RegexOptions.IgnoreCase).Count;
        Assert.Equal(count, trCount);
    }

    [Fact]
    public void HtmlShrinkTrace_Render_EmptyList_OutputContainsTableWithNoRows()
    {
        List<int> steps = [];

        string html = HtmlShrinkTrace.Render<int>(steps);

        Assert.Contains("<table", html);
        Assert.DoesNotContain("<tr>", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void HtmlShrinkTrace_Render_SingleStep_ContainsStepValue()
    {
        List<int> steps = [42];

        string html = HtmlShrinkTrace.Render<int>(steps);

        Assert.Contains("42", html);
    }

    // --- StrategyLinqPadExtensions.ShrinkTraceHtml ---

    [Fact]
    public void ShrinkTraceHtml_ReturnsNonNull()
    {
        Strategy<int> strategy = Generate.Integers<int>(1, 100);

        object result = strategy.ShrinkTraceHtml(seed: 0, static x => x > 0);

        Assert.NotNull(result);
    }

    [Fact]
    public void ShrinkTraceHtml_ToStringContainsTableElement()
    {
        Strategy<int> strategy = Generate.Integers<int>(1, 100);

        object result = strategy.ShrinkTraceHtml(seed: 0, static x => x > 0);

        Assert.Contains("<table", result.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ShrinkTraceHtml_ToStringContainsAtLeastOneTrElement()
    {
        Strategy<int> strategy = Generate.Integers<int>(1, 100);

        object result = strategy.ShrinkTraceHtml(seed: 0, static x => x > 0);

        Assert.Contains("<tr", result.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ShrinkTraceHtml_SameSeedTwice_ProducesIdenticalOutput()
    {
        Strategy<int> strategy = Generate.Integers<int>(1, 100);

        object first = strategy.ShrinkTraceHtml(seed: 1, static x => x > 0);
        object second = strategy.ShrinkTraceHtml(seed: 1, static x => x > 0);

        Assert.Equal(first.ToString(), second.ToString());
    }
}