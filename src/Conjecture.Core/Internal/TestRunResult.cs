namespace Conjecture.Core.Internal;

internal sealed class TestRunResult
{
    internal bool Passed { get; }
    internal IReadOnlyList<IRNode>? Counterexample { get; }

    private TestRunResult(bool passed, IReadOnlyList<IRNode>? counterexample)
    {
        Passed = passed;
        Counterexample = counterexample;
    }

    internal static TestRunResult Pass() => new(true, null);
    internal static TestRunResult Fail(IReadOnlyList<IRNode> counterexample) => new(false, counterexample);
}
