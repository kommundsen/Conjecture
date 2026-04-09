// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core.Internal;

using Microsoft.Extensions.Logging;

namespace Conjecture.Core.Tests.Internal;

public class HillClimberLoggingTests
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

    private static IReadOnlyList<IRNode> SingleIntNode(ulong value) =>
        [IRNode.ForInteger(value, 0UL, 100UL)];

    private static Task<(Status, IReadOnlyDictionary<string, double>)> EvalByValue(
        IReadOnlyList<IRNode> nodes)
    {
        double score = (double)nodes[0].Value;
        return Task.FromResult<(Status, IReadOnlyDictionary<string, double>)>(
            (Status.Valid, new Dictionary<string, double> { ["score"] = score }));
    }

    [Fact]
    public async Task Climb_WhenScoreImproves_LogsTargetingStepAtDebug()
    {
        CollectingLogger logger = new();

        await HillClimber.Climb(SingleIntNode(5UL), 5.0, "score", EvalByValue, budget: 4, logger: logger);

        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Debug && e.EventId.Id == 12);
    }

    [Fact]
    public async Task Climb_WithNullLogger_DoesNotThrow()
    {
        (IReadOnlyList<IRNode> result, double finalScore) =
            await HillClimber.Climb(SingleIntNode(5UL), 5.0, "score", EvalByValue, budget: 4);

        Assert.True(finalScore >= 5.0);
    }

    [Fact]
    public async Task Climb_WhenNoImprovement_DoesNotLogTargetingStep()
    {
        CollectingLogger logger = new();
        // Score always stays at 0 — no improvement possible
        static Task<(Status, IReadOnlyDictionary<string, double>)> NoImprove(IReadOnlyList<IRNode> _) =>
            Task.FromResult<(Status, IReadOnlyDictionary<string, double>)>(
                (Status.Valid, new Dictionary<string, double> { ["score"] = 0.0 }));

        await HillClimber.Climb(SingleIntNode(50UL), 1.0, "score", NoImprove, budget: 4, logger: logger);

        Assert.DoesNotContain(logger.Entries, e => e.EventId.Id == 12);
    }
}