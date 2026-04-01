using System.Reflection;
using Conjecture.Core;
using Conjecture.Core.Internal;


namespace Conjecture.Xunit.Tests;

/// <summary>
/// Tests that [Property] failure messages include formatted parameter values and the seed.
/// Drives: TestRunResult.Seed, TestCaseHelper.BuildFailureMessage.
/// </summary>
public class PropertyAttributeFailureMessageTests
{
    // Dummy method used to get ParameterInfo for int parameter named "x".
#pragma warning disable IDE0060
    private static void PropertyWithInt(int x) { }
    private static void PropertyWithIntAndBool(int x, bool flag) { }
#pragma warning restore IDE0060

    private static ParameterInfo[] Params(string methodName) =>
        typeof(PropertyAttributeFailureMessageTests)
            .GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static)!
            .GetParameters();

    [Fact]
    public async Task Run_WithSeed_ResultCarriesThatSeed()
    {
        ConjectureSettings settings = new() { MaxExamples = 10, Seed = 42UL };

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            data.NextInteger(0, 100);
            throw new Exception("fail");
        });

        Assert.Equal(42UL, result.Seed);
    }

    [Fact]
    public async Task Run_WithNoSeed_ResultCarriesNonNullSeed()
    {
        ConjectureSettings settings = new() { MaxExamples = 10 };

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            data.NextInteger(0, 100);
            throw new Exception("fail");
        });

        Assert.NotNull(result.Seed);
    }

    [Fact]
    public async Task BuildFailureMessage_IntParam_ContainsParamNameAndValue()
    {
        ConjectureSettings settings = new() { MaxExamples = 50, Seed = 1UL };
        ParameterInfo[] parameters = Params(nameof(PropertyWithInt));

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            object[] args = SharedParameterStrategyResolver.Resolve(parameters, data);
            if ((int)args[0] > 5) { throw new Exception("fail"); }
        });

        Assert.False(result.Passed);
        string message = TestCaseHelper.BuildFailureMessage(result, parameters);
        Assert.Contains("x =", message);
    }

    [Fact]
    public async Task BuildFailureMessage_ContainsSeedReproductionLine()
    {
        ConjectureSettings settings = new() { MaxExamples = 50, Seed = 0xDEADBEEFUL };
        ParameterInfo[] parameters = Params(nameof(PropertyWithInt));

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            object[] args = SharedParameterStrategyResolver.Resolve(parameters, data);
            if ((int)args[0] > 5) { throw new Exception("fail"); }
        });

        Assert.False(result.Passed);
        string message = TestCaseHelper.BuildFailureMessage(result, parameters);
        Assert.Contains("Reproduce with: [Property(Seed = 0xDEADBEEF)]", message);
    }

    [Fact]
    public async Task BuildFailureMessage_MultipleParams_ContainsAllParamNames()
    {
        ConjectureSettings settings = new() { MaxExamples = 50, Seed = 7UL };
        ParameterInfo[] parameters = Params(nameof(PropertyWithIntAndBool));

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            object[] args = SharedParameterStrategyResolver.Resolve(parameters, data);
            throw new Exception("always fail");
        });

        Assert.False(result.Passed);
        string message = TestCaseHelper.BuildFailureMessage(result, parameters);
        Assert.Contains("x =", message);
        Assert.Contains("flag =", message);
    }

    [Fact]
    public async Task Run_PassingProperty_SeedIsStillCarried()
    {
        ConjectureSettings settings = new() { MaxExamples = 5, Seed = 99UL };

        TestRunResult result = await TestRunner.Run(settings, _ => { });

        Assert.True(result.Passed);
        Assert.Equal(99UL, result.Seed);
    }
}
