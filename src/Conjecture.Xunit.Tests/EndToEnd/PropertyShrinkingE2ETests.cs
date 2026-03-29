using System.Reflection;
using Conjecture.Core;
using Conjecture.Core.Internal;
using Conjecture.Xunit.Internal;

namespace Conjecture.Xunit.Tests.EndToEnd;

/// <summary>
/// End-to-end tests verifying that shrinking, the test runner, and failure
/// message formatting all work together correctly via the full pipeline.
/// </summary>
public class PropertyShrinkingE2ETests
{
#pragma warning disable IDE0060
    private static void IntProperty(int x) { }
    private static void TwoIntProperty(int x, int y) { }
    private static void IntAndBoolProperty(int x, bool flag) { }
#pragma warning restore IDE0060

    private static ParameterInfo[] Params(string methodName) =>
        typeof(PropertyShrinkingE2ETests)
            .GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static)!
            .GetParameters();

    // --- Shrunk value appears in formatted failure message ---

    [Fact]
    public void FailingIntProperty_ShrunkValueAppearsInFailureMessage()
    {
        // Property fails when x > 5; minimal counterexample is x = 6.
        // The formatted message must show the shrunk value, not some large original.
        var settings = new ConjectureSettings { MaxExamples = 100, Seed = 1UL };
        var parameters = Params(nameof(IntProperty));

        var result = TestRunner.Run(settings, data =>
        {
            var args = ParameterStrategyResolver.Resolve(parameters, data);
            if ((int)args[0] > 5) { throw new Exception("fail"); }
        });

        Assert.False(result.Passed);
        var message = PropertyTestCaseRunner.BuildFailureMessage(result, parameters);
        Assert.Contains("x = 6", message);
    }

    [Fact]
    public void FailingIntProperty_FailureMessageContainsReproductionSeed()
    {
        var settings = new ConjectureSettings { MaxExamples = 100, Seed = 42UL };
        var parameters = Params(nameof(IntProperty));

        var result = TestRunner.Run(settings, data =>
        {
            var args = ParameterStrategyResolver.Resolve(parameters, data);
            if ((int)args[0] > 5) { throw new Exception("fail"); }
        });

        Assert.False(result.Passed);
        var message = PropertyTestCaseRunner.BuildFailureMessage(result, parameters);
        Assert.Contains("Reproduce with: [Property(Seed = 0x2A)]", message);
    }

    // --- Multiple parameters ---

    [Fact]
    public void FailingTwoIntProperty_BothParamsShrunkAndInMessage()
    {
        // Property fails when x + y > 10; minimal shrunk pair has x + y == 11.
        var settings = new ConjectureSettings { MaxExamples = 200, Seed = 7UL };
        var parameters = Params(nameof(TwoIntProperty));

        var result = TestRunner.Run(settings, data =>
        {
            var args = ParameterStrategyResolver.Resolve(parameters, data);
            if ((int)args[0] + (int)args[1] > 10) { throw new Exception("fail"); }
        });

        Assert.False(result.Passed);
        var message = PropertyTestCaseRunner.BuildFailureMessage(result, parameters);
        // Both param names must appear in the message on separate lines
        Assert.Contains("x =", message);
        Assert.Contains("y =", message);
        var lines = message.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.True(lines.Length >= 3, $"Expected at least 3 lines (x, y, seed), got: {message}");
        // Reconstruct values from the message to check the sum is minimal (11)
        var replay = ConjectureData.ForRecord(result.Counterexample!);
        var args = ParameterStrategyResolver.Resolve(parameters, replay);
        Assert.Equal(11, (int)args[0] + (int)args[1]);
    }

    // --- Passing property ---

    [Fact]
    public void PassingProperty_RunsMaxExamplesAndResultPassed()
    {
        var count = 0;
        var settings = new ConjectureSettings { MaxExamples = 50, Seed = 1UL };
        var parameters = Params(nameof(IntProperty));

        var result = TestRunner.Run(settings, data =>
        {
            ParameterStrategyResolver.Resolve(parameters, data);
            count++;
        });

        Assert.True(result.Passed);
        Assert.Equal(50, count);
        Assert.Null(result.Counterexample);
    }

    // --- Assume.That filters inputs; shrinking still finds minimal failure ---

    [Fact]
    public void AssumeFiltersInputs_ShrinkingFindsMinimalFailureAboveConstraint()
    {
        // Only consider positive x (Assume.That(x > 0)).
        // Property fails when x > 5; minimal positive x that fails is 6.
        var settings = new ConjectureSettings { MaxExamples = 500, Seed = 3UL };
        var parameters = Params(nameof(IntProperty));

        var result = TestRunner.Run(settings, data =>
        {
            var args = ParameterStrategyResolver.Resolve(parameters, data);
            var x = (int)args[0];
            Assume.That(x > 0);
            if (x > 5) { throw new Exception("fail"); }
        });

        Assert.False(result.Passed);
        // Replay shrunk nodes to confirm value is 6
        var replay = ConjectureData.ForRecord(result.Counterexample!);
        var shrunkArgs = ParameterStrategyResolver.Resolve(parameters, replay);
        Assert.Equal(6, (int)shrunkArgs[0]);
        // And it appears in the message
        var message = PropertyTestCaseRunner.BuildFailureMessage(result, parameters);
        Assert.Contains("x = 6", message);
    }

    [Fact]
    public void AssumeFiltersAllInputs_ResultIsPass()
    {
        // Assume always fails → all examples discarded → treated as pass (no counterexample found).
        var settings = new ConjectureSettings { MaxExamples = 20, Seed = 1UL };

        var result = TestRunner.Run(settings, data =>
        {
            data.DrawInteger(0, 100);
            Assume.That(false);
        });

        Assert.True(result.Passed);
    }
}
