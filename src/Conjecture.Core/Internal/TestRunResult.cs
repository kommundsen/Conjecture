// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

namespace Conjecture.Core.Internal;

internal sealed class TestRunResult
{
    internal bool Passed { get; }
    internal IReadOnlyList<IRNode>? Counterexample { get; }
    internal IReadOnlyList<IRNode>? OriginalCounterexample { get; }
    internal ulong? Seed { get; }
    internal int ExampleCount { get; }
    internal int ShrinkCount { get; }
    internal string? FailureStackTrace { get; }
    internal IReadOnlyDictionary<string, double>? TargetingScores { get; }

    private TestRunResult(bool passed, IReadOnlyList<IRNode>? counterexample, IReadOnlyList<IRNode>? originalCounterexample, ulong? seed, int exampleCount, int shrinkCount, string? failureStackTrace, IReadOnlyDictionary<string, double>? targetingScores = null)
    {
        Passed = passed;
        Counterexample = counterexample;
        OriginalCounterexample = originalCounterexample;
        Seed = seed;
        ExampleCount = exampleCount;
        ShrinkCount = shrinkCount;
        FailureStackTrace = failureStackTrace;
        TargetingScores = targetingScores;
    }

    internal static TestRunResult Pass(ulong seed, int exampleCount, IReadOnlyDictionary<string, double>? targetingScores = null) =>
        new(true, null, null, seed, exampleCount, 0, null, targetingScores);
    internal static TestRunResult Fail(IReadOnlyList<IRNode> counterexample, IReadOnlyList<IRNode> originalCounterexample, ulong seed, int exampleCount, int shrinkCount, string? failureStackTrace = null) =>
        new(false, counterexample, originalCounterexample, seed, exampleCount, shrinkCount, failureStackTrace);
    internal static TestRunResult WithExtraExamples(TestRunResult result, int extraCount) =>
        new(result.Passed, result.Counterexample, result.OriginalCounterexample, result.Seed, result.ExampleCount + extraCount, result.ShrinkCount, result.FailureStackTrace, result.TargetingScores);
}