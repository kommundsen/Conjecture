// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Reflection;

using Conjecture.Core;
using Conjecture.Core.Internal;


namespace Conjecture.Xunit.Tests.EndToEnd;

/// <summary>
/// End-to-end tests verifying that [Property] with string parameters
/// runs, shrinks to minimal length, and produces formatted failure messages.
/// </summary>
public class StringPropertyE2ETests
{
    // [Property]-decorated method xUnit discovers directly — must pass.
#pragma warning disable IDE0060
    [Property(MaxExamples = 20, Seed = 1UL)]
    public void StringParameter_NoAssertion_Passes(string s) { }

    private static void StringMethod(string s) { }
#pragma warning restore IDE0060

    private static ParameterInfo[] Params(string methodName) =>
        typeof(StringPropertyE2ETests)
            .GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static)!
            .GetParameters();

    // --- [Property] with string param runs MaxExamples without exception ---

    [Fact]
    public async Task StringParameter_PassingProperty_RunsMaxExamplesWithoutException()
    {
        int count = 0;
        ConjectureSettings settings = new() { MaxExamples = 50, Seed = 1UL };
        ParameterInfo[] parameters = Params(nameof(StringMethod));

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            SharedParameterStrategyResolver.Resolve(parameters, data);
            count++;
        });

        Assert.True(result.Passed);
        Assert.Equal(50, count);
        Assert.Null(result.Counterexample);
    }

    // --- Failing string property shrinks to minimal string (length exactly 4) ---

    [Fact]
    public async Task StringParameter_FailsWhenLengthOver3_ShrinksTolength4()
    {
        // Property fails when string.Length > 3.
        // Shrinker must find a minimal counterexample of exactly length 4.
        ConjectureSettings settings = new() { MaxExamples = 200, Seed = 1UL };
        ParameterInfo[] parameters = Params(nameof(StringMethod));

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            object[] args = SharedParameterStrategyResolver.Resolve(parameters, data);
            string s = (string)args[0];
            if (s.Length > 3) { throw new Exception("too long"); }
        });

        Assert.False(result.Passed);
        ConjectureData replay = ConjectureData.ForRecord(result.Counterexample!);
        object[] shrunkArgs = SharedParameterStrategyResolver.Resolve(parameters, replay);
        string shrunk = (string)shrunkArgs[0];
        Assert.Equal(4, shrunk.Length);
    }

    [Fact]
    public async Task StringParameter_ShrunkCounterexample_StillSatisfiesFailureCondition()
    {
        ConjectureSettings settings = new() { MaxExamples = 200, Seed = 3UL };
        ParameterInfo[] parameters = Params(nameof(StringMethod));

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            object[] args = SharedParameterStrategyResolver.Resolve(parameters, data);
            string s = (string)args[0];
            if (s.Length > 3) { throw new Exception("too long"); }
        });

        Assert.False(result.Passed);
        ConjectureData replay = ConjectureData.ForRecord(result.Counterexample!);
        object[] shrunkArgs = SharedParameterStrategyResolver.Resolve(parameters, replay);
        string shrunk = (string)shrunkArgs[0];
        Assert.True(shrunk.Length > 3, $"Replayed shrunk string '{shrunk}' does not trigger the failure condition");
    }

    // --- Formatted failure output shows string value in double quotes ---

    [Fact]
    public async Task StringParameter_FailureMessage_ShowsStringInDoubleQuotes()
    {
        ConjectureSettings settings = new() { MaxExamples = 200, Seed = 1UL };
        ParameterInfo[] parameters = Params(nameof(StringMethod));

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            object[] args = SharedParameterStrategyResolver.Resolve(parameters, data);
            string s = (string)args[0];
            if (s.Length > 3) { throw new Exception("too long"); }
        });

        Assert.False(result.Passed);
        string message = TestCaseHelper.BuildFailureMessage(result, parameters);
        Assert.Contains("s = \"", message);
    }

    [Fact]
    public async Task StringParameter_FailureMessage_ContainsParamNameAndSeed()
    {
        ConjectureSettings settings = new() { MaxExamples = 200, Seed = 7UL };
        ParameterInfo[] parameters = Params(nameof(StringMethod));

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            object[] args = SharedParameterStrategyResolver.Resolve(parameters, data);
            string s = (string)args[0];
            if (s.Length > 3) { throw new Exception("too long"); }
        });

        Assert.False(result.Passed);
        string message = TestCaseHelper.BuildFailureMessage(result, parameters);
        Assert.Contains("s =", message);
        Assert.Contains("Reproduce with: [Property(Seed = 0x7)]", message);
    }
}