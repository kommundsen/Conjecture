// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core;
using Conjecture.Core.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Conjecture.Core.Tests.Logging.EndToEnd;

public class LoggingEndToEndTests
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
    public async Task Run_PassingProperty_EmitsGenerationCompleted()
    {
        CollectingLogger logger = new();
        ConjectureSettings settings = new() { Logger = logger, MaxExamples = 10, Seed = 1UL };

        await TestRunner.Run(settings, _ => { });

        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Information && e.EventId.Id == 1);
    }

    [Fact]
    public async Task Run_FailingProperty_EmitsPropertyTestFailureAndShrinkEvents()
    {
        CollectingLogger logger = new();
        ConjectureSettings settings = new() { Logger = logger, MaxExamples = 10, Seed = 1UL };

        await TestRunner.Run(settings, _ => throw new InvalidOperationException("always fails"));

        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Error && e.EventId.Id == 9);
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Information && e.EventId.Id == 2);
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Information && e.EventId.Id == 3);
    }

    [Fact]
    public async Task Run_WithTargeting_EmitsTargetingStartedAndCompleted()
    {
        CollectingLogger logger = new();
        ConjectureSettings settings = new()
        {
            Logger = logger,
            MaxExamples = 20,
            Seed = 1UL,
            Targeting = true,
            TargetingProportion = 0.5,
        };

        await TestRunner.Run(settings, data =>
        {
            Target.Maximize((double)data.NextInteger(0, 100));
        });

        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Information && e.EventId.Id == 4);
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Information && e.EventId.Id == 5);
    }

    [Fact]
    public async Task Run_HighAssumptionRejection_EmitsHighUnsatisfiedRatioWarning()
    {
        CollectingLogger logger = new();
        ConjectureSettings settings = new()
        {
            Logger = logger,
            MaxExamples = 100,
            MaxUnsatisfiedRatio = 4,
            Seed = 1UL,
        };

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
    public async Task Run_WithNullLogger_PassingPropertyDoesNotThrow()
    {
        ConjectureSettings settings = new() { MaxExamples = 10, Seed = 1UL };
        Assert.Same(NullLogger.Instance, settings.Logger);

        await TestRunner.Run(settings, _ => { });
    }

    [Fact]
    public async Task Run_WithNullLogger_FailingPropertyDoesNotThrow()
    {
        ConjectureSettings settings = new() { MaxExamples = 10, Seed = 1UL };

        TestRunResult result = await TestRunner.Run(settings,
            _ => throw new InvalidOperationException("always fails"));

        Assert.False(result.Passed);
    }
}
