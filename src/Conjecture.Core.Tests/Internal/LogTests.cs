// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core.Internal;
using Microsoft.Extensions.Logging;

namespace Conjecture.Core.Tests.Internal;

public class LogTests
{
    private sealed class CollectingLogger : ILogger
    {
        public List<(LogLevel Level, EventId EventId, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Entries.Add((logLevel, eventId, formatter(state, exception)));
        }
    }

    private sealed class DisabledLogger : ILogger
    {
        public int LogCallCount { get; private set; }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => false;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            LogCallCount++;
        }
    }

    [Fact]
    public void GenerationCompleted_EmitsAtInformation_EventId1()
    {
        CollectingLogger logger = new();
        Log.GenerationCompleted(logger, valid: 100, unsatisfied: 23, durationMs: 142.0);
        Assert.Single(logger.Entries);
        (LogLevel level, EventId eventId, string _) = logger.Entries[0];
        Assert.Equal(LogLevel.Information, level);
        Assert.Equal(1, eventId.Id);
    }

    [Fact]
    public void ShrinkingStarted_EmitsAtInformation_EventId2()
    {
        CollectingLogger logger = new();
        Log.ShrinkingStarted(logger, nodeCount: 47);
        Assert.Single(logger.Entries);
        (LogLevel level, EventId eventId, string _) = logger.Entries[0];
        Assert.Equal(LogLevel.Information, level);
        Assert.Equal(2, eventId.Id);
    }

    [Fact]
    public void ShrinkingCompleted_EmitsAtInformation_EventId3()
    {
        CollectingLogger logger = new();
        Log.ShrinkingCompleted(logger, nodeCount: 12, shrinkCount: 31, durationMs: 18.0);
        Assert.Single(logger.Entries);
        (LogLevel level, EventId eventId, string _) = logger.Entries[0];
        Assert.Equal(LogLevel.Information, level);
        Assert.Equal(3, eventId.Id);
    }

    [Fact]
    public void TargetingStarted_EmitsAtInformation_EventId4()
    {
        CollectingLogger logger = new();
        Log.TargetingStarted(logger, labels: "score");
        Assert.Single(logger.Entries);
        (LogLevel level, EventId eventId, string _) = logger.Entries[0];
        Assert.Equal(LogLevel.Information, level);
        Assert.Equal(4, eventId.Id);
    }

    [Fact]
    public void TargetingCompleted_EmitsAtInformation_EventId5()
    {
        CollectingLogger logger = new();
        Log.TargetingCompleted(logger, labels: "score", bestScores: "42.0");
        Assert.Single(logger.Entries);
        (LogLevel level, EventId eventId, string _) = logger.Entries[0];
        Assert.Equal(LogLevel.Information, level);
        Assert.Equal(5, eventId.Id);
    }

    [Fact]
    public void DatabaseReplaying_EmitsAtInformation_EventId6()
    {
        CollectingLogger logger = new();
        Log.DatabaseReplaying(logger, bufferCount: 3);
        Assert.Single(logger.Entries);
        (LogLevel level, EventId eventId, string _) = logger.Entries[0];
        Assert.Equal(LogLevel.Information, level);
        Assert.Equal(6, eventId.Id);
    }

    [Fact]
    public void HighUnsatisfiedRatio_EmitsAtWarning_EventId7()
    {
        CollectingLogger logger = new();
        Log.HighUnsatisfiedRatio(logger, unsatisfied: 80, valid: 20, limit: 100);
        Assert.Single(logger.Entries);
        (LogLevel level, EventId eventId, string _) = logger.Entries[0];
        Assert.Equal(LogLevel.Warning, level);
        Assert.Equal(7, eventId.Id);
    }

    [Fact]
    public void DatabaseError_EmitsAtWarning_EventId8()
    {
        CollectingLogger logger = new();
        Log.DatabaseError(logger, errorMessage: "corrupt", exception: new Exception("x"));
        Assert.Single(logger.Entries);
        (LogLevel level, EventId eventId, string _) = logger.Entries[0];
        Assert.Equal(LogLevel.Warning, level);
        Assert.Equal(8, eventId.Id);
    }

    [Fact]
    public void PropertyTestFailure_EmitsAtError_EventId9()
    {
        CollectingLogger logger = new();
        Log.PropertyTestFailure(logger, exampleCount: 101, seed: "0xDEADBEEF");
        Assert.Single(logger.Entries);
        (LogLevel level, EventId eventId, string _) = logger.Entries[0];
        Assert.Equal(LogLevel.Error, level);
        Assert.Equal(9, eventId.Id);
    }

    [Fact]
    public void ShrinkPassProgress_EmitsAtDebug_EventId10()
    {
        CollectingLogger logger = new();
        Log.ShrinkPassProgress(logger, passName: "DeleteBlocks", madeProgress: true);
        Assert.Single(logger.Entries);
        (LogLevel level, EventId eventId, string _) = logger.Entries[0];
        Assert.Equal(LogLevel.Debug, level);
        Assert.Equal(10, eventId.Id);
    }

    [Fact]
    public void DatabaseSaved_EmitsAtDebug_EventId11()
    {
        CollectingLogger logger = new();
        Log.DatabaseSaved(logger, testIdHash: "abc123");
        Assert.Single(logger.Entries);
        (LogLevel level, EventId eventId, string _) = logger.Entries[0];
        Assert.Equal(LogLevel.Debug, level);
        Assert.Equal(11, eventId.Id);
    }

    [Fact]
    public void AllMethods_WhenLoggerDisabled_DoNotCallLog()
    {
        DisabledLogger logger = new();
        Log.GenerationCompleted(logger, valid: 1, unsatisfied: 0, durationMs: 1.0);
        Log.ShrinkingStarted(logger, nodeCount: 1);
        Log.ShrinkingCompleted(logger, nodeCount: 1, shrinkCount: 0, durationMs: 1.0);
        Log.TargetingStarted(logger, labels: "x");
        Log.TargetingCompleted(logger, labels: "x", bestScores: "1.0");
        Log.DatabaseReplaying(logger, bufferCount: 1);
        Log.HighUnsatisfiedRatio(logger, unsatisfied: 1, valid: 1, limit: 2);
        Log.DatabaseError(logger, errorMessage: "e", exception: new Exception());
        Log.PropertyTestFailure(logger, exampleCount: 1, seed: "0x0");
        Log.ShrinkPassProgress(logger, passName: "P", madeProgress: false);
        Log.DatabaseSaved(logger, testIdHash: "h");
        Assert.Equal(0, logger.LogCallCount);
    }
}
