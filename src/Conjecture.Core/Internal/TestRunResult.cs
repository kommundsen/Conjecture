namespace Conjecture.Core.Internal;

internal sealed class TestRunResult
{
    internal bool Passed { get; }
    internal IReadOnlyList<IRNode>? Counterexample { get; }
    internal ulong? Seed { get; }
    internal int ExampleCount { get; }
    internal int ShrinkCount { get; }

    private TestRunResult(bool passed, IReadOnlyList<IRNode>? counterexample, ulong? seed, int exampleCount, int shrinkCount)
    {
        Passed = passed;
        Counterexample = counterexample;
        Seed = seed;
        ExampleCount = exampleCount;
        ShrinkCount = shrinkCount;
    }

    internal static TestRunResult Pass(ulong seed, int exampleCount) => new(true, null, seed, exampleCount, 0);
    internal static TestRunResult Fail(IReadOnlyList<IRNode> counterexample, ulong seed, int exampleCount, int shrinkCount) =>
        new(false, counterexample, seed, exampleCount, shrinkCount);
}
