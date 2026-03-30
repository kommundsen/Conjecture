using System.Reflection;
using Conjecture.Core;
using Conjecture.Core.Generation;
using Conjecture.Core.Internal;
using Conjecture.Xunit.V3;
using Conjecture.Xunit.V3.Internal;
using Xunit;

namespace Conjecture.Xunit.V3.Tests;

file sealed class PositiveInts : IStrategyProvider<int>
{
    public Strategy<int> Create() => Gen.Integers(1, int.MaxValue);
}

/// <summary>
/// Integration tests for the xUnit v3 [Property] execution pipeline.
/// [Property]-decorated methods below ARE the tests — xUnit v3 discovers them,
/// the attribute resolves parameter strategies, and the engine runs them.
/// </summary>
public class XunitV3PropertyExecutionTests
{
    // --- xUnit v3 discovery: [Property] methods that should always pass ---

#pragma warning disable IDE0060
    [Property(MaxExamples = 20, Seed = 1)]
    public void IntParameter_NoAssertion_Passes(int x) { }

    [Property(MaxExamples = 20, Seed = 1)]
    public void BoolParameter_NoAssertion_Passes(bool b) { }

    [Property(MaxExamples = 20, Seed = 1)]
    public async Task AsyncTaskReturn_NoAssertion_Passes(int x)
    {
        await Task.Yield();
        _ = x;
    }

    [Property(MaxExamples = 10, Seed = 1)]
    public void FromAttribute_PositiveInts_OnlyPositiveValues([From<PositiveInts>] int x)
    {
        Assert.True(x > 0);
    }

    [Property(MaxExamples = 5, Seed = 1)]
    [Example(0, 0)]
    public void ExampleAttribute_ExplicitCaseRunsAlongGenerated(int a, int b)
    {
        _ = a;
        _ = b;
    }
#pragma warning restore IDE0060

    // --- Direct TestRunner unit tests ---

    [Fact]
    public async Task FailingPredicate_ProducesFailingResult()
    {
        ConjectureSettings settings = new() { MaxExamples = 10, Seed = 1UL };
        TestRunResult result = await TestRunner.Run(settings, _ =>
            throw new InvalidOperationException("always fails"));

        Assert.False(result.Passed);
        Assert.NotNull(result.Counterexample);
    }

    [Fact]
    public async Task SameSeed_ProducesSameCounterexample()
    {
        ConjectureSettings settings = new() { MaxExamples = 50, Seed = 42UL };

        TestRunResult run1 = await TestRunner.Run(settings, data =>
        {
            ulong v = data.DrawInteger(0, 100);
            if (v > 70) { throw new Exception("fail"); }
        });

        TestRunResult run2 = await TestRunner.Run(settings, data =>
        {
            ulong v = data.DrawInteger(0, 100);
            if (v > 70) { throw new Exception("fail"); }
        });

        Assert.Equal(
            run1.Counterexample!.Select(n => n.Value),
            run2.Counterexample!.Select(n => n.Value));
    }

    [Fact]
    public async Task MaxExamples5_RunsExactly5Times()
    {
        int count = 0;
        ConjectureSettings settings = new() { MaxExamples = 5, Seed = 1UL };
        await TestRunner.Run(settings, _ => count++);
        Assert.Equal(5, count);
    }

    // --- BuildFailureMessage tests ---
    // These verify that PropertyTestCaseRunner.BuildFailureMessage formats the output correctly.
    // The test lambdas draw integers the same way the resolver does for int params (Gen.Integers<int>()),
    // so BuildFailureMessage can replay the counterexample through the resolver.

#pragma warning disable IDE0060
    private static void PropertyWithInt(int x) { }
    private static void PropertyWithIntAndBool(int x, bool flag) { }
#pragma warning restore IDE0060

    private static ParameterInfo[] Params(string methodName) =>
        typeof(XunitV3PropertyExecutionTests)
            .GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static)!
            .GetParameters();

    [Fact]
    public async Task BuildFailureMessage_IntParam_ContainsParamNameAndValue()
    {
        ConjectureSettings settings = new() { MaxExamples = 50, Seed = 1UL };
        ParameterInfo[] parameters = Params(nameof(PropertyWithInt));

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            int x = Gen.Integers<int>().Next(data);
            if (x > 5) { throw new Exception("fail"); }
        });

        Assert.False(result.Passed);
        string message = PropertyTestCaseRunner.BuildFailureMessage(result, parameters);
        Assert.Contains("x =", message);
    }

    [Fact]
    public async Task BuildFailureMessage_ContainsSeedReproductionLine()
    {
        ConjectureSettings settings = new() { MaxExamples = 50, Seed = 0xDEADBEEFUL };
        ParameterInfo[] parameters = Params(nameof(PropertyWithInt));

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            int x = Gen.Integers<int>().Next(data);
            if (x > 5) { throw new Exception("fail"); }
        });

        Assert.False(result.Passed);
        string message = PropertyTestCaseRunner.BuildFailureMessage(result, parameters);
        Assert.Contains("Reproduce with: [Property(Seed = 0xDEADBEEF)]", message);
    }

    [Fact]
    public async Task BuildFailureMessage_MultipleParams_ContainsAllParamNames()
    {
        ConjectureSettings settings = new() { MaxExamples = 50, Seed = 7UL };
        ParameterInfo[] parameters = Params(nameof(PropertyWithIntAndBool));

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            _ = Gen.Integers<int>().Next(data);
            _ = Gen.Booleans().Next(data);
            throw new Exception("always fail");
        });

        Assert.False(result.Passed);
        string message = PropertyTestCaseRunner.BuildFailureMessage(result, parameters);
        Assert.Contains("x =", message);
        Assert.Contains("flag =", message);
    }
}
