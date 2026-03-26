using Conjecture.Core;
using Conjecture.Core.Internal;
using Conjecture.Xunit;

namespace Conjecture.Tests;

/// <summary>
/// Integration tests for [Property]. The [Property]-decorated methods below ARE the tests —
/// xUnit discovers them, the attribute resolves parameter strategies, and the engine runs them.
/// </summary>
public class PropertyAttributeTests
{
    // --- xUnit discovery: [Property] methods that should always pass ---

    [Property(MaxExamples = 20, Seed = 1UL)]
    public void IntParameter_NoAssertion_Passes(int x) { }

    [Property(MaxExamples = 20, Seed = 1UL)]
    public void BoolParameter_NoAssertion_Passes(bool b) { }

    [Property(MaxExamples = 20, Seed = 1UL)]
    public void MultipleParameters_IntAndBool_Passes(int x, bool b) { }

    // --- Failure: failing property must throw so xUnit reports it as failed.
    //     We can't put a known-failing [Property] in the test suite, so we verify
    //     via TestRunner directly that a counterexample is produced. ---

    [Fact]
    public void Property_FailingPredicate_ReturnsFailingResult()
    {
        var settings = new ConjectureSettings { MaxExamples = 10, Seed = 1UL };
        var result = TestRunner.Run(settings, _ =>
            throw new InvalidOperationException("always fails"));

        Assert.False(result.Passed);
        Assert.NotNull(result.Counterexample);
    }

    // --- Seed: attribute Seed property flows into ConjectureSettings ---

    [Fact]
    public void Property_SameSeedAttribute_ProducesSameCounterexample()
    {
        // Simulate two runs of [Property(Seed = 42)] with the same failing predicate
        var settings = new ConjectureSettings { MaxExamples = 50, Seed = 42UL };

        var run1 = TestRunner.Run(settings, data =>
        {
            var v = data.DrawInteger(0, 100);
            if (v > 70) throw new Exception("fail");
        });

        var run2 = TestRunner.Run(settings, data =>
        {
            var v = data.DrawInteger(0, 100);
            if (v > 70) throw new Exception("fail");
        });

        Assert.Equal(
            run1.Counterexample!.Select(n => n.Value),
            run2.Counterexample!.Select(n => n.Value));
    }

    // --- MaxExamples: attribute MaxExamples property flows into ConjectureSettings ---

    [Fact]
    public void Property_MaxExamples5_RunsExactly5Times()
    {
        var count = 0;
        var settings = new ConjectureSettings { MaxExamples = 5, Seed = 1UL };
        TestRunner.Run(settings, _ => count++);
        Assert.Equal(5, count);
    }
}
