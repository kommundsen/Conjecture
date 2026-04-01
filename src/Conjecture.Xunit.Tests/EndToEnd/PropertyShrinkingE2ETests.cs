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
    public async Task FailingIntProperty_ShrunkValueAppearsInFailureMessage()
    {
        // Property fails when x > 5; minimal counterexample is x = 6.
        // The formatted message must show the shrunk value, not some large original.
        ConjectureSettings settings = new() { MaxExamples = 100, Seed = 1UL };
        ParameterInfo[] parameters = Params(nameof(IntProperty));

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            object[] args = ParameterStrategyResolver.Resolve(parameters, data);
            if ((int)args[0] > 5) { throw new Exception("fail"); }
        });

        Assert.False(result.Passed);
        string message = PropertyTestCaseRunner.BuildFailureMessage(result, parameters);
        Assert.Contains("x = 6", message);
    }

    [Fact]
    public async Task FailingIntProperty_FailureMessageContainsReproductionSeed()
    {
        ConjectureSettings settings = new() { MaxExamples = 100, Seed = 42UL };
        ParameterInfo[] parameters = Params(nameof(IntProperty));

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            object[] args = ParameterStrategyResolver.Resolve(parameters, data);
            if ((int)args[0] > 5) { throw new Exception("fail"); }
        });

        Assert.False(result.Passed);
        string message = PropertyTestCaseRunner.BuildFailureMessage(result, parameters);
        Assert.Contains("Reproduce with: [Property(Seed = 0x2A)]", message);
    }

    // --- Multiple parameters ---

    [Fact]
    public async Task FailingTwoIntProperty_BothParamsShrunkAndInMessage()
    {
        // Property fails when x + y > 10; minimal shrunk pair has x + y == 11.
        ConjectureSettings settings = new() { MaxExamples = 200, Seed = 7UL };
        ParameterInfo[] parameters = Params(nameof(TwoIntProperty));

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            object[] args = ParameterStrategyResolver.Resolve(parameters, data);
            if ((int)args[0] + (int)args[1] > 10) { throw new Exception("fail"); }
        });

        Assert.False(result.Passed);
        string message = PropertyTestCaseRunner.BuildFailureMessage(result, parameters);
        // Both param names must appear in the message on separate lines
        Assert.Contains("x =", message);
        Assert.Contains("y =", message);
        string[] lines = message.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.True(lines.Length >= 3, $"Expected at least 3 lines (x, y, seed), got: {message}");
        // Reconstruct values from the message to check the sum is minimal (11)
        ConjectureData replay = ConjectureData.ForRecord(result.Counterexample!);
        object[] args = ParameterStrategyResolver.Resolve(parameters, replay);
        Assert.Equal(11, (int)args[0] + (int)args[1]);
    }

    // --- Passing property ---

    [Fact]
    public async Task PassingProperty_RunsMaxExamplesAndResultPassed()
    {
        int count = 0;
        ConjectureSettings settings = new() { MaxExamples = 50, Seed = 1UL };
        ParameterInfo[] parameters = Params(nameof(IntProperty));

        TestRunResult result = await TestRunner.Run(settings, data =>
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
    public async Task AssumeFiltersInputs_ShrinkingFindsMinimalFailureAboveConstraint()
    {
        // Only consider positive x (Assume.That(x > 0)).
        // Property fails when x > 5; minimal positive x that fails is 6.
        ConjectureSettings settings = new() { MaxExamples = 500, Seed = 3UL };
        ParameterInfo[] parameters = Params(nameof(IntProperty));

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            object[] args = ParameterStrategyResolver.Resolve(parameters, data);
            int x = (int)args[0];
            Assume.That(x > 0);
            if (x > 5) { throw new Exception("fail"); }
        });

        Assert.False(result.Passed);
        // Replay shrunk nodes to confirm value is 6
        ConjectureData replay = ConjectureData.ForRecord(result.Counterexample!);
        object[] shrunkArgs = ParameterStrategyResolver.Resolve(parameters, replay);
        Assert.Equal(6, (int)shrunkArgs[0]);
        // And it appears in the message
        string message = PropertyTestCaseRunner.BuildFailureMessage(result, parameters);
        Assert.Contains("x = 6", message);
    }

    [Fact]
    public async Task AssumeFiltersAllInputs_ResultIsPass()
    {
        // Assume always fails → all examples discarded → treated as pass (no counterexample found).
        ConjectureSettings settings = new() { MaxExamples = 20, Seed = 1UL };

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            data.NextInteger(0, 100);
            Assume.That(false);
        });

        Assert.True(result.Passed);
    }
}
