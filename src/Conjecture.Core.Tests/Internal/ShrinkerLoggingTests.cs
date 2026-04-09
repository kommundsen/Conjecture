// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core.Internal;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Conjecture.Core.Tests.Internal;

public class ShrinkerLoggingTests
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

    private static IReadOnlyList<IRNode> OneIntegerNode() =>
        [IRNode.ForInteger(42UL, 0UL, 100UL)];

    [Fact]
    public async Task ShrinkAsync_LogsShrinkingStarted_AtInformation()
    {
        CollectingLogger logger = new();
        IReadOnlyList<IRNode> nodes = OneIntegerNode();

        await Conjecture.Core.Internal.Shrinker.ShrinkAsync(nodes, _ => ValueTask.FromResult(Status.Valid), logger);

        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Information && e.EventId.Id == 2);
    }

    [Fact]
    public async Task ShrinkAsync_LogsShrinkingCompleted_AtInformation()
    {
        CollectingLogger logger = new();
        IReadOnlyList<IRNode> nodes = OneIntegerNode();

        await Conjecture.Core.Internal.Shrinker.ShrinkAsync(nodes, _ => ValueTask.FromResult(Status.Valid), logger);

        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Information && e.EventId.Id == 3);
    }

    [Fact]
    public async Task ShrinkAsync_LogsShrinkPassProgress_AtDebug()
    {
        CollectingLogger logger = new();
        IReadOnlyList<IRNode> nodes = OneIntegerNode();

        await Conjecture.Core.Internal.Shrinker.ShrinkAsync(nodes, _ => ValueTask.FromResult(Status.Valid), logger);

        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Debug && e.EventId.Id == 10);
    }

    [Fact]
    public async Task ShrinkAsync_WithNullLogger_DoesNotThrow()
    {
        IReadOnlyList<IRNode> nodes = OneIntegerNode();

        (IReadOnlyList<IRNode> shrunk, int shrinkCount) = await Conjecture.Core.Internal.Shrinker.ShrinkAsync(
            nodes, _ => ValueTask.FromResult(Status.Valid), NullLogger.Instance);

        Assert.Equal(nodes.Count, shrunk.Count);
        Assert.Equal(0, shrinkCount);
    }
}