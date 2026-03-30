using System.Reflection;
using Conjecture.Core;
using Conjecture.Core.Generation;
using Conjecture.Core.Internal;
using Conjecture.MSTest;
using Conjecture.MSTest.Internal;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Conjecture.MSTest.Tests;

file sealed class PositiveInts : IStrategyProvider<int>
{
    public Strategy<int> Create() => Gen.Integers(1, int.MaxValue);
}

/// <summary>
/// Integration tests for the MSTest [Property] execution pipeline.
/// [Property]-decorated methods ARE the tests — MSTest discovers them,
/// the attribute runs the property loop via PropertyAttribute.Execute.
/// </summary>
[TestClass]
public class MSTestPropertyExecutionTests
{
    // --- Self-exercising [Property] tests (meaningful once PropertyAttribute.Execute is wired) ---

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
        Assert.IsTrue(x > 0);
    }

    [Property(MaxExamples = 5, Seed = 1)]
    [Example(0, 0)]
    public void ExampleAttribute_ExplicitCaseRunsAlongGenerated(int a, int b)
    {
        _ = a;
        _ = b;
    }
#pragma warning restore IDE0060

    // --- Unit tests for PropertyTestMethodAttribute helper methods ---

    [TestMethod]
    public void ComputeTestId_ReturnsNonNullHash()
    {
        MethodInfo method = typeof(MSTestPropertyExecutionTests)
            .GetMethod(nameof(IntParameter_NoAssertion_Passes))!;

        string hash = PropertyTestMethodAttribute.ComputeTestId(method);

        Assert.IsNotNull(hash);
        Assert.AreNotEqual(string.Empty, hash);
    }

    [TestMethod]
    public void ComputeTestId_SameMethod_ReturnsSameHash()
    {
        MethodInfo method = typeof(MSTestPropertyExecutionTests)
            .GetMethod(nameof(IntParameter_NoAssertion_Passes))!;

        string hash1 = PropertyTestMethodAttribute.ComputeTestId(method);
        string hash2 = PropertyTestMethodAttribute.ComputeTestId(method);

        Assert.AreEqual(hash1, hash2);
    }

    [TestMethod]
    public void ComputeTestId_DifferentMethods_ReturnDifferentHashes()
    {
        MethodInfo method1 = typeof(MSTestPropertyExecutionTests)
            .GetMethod(nameof(IntParameter_NoAssertion_Passes))!;
        MethodInfo method2 = typeof(MSTestPropertyExecutionTests)
            .GetMethod(nameof(AsyncTaskReturn_NoAssertion_Passes))!;

        string hash1 = PropertyTestMethodAttribute.ComputeTestId(method1);
        string hash2 = PropertyTestMethodAttribute.ComputeTestId(method2);

        Assert.AreNotEqual(hash1, hash2);
    }

    // --- Direct TestRunner tests ---

    [TestMethod]
    public async Task FailingPredicate_ProducesFailingResult()
    {
        ConjectureSettings settings = new() { MaxExamples = 10, Seed = 1UL };
        TestRunResult result = await TestRunner.Run(settings, _ =>
            throw new InvalidOperationException("always fails"));

        Assert.IsFalse(result.Passed);
        Assert.IsNotNull(result.Counterexample);
    }

    [TestMethod]
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

        Assert.IsTrue(
            run1.Counterexample!.Select(n => n.Value)
                .SequenceEqual(run2.Counterexample!.Select(n => n.Value)));
    }

    [TestMethod]
    public async Task MaxExamples5_RunsExactly5Times()
    {
        int count = 0;
        ConjectureSettings settings = new() { MaxExamples = 5, Seed = 1UL };
        await TestRunner.Run(settings, _ => count++);
        Assert.AreEqual(5, count);
    }

    // --- BuildFailureMessage tests ---

#pragma warning disable IDE0060
    private static void PropertyWithInt(int x) { }
    private static void PropertyWithIntAndBool(int x, bool flag) { }
#pragma warning restore IDE0060

    private static ParameterInfo[] Params(string methodName) =>
        typeof(MSTestPropertyExecutionTests)
            .GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static)!
            .GetParameters();

    [TestMethod]
    public async Task BuildFailureMessage_IntParam_ContainsParamNameAndValue()
    {
        ConjectureSettings settings = new() { MaxExamples = 50, Seed = 1UL };
        ParameterInfo[] parameters = Params(nameof(PropertyWithInt));

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            int x = Gen.Integers<int>().Next(data);
            if (x > 5) { throw new Exception("fail"); }
        });

        Assert.IsFalse(result.Passed);
        string message = PropertyTestMethodAttribute.BuildFailureMessage(result, parameters);
        Assert.IsTrue(message.Contains("x ="), $"Expected 'x =' in: {message}");
    }

    [TestMethod]
    public async Task BuildFailureMessage_ContainsSeedReproductionLine()
    {
        ConjectureSettings settings = new() { MaxExamples = 50, Seed = 0xDEADBEEFUL };
        ParameterInfo[] parameters = Params(nameof(PropertyWithInt));

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            int x = Gen.Integers<int>().Next(data);
            if (x > 5) { throw new Exception("fail"); }
        });

        Assert.IsFalse(result.Passed);
        string message = PropertyTestMethodAttribute.BuildFailureMessage(result, parameters);
        Assert.IsTrue(
            message.Contains("Reproduce with: [Property(Seed = 0xDEADBEEF)]"),
            $"Expected seed line in: {message}");
    }

    [TestMethod]
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

        Assert.IsFalse(result.Passed);
        string message = PropertyTestMethodAttribute.BuildFailureMessage(result, parameters);
        Assert.IsTrue(message.Contains("x ="), $"Expected 'x =' in: {message}");
        Assert.IsTrue(message.Contains("flag ="), $"Expected 'flag =' in: {message}");
    }
}
