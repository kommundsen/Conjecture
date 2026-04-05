// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Conjecture.Tests.Internal;

public class TestOutputHelperLoggerTests
{
    private sealed class CapturingWriteLine
    {
        public List<string> Lines { get; } = [];
        public Action<string> Action => line => Lines.Add(line);
    }

    // IsEnabled

    [Theory]
    [InlineData(LogLevel.Information, true)]
    [InlineData(LogLevel.Warning, true)]
    [InlineData(LogLevel.Error, true)]
    [InlineData(LogLevel.Critical, true)]
    [InlineData(LogLevel.Debug, false)]
    [InlineData(LogLevel.Trace, false)]
    public void IsEnabled_DefaultMinLevel_ReturnsTrueAtOrAboveInformation(LogLevel level, bool expected)
    {
        CapturingWriteLine capture = new();
        TestOutputHelperLogger logger = new(capture.Action);

        Assert.Equal(expected, logger.IsEnabled(level));
    }

    [Fact]
    public void IsEnabled_CustomMinLevel_ReturnsCorrectly()
    {
        CapturingWriteLine capture = new();
        TestOutputHelperLogger logger = new(capture.Action, LogLevel.Debug);

        Assert.True(logger.IsEnabled(LogLevel.Debug));
        Assert.False(logger.IsEnabled(LogLevel.Trace));
    }

    // Log formatting

    [Fact]
    public void Log_AtEnabledLevel_FormatsAndCallsWriteLine()
    {
        CapturingWriteLine capture = new();
        TestOutputHelperLogger logger = new(capture.Action);

        logger.Log(LogLevel.Information, new EventId(1), "hello world", null,
            (state, _) => state);

        Assert.Single(capture.Lines);
        string line = capture.Lines[0];
        Assert.Contains("Information", line);
        Assert.Contains("hello world", line);
    }

    [Fact]
    public void Log_AtDisabledLevel_DoesNotCallWriteLine()
    {
        CapturingWriteLine capture = new();
        TestOutputHelperLogger logger = new(capture.Action);

        logger.Log(LogLevel.Debug, new EventId(10), "debug msg", null,
            (state, _) => state);

        Assert.Empty(capture.Lines);
    }

    [Theory]
    [InlineData(LogLevel.Information, "[Information]")]
    [InlineData(LogLevel.Warning, "[Warning]")]
    [InlineData(LogLevel.Error, "[Error]")]
    public void Log_IncludesLevelInBrackets(LogLevel level, string expectedFragment)
    {
        CapturingWriteLine capture = new();
        TestOutputHelperLogger logger = new(capture.Action);

        logger.Log(level, new EventId(1), "msg", null, (state, _) => state);

        Assert.Contains(expectedFragment, capture.Lines[0]);
    }

    // BeginScope

    [Fact]
    public void BeginScope_ReturnsNonNullDisposable()
    {
        CapturingWriteLine capture = new();
        TestOutputHelperLogger logger = new(capture.Action);

        IDisposable? scope = logger.BeginScope("scope state");

        Assert.NotNull(scope);
        scope.Dispose(); // must not throw
    }

    // FromWriteLine factory

    [Fact]
    public void FromWriteLine_WithNullAction_ReturnsNullLoggerInstance()
    {
        ILogger logger = TestOutputHelperLogger.FromWriteLine(null);

        Assert.IsType<NullLogger>(logger);
    }

    [Fact]
    public void FromWriteLine_WithAction_ReturnsTestOutputHelperLogger()
    {
        CapturingWriteLine capture = new();

        ILogger logger = TestOutputHelperLogger.FromWriteLine(capture.Action);

        Assert.IsType<TestOutputHelperLogger>(logger);
    }

    [Fact]
    public void FromWriteLine_WithCustomMinLevel_HonorsLevel()
    {
        CapturingWriteLine capture = new();
        ILogger logger = TestOutputHelperLogger.FromWriteLine(capture.Action, LogLevel.Debug);

        Assert.True(logger.IsEnabled(LogLevel.Debug));
    }
}
