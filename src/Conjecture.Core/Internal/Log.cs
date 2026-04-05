// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Microsoft.Extensions.Logging;

namespace Conjecture.Core.Internal;

internal static partial class Log
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Information,
        Message = "Generation complete: valid={Valid}, unsatisfied={Unsatisfied}, elapsed={DurationMs}ms")]
    internal static partial void GenerationCompleted(ILogger logger, int valid, int unsatisfied, double durationMs);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information,
        Message = "Shrinking started: {NodeCount} nodes")]
    internal static partial void ShrinkingStarted(ILogger logger, int nodeCount);

    [LoggerMessage(EventId = 3, Level = LogLevel.Information,
        Message = "Shrinking complete: {NodeCount} nodes, {ShrinkCount} steps, elapsed={DurationMs}ms")]
    internal static partial void ShrinkingCompleted(ILogger logger, int nodeCount, int shrinkCount, double durationMs);

    [LoggerMessage(EventId = 4, Level = LogLevel.Information,
        Message = "Targeting started: labels={Labels}")]
    internal static partial void TargetingStarted(ILogger logger, string labels);

    [LoggerMessage(EventId = 5, Level = LogLevel.Information,
        Message = "Targeting complete: labels={Labels}, best={BestScores}")]
    internal static partial void TargetingCompleted(ILogger logger, string labels, string bestScores);

    [LoggerMessage(EventId = 6, Level = LogLevel.Information,
        Message = "Replaying {BufferCount} stored example(s) from database")]
    internal static partial void DatabaseReplaying(ILogger logger, int bufferCount);

    [LoggerMessage(EventId = 7, Level = LogLevel.Warning,
        Message = "High assumption rejection: {Unsatisfied} unsatisfied vs {Valid} valid (limit={Limit})")]
    internal static partial void HighUnsatisfiedRatio(ILogger logger, int unsatisfied, int valid, int limit);

    [LoggerMessage(EventId = 8, Level = LogLevel.Warning,
        Message = "Database error: {ErrorMessage}")]
    internal static partial void DatabaseError(ILogger logger, string errorMessage, Exception exception);

    [LoggerMessage(EventId = 9, Level = LogLevel.Error,
        Message = "Property test failed after {ExampleCount} example(s), seed={Seed}")]
    internal static partial void PropertyTestFailure(ILogger logger, int exampleCount, string seed);

    [LoggerMessage(EventId = 10, Level = LogLevel.Debug,
        Message = "Shrink pass {PassName}: progress={MadeProgress}")]
    internal static partial void ShrinkPassProgress(ILogger logger, string passName, bool madeProgress);

    [LoggerMessage(EventId = 11, Level = LogLevel.Debug,
        Message = "Database saved: testId={TestIdHash}")]
    internal static partial void DatabaseSaved(ILogger logger, string testIdHash);

    [LoggerMessage(EventId = 12, Level = LogLevel.Debug,
        Message = "Targeting step: label={Label} improved to score={NewScore}")]
    internal static partial void TargetingStepImproved(ILogger logger, string label, double newScore);
}
