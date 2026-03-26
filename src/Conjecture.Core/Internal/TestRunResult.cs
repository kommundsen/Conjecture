namespace Conjecture.Core.Internal;

internal sealed class TestRunResult
{
    internal bool Passed { get; }
    internal IReadOnlyList<IRNode>? Counterexample { get; }
    internal ulong? Seed { get; }

    private TestRunResult(bool passed, IReadOnlyList<IRNode>? counterexample, ulong? seed)
    {
        Passed = passed;
        Counterexample = counterexample;
        Seed = seed;
    }

    internal static TestRunResult Pass(ulong seed) => new(true, null, seed);
    internal static TestRunResult Fail(IReadOnlyList<IRNode> counterexample, ulong seed) => new(false, counterexample, seed);
}
