using System.Reflection;
using Conjecture.Core;
using Conjecture.Core.Internal;
using Conjecture.NUnit;
using NUnit.Framework;

namespace Conjecture.NUnit.Tests;

file sealed class PositiveInts : IStrategyProvider<int>
{
    public Strategy<int> Create() => Generate.Integers(1, int.MaxValue);
}

/// <summary>
/// Integration tests for the NUnit [Property] execution pipeline.
/// [Property]-decorated methods ARE the tests — NUnit discovers them,
/// the attribute runs the property loop.
/// </summary>
[TestFixture]
public class NUnitPropertyExecutionTests
{
    // --- Self-exercising [Property] tests (meaningful once TestCaseHelper is wired) ---

#pragma warning disable IDE0060
    [Property(MaxExamples = 20, Seed = 1)]
    public void IntParameter_NoAssertion_Passes(int x) { }

    [Property(MaxExamples = 20, Seed = 1)]
    public async Task AsyncTaskReturn_NoAssertion_Passes(int x)
    {
        await Task.Yield();
        _ = x;
    }

    [Property(MaxExamples = 10, Seed = 1)]
    public void FromAttribute_PositiveInts_OnlyPositiveValues([From<PositiveInts>] int x)
    {
        Assert.That(x, Is.GreaterThan(0));
    }

    [Property(MaxExamples = 5, Seed = 1)]
    [Example(0, 0)]
    public void ExampleAttribute_ExplicitCaseRunsAlongGenerated(int a, int b)
    {
        _ = a;
        _ = b;
    }
#pragma warning restore IDE0060

    // --- Unit tests for TestCaseHelper helper methods ---

    [Test]
    public void ComputeTestId_ReturnsNonNullHash()
    {
        MethodInfo method = typeof(NUnitPropertyExecutionTests)
            .GetMethod(nameof(IntParameter_NoAssertion_Passes))!;

        string hash = TestCaseHelper.ComputeTestId(method);

        Assert.That(hash, Is.Not.Null);
        Assert.That(hash, Is.Not.Empty);
    }

    [Test]
    public void ComputeTestId_SameMethod_ReturnsSameHash()
    {
        MethodInfo method = typeof(NUnitPropertyExecutionTests)
            .GetMethod(nameof(IntParameter_NoAssertion_Passes))!;

        string hash1 = TestCaseHelper.ComputeTestId(method);
        string hash2 = TestCaseHelper.ComputeTestId(method);

        Assert.That(hash1, Is.EqualTo(hash2));
    }

    [Test]
    public void ComputeTestId_DifferentMethods_ReturnDifferentHashes()
    {
        MethodInfo method1 = typeof(NUnitPropertyExecutionTests)
            .GetMethod(nameof(IntParameter_NoAssertion_Passes))!;
        MethodInfo method2 = typeof(NUnitPropertyExecutionTests)
            .GetMethod(nameof(AsyncTaskReturn_NoAssertion_Passes))!;

        string hash1 = TestCaseHelper.ComputeTestId(method1);
        string hash2 = TestCaseHelper.ComputeTestId(method2);

        Assert.That(hash1, Is.Not.EqualTo(hash2));
    }

    // --- Direct TestRunner tests ---

    [Test]
    public async Task FailingPredicate_ProducesFailingResult()
    {
        ConjectureSettings settings = new() { MaxExamples = 10, Seed = 1UL };
        TestRunResult result = await TestRunner.Run(settings, _ =>
            throw new InvalidOperationException("always fails"));

        Assert.That(result.Passed, Is.False);
        Assert.That(result.Counterexample, Is.Not.Null);
    }

    [Test]
    public async Task SameSeed_ProducesSameCounterexample()
    {
        ConjectureSettings settings = new() { MaxExamples = 50, Seed = 42UL };

        TestRunResult run1 = await TestRunner.Run(settings, data =>
        {
            ulong v = data.NextInteger(0, 100);
            if (v > 70) { throw new Exception("fail"); }
        });

        TestRunResult run2 = await TestRunner.Run(settings, data =>
        {
            ulong v = data.NextInteger(0, 100);
            if (v > 70) { throw new Exception("fail"); }
        });

        Assert.That(
            run1.Counterexample!.Select(n => n.Value),
            Is.EqualTo(run2.Counterexample!.Select(n => n.Value)));
    }

    [Test]
    public async Task MaxExamples5_RunsExactly5Times()
    {
        int count = 0;
        ConjectureSettings settings = new() { MaxExamples = 5, Seed = 1UL };
        await TestRunner.Run(settings, _ => count++);
        Assert.That(count, Is.EqualTo(5));
    }

    // --- BuildFailureMessage tests ---

#pragma warning disable IDE0060
    private static void PropertyWithInt(int x) { }
    private static void PropertyWithIntAndBool(int x, bool flag) { }
#pragma warning restore IDE0060

    private static ParameterInfo[] Params(string methodName) =>
        typeof(NUnitPropertyExecutionTests)
            .GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static)!
            .GetParameters();

    [Test]
    public async Task BuildFailureMessage_IntParam_ContainsParamNameAndValue()
    {
        ConjectureSettings settings = new() { MaxExamples = 50, Seed = 1UL };
        ParameterInfo[] parameters = Params(nameof(PropertyWithInt));

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            int x = Generate.Integers<int>().Generate(data);
            if (x > 5) { throw new Exception("fail"); }
        });

        Assert.That(result.Passed, Is.False);
        string message = TestCaseHelper.BuildFailureMessage(result, parameters);
        Assert.That(message, Does.Contain("x ="));
    }

    [Test]
    public async Task BuildFailureMessage_ContainsSeedReproductionLine()
    {
        ConjectureSettings settings = new() { MaxExamples = 50, Seed = 0xDEADBEEFUL };
        ParameterInfo[] parameters = Params(nameof(PropertyWithInt));

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            int x = Generate.Integers<int>().Generate(data);
            if (x > 5) { throw new Exception("fail"); }
        });

        Assert.That(result.Passed, Is.False);
        string message = TestCaseHelper.BuildFailureMessage(result, parameters);
        Assert.That(message, Does.Contain("Reproduce with: [Property(Seed = 0xDEADBEEF)]"));
    }

    [Test]
    public async Task BuildFailureMessage_MultipleParams_ContainsAllParamNames()
    {
        ConjectureSettings settings = new() { MaxExamples = 50, Seed = 7UL };
        ParameterInfo[] parameters = Params(nameof(PropertyWithIntAndBool));

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            _ = Generate.Integers<int>().Generate(data);
            _ = Generate.Booleans().Generate(data);
            throw new Exception("always fail");
        });

        Assert.That(result.Passed, Is.False);
        string message = TestCaseHelper.BuildFailureMessage(result, parameters);
        Assert.That(message, Does.Contain("x ="));
        Assert.That(message, Does.Contain("flag ="));
    }
}
