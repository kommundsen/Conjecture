// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core;
using Conjecture.Core.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Conjecture.Core.Tests.Internal;

public class TestRunnerLoggingTests
{
    private sealed class CollectingLogger : ILogger
    {
        public List<(LogLevel Level, EventId EventId)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Entries.Add((logLevel, eventId));
        }
    }

    [Fact]
    public async Task Run_PassingRun_LogsGenerationCompleted()
    {
        CollectingLogger logger = new();
        ConjectureSettings settings = new() { Logger = logger, MaxExamples = 5, Seed = 1UL };

        await TestRunner.Run(settings, _ => { });

        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Information && e.EventId.Id == 1);
    }

    [Fact]
    public async Task Run_FailingRun_LogsPropertyTestFailure()
    {
        CollectingLogger logger = new();
        ConjectureSettings settings = new() { Logger = logger, MaxExamples = 5, Seed = 1UL };

        await TestRunner.Run(settings, _ => throw new InvalidOperationException("fail"));

        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Error && e.EventId.Id == 9);
    }

    [Fact]
    public async Task Run_HighAssumptionRejection_LogsHighUnsatisfiedRatioWarning()
    {
        CollectingLogger logger = new();
        // MaxUnsatisfiedRatio=4: warn when unsatisfied > valid * 2, throw when > valid * 4
        ConjectureSettings settings = new() { Logger = logger, MaxExamples = 100, MaxUnsatisfiedRatio = 4, Seed = 1UL };

        int callCount = 0;
        await Assert.ThrowsAsync<ConjectureException>(() => TestRunner.RunAsync(settings, _ =>
        {
            callCount++;
            if (callCount > 1)
            {
                throw new UnsatisfiedAssumptionException();
            }

            return Task.CompletedTask;
        }));

        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning && e.EventId.Id == 7);
    }

    [Fact]
    public async Task Run_WithNullLogger_PassingRunDoesNotThrow()
    {
        ConjectureSettings settings = new() { MaxExamples = 5, Seed = 1UL };
        Assert.Same(NullLogger.Instance, settings.Logger);

        await TestRunner.Run(settings, _ => { });
    }

    [Fact]
    public async Task Run_WithNullLogger_FailingRunDoesNotThrow()
    {
        ConjectureSettings settings = new() { MaxExamples = 5, Seed = 1UL };

        TestRunResult result = await TestRunner.Run(settings,
            _ => throw new InvalidOperationException("fail"));

        Assert.False(result.Passed);
    }
}
