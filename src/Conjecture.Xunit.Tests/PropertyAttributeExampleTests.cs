// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Reflection;
using Conjecture.Core;
using Conjecture.Core.Internal;
using Conjecture.Xunit;


namespace Conjecture.Xunit.Tests;

/// <summary>
/// Integration tests for [Example] + [Property] wiring.
/// The [Property]-decorated methods below ARE tests — xUnit discovers and executes them.
/// </summary>
public class PropertyAttributeExampleTests
{
    // ── Happy-path: explicit examples run, generated still run ──────────────

#pragma warning disable IDE0060
    [Example(0, 0)]
    [Property(MaxExamples = 5, Seed = 1UL)]
    public void TwoInts_SingleExample_BothExplicitAndGeneratedPass(int x, int y) { }

    [Example(1, 2)]
    [Example(3, 4)]
    [Property(MaxExamples = 3, Seed = 1UL)]
    public void TwoInts_MultipleExamples_AllExecuteWithoutError(int x, int y) { }
#pragma warning restore IDE0060

    // ── Failure message for explicit example ────────────────────────────────

    [Fact]
    public void BuildExampleFailureMessage_ContainsParamNamesAndArgValues()
    {
        ParameterInfo[] parameters = GetParams(nameof(TwoIntHelper));
        ExampleAttribute example = new(3, 7);
        Exception failure = new InvalidOperationException("bad value");

        string message = TestCaseHelper.BuildExampleFailureMessage(example, parameters, failure);

        Assert.Contains("x =", message);
        Assert.Contains("y =", message);
        Assert.Contains("3", message);
        Assert.Contains("7", message);
    }

    [Fact]
    public void BuildExampleFailureMessage_IndicatesExplicitExample()
    {
        ParameterInfo[] parameters = GetParams(nameof(TwoIntHelper));
        ExampleAttribute example = new(0, 0);
        Exception failure = new("fail");

        string message = TestCaseHelper.BuildExampleFailureMessage(example, parameters, failure);

        Assert.Contains("explicit", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildExampleFailureMessage_ContainsInnerExceptionMessage()
    {
        ParameterInfo[] parameters = GetParams(nameof(TwoIntHelper));
        ExampleAttribute example = new(1, 2);
        Exception failure = new InvalidOperationException("specific failure reason");

        string message = TestCaseHelper.BuildExampleFailureMessage(example, parameters, failure);

        Assert.Contains("specific failure reason", message);
    }

    // ── Wrong arg count throws clear error ───────────────────────────────────

    [Fact]
    public void ValidateExampleArgs_WrongCount_ThrowsArgumentException()
    {
        ParameterInfo[] twoParams = GetParams(nameof(TwoIntHelper));
        ExampleAttribute wrongCount = new(1); // 1 arg for 2-param method

        ArgumentException ex = Assert.Throws<ArgumentException>(
            () => TestCaseHelper.ValidateExampleArgs(wrongCount, twoParams));

        Assert.Contains("2", ex.Message);
    }

    [Fact]
    public void ValidateExampleArgs_CorrectCount_DoesNotThrow()
    {
        ParameterInfo[] twoParams = GetParams(nameof(TwoIntHelper));
        ExampleAttribute correct = new(1, 2);

        // Should not throw
        TestCaseHelper.ValidateExampleArgs(correct, twoParams);
    }

    [Fact]
    public void ValidateExampleArgs_ExtraArgs_ThrowsArgumentException()
    {
        ParameterInfo[] twoParams = GetParams(nameof(TwoIntHelper));
        ExampleAttribute tooMany = new(1, 2, 3); // 3 args for 2-param method

        Assert.Throws<ArgumentException>(
            () => TestCaseHelper.ValidateExampleArgs(tooMany, twoParams));
    }

    // ── Explicit examples contribute to ExampleCount ─────────────────────────

    [Fact]
    public async Task ExplicitExamples_AddedToExampleCount_InFailureResult()
    {
        // When 2 explicit examples pass and a generated one fails:
        // ExampleCount in the failure result = 2 (explicit) + 1 (generated that failed) = 3
        ConjectureSettings settings = new() { MaxExamples = 100, Seed = 1UL };

        TestRunResult generated = await TestRunner.Run(settings, _ =>
            throw new Exception("always fails"));

        // Simulate: 2 explicit examples ran before the generated run
        TestRunResult withExplicit = TestRunResult.WithExtraExamples(generated, extraCount: 2);

        Assert.False(withExplicit.Passed);
        Assert.Equal(generated.ExampleCount + 2, withExplicit.ExampleCount);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static ParameterInfo[] GetParams(string methodName) =>
        typeof(PropertyAttributeExampleTests)
            .GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static)!
            .GetParameters();

#pragma warning disable IDE0060
    private static void TwoIntHelper(int x, int y) { }
#pragma warning restore IDE0060
}