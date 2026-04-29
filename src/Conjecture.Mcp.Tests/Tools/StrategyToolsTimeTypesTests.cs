// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Mcp.Tools;

namespace Conjecture.Mcp.Tests.Tools;

public class StrategyToolsTimeTypesTests
{
    // DateOnly
    [Fact]
    public void SuggestForType_DateOnly_ContainsDateOnlyValues()
    {
        string result = StrategyTools.SuggestForType("DateOnly");
        Assert.Contains("Strategy.DateOnlyValues()", result);
    }

    [Fact]
    public void SuggestForType_DateOnly_MentionsNearMonthBoundary()
    {
        string result = StrategyTools.SuggestForType("DateOnly");
        Assert.Contains("NearMonthBoundary()", result);
    }

    [Fact]
    public void SuggestForType_DateOnly_MentionsNearLeapDay()
    {
        string result = StrategyTools.SuggestForType("DateOnly");
        Assert.Contains("NearLeapDay()", result);
    }

    // TimeOnly
    [Fact]
    public void SuggestForType_TimeOnly_ContainsTimeOnlyValues()
    {
        string result = StrategyTools.SuggestForType("TimeOnly");
        Assert.Contains("Strategy.TimeOnlyValues()", result);
    }

    [Fact]
    public void SuggestForType_TimeOnly_MentionsNearMidnight()
    {
        string result = StrategyTools.SuggestForType("TimeOnly");
        Assert.Contains("NearMidnight()", result);
    }

    [Fact]
    public void SuggestForType_TimeOnly_MentionsNearNoon()
    {
        string result = StrategyTools.SuggestForType("TimeOnly");
        Assert.Contains("NearNoon()", result);
    }

    [Fact]
    public void SuggestForType_TimeOnly_MentionsNearEndOfDay()
    {
        string result = StrategyTools.SuggestForType("TimeOnly");
        Assert.Contains("NearEndOfDay()", result);
    }

    // DateTime (kind-sensitive)
    [Fact]
    public void SuggestForType_DateTime_ContainsWithKinds()
    {
        string result = StrategyTools.SuggestForType("DateTime");
        Assert.Contains("WithKinds()", result);
    }

    [Fact]
    public void SuggestForType_DateTime_ContainsGenerateDateTimes()
    {
        string result = StrategyTools.SuggestForType("DateTime");
        Assert.Contains("Strategy.DateTimes()", result);
    }

    // TimeZoneInfo (DST focus)
    [Fact]
    public void SuggestForType_TimeZoneInfo_ContainsTimeZonePreferDst()
    {
        string result = StrategyTools.SuggestForType("TimeZoneInfo");
        Assert.Contains("Strategy.TimeZone(preferDst: true)", result);
    }

    // FakeTimeProvider / TimeProvider (adversarial)
    [Fact]
    public void SuggestForType_FakeTimeProvider_ContainsClockWithAdvances()
    {
        string result = StrategyTools.SuggestForType("FakeTimeProvider");
        Assert.Contains("Strategy.ClockWithAdvances(", result);
    }

    [Fact]
    public void SuggestForType_TimeProvider_ContainsClockWithAdvances()
    {
        string result = StrategyTools.SuggestForType("TimeProvider");
        Assert.Contains("Strategy.ClockWithAdvances(", result);
    }

    // DateTimeOffset — updated to mention WithPrecision and WithStrippedOffset
    [Fact]
    public void SuggestForType_DateTimeOffset_ContainsGenerateDateTimeOffsets()
    {
        string result = StrategyTools.SuggestForType("DateTimeOffset");
        Assert.Contains("Strategy.DateTimeOffsets()", result);
    }

    [Fact]
    public void SuggestForType_DateTimeOffset_MentionsWithPrecision()
    {
        string result = StrategyTools.SuggestForType("DateTimeOffset");
        Assert.Contains("WithPrecision(", result);
    }

    [Fact]
    public void SuggestForType_DateTimeOffset_MentionsWithStrippedOffset()
    {
        string result = StrategyTools.SuggestForType("DateTimeOffset");
        Assert.Contains("WithStrippedOffset()", result);
    }
}