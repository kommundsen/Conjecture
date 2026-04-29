// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core;

namespace Conjecture.Interactive.Tests;

public class StrategyExtensionsInteractivePreviewTests
{
    // --- Preview ---

    [Fact]
    public void Preview_DefaultCount_Returns20Values()
    {
        Strategy<int> strategy = Strategy.Integers<int>(1, 1000);

        string text = strategy.Preview();

        // 20 values separated by ", " means 19 commas.
        int commaCount = CountOccurrences(text, ", ");
        Assert.Equal(19, commaCount);
    }

    [Fact]
    public void Preview_CountAbove100_IsCappedAt100()
    {
        Strategy<int> strategy = Strategy.Integers<int>(1, 1000);

        string text = strategy.Preview(150);

        // 100 values = 99 ", " separators on the first line.
        string valuesLine = text.Split('\n')[0];
        int commaCount = CountOccurrences(valuesLine, ", ");
        Assert.Equal(99, commaCount);
    }

    [Fact]
    public void Preview_CountAbove100_IncludesTruncationNotice()
    {
        Strategy<int> strategy = Strategy.Integers<int>(1, 1000);

        string text = strategy.Preview(150);

        Assert.Contains("capped", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("100", text);
    }

    [Fact]
    public void Preview_SameSeedTwice_ProducesIdenticalOutput()
    {
        Strategy<int> strategy = Strategy.Integers<int>(1, 1000);

        string first = strategy.Preview(seed: 42UL);
        string second = strategy.Preview(seed: 42UL);

        Assert.Equal(first, second);
    }

    [Fact]
    public void Preview_ReturnsCommaSeparatedValues()
    {
        Strategy<int> strategy = Strategy.Just(7);

        string text = strategy.Preview();

        Assert.Contains("7", text);
        Assert.Contains(", ", text);
    }

    // --- SampleTable ---

    [Fact]
    public void SampleTable_DefaultCount_Returns10Values()
    {
        Strategy<int> strategy = Strategy.Integers<int>(1, 1000);

        string text = strategy.SampleTable();

        // Header line + separator line + 10 data lines = 12 lines total.
        string[] lines = text.Split('\n');
        Assert.Equal(12, lines.Length);
    }

    [Fact]
    public void SampleTable_CountAbove50_IsCappedAt50()
    {
        Strategy<int> strategy = Strategy.Integers<int>(1, 1000);

        string text = strategy.SampleTable(60);

        // Header + separator + 50 data lines + capped notice = 53 lines.
        string[] lines = text.Split('\n');
        Assert.Equal(53, lines.Length);
    }

    [Fact]
    public void SampleTable_ContainsTableHeaderAndSeparator()
    {
        Strategy<int> strategy = Strategy.Just(99);

        string text = strategy.SampleTable();

        Assert.Contains("│", text);
        Assert.Contains("─", text);
        Assert.Contains("#", text);
        Assert.Contains("Value", text);
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