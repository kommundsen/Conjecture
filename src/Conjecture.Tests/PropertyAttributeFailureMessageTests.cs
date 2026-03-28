using System.Reflection;
using Conjecture.Core;
using Conjecture.Core.Internal;
using Conjecture.Xunit.Internal;

namespace Conjecture.Tests;

/// <summary>
/// Tests that [Property] failure messages include formatted parameter values and the seed.
/// Drives: TestRunResult.Seed, PropertyTestCaseRunner.BuildFailureMessage.
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
    public void Run_WithSeed_ResultCarriesThatSeed()
    {
        var settings = new ConjectureSettings { MaxExamples = 10, Seed = 42UL };

        var result = TestRunner.Run(settings, data =>
        {
            data.DrawInteger(0, 100);
            throw new Exception("fail");
        });

        Assert.Equal(42UL, result.Seed); // compile error until Seed added to TestRunResult
    }

    [Fact]
    public void Run_WithNoSeed_ResultCarriesNonNullSeed()
    {
        var settings = new ConjectureSettings { MaxExamples = 10 };

        var result = TestRunner.Run(settings, data =>
        {
            data.DrawInteger(0, 100);
            throw new Exception("fail");
        });

        Assert.NotNull(result.Seed); // compile error until Seed added to TestRunResult
    }

    [Fact]
    public void BuildFailureMessage_IntParam_ContainsParamNameAndValue()
    {
        var settings = new ConjectureSettings { MaxExamples = 50, Seed = 1UL };
        var parameters = Params(nameof(PropertyWithInt));

        var result = TestRunner.Run(settings, data =>
        {
            var args = ParameterStrategyResolver.Resolve(parameters, data);
            if ((int)args[0] > 5) { throw new Exception("fail"); }
        });

        Assert.False(result.Passed);
        var message = PropertyTestCaseRunner.BuildFailureMessage(result, parameters);
        // compile error until BuildFailureMessage added to PropertyTestCaseRunner
        Assert.Contains("x =", message);
    }

    [Fact]
    public void BuildFailureMessage_ContainsSeedReproductionLine()
    {
        var settings = new ConjectureSettings { MaxExamples = 50, Seed = 0xDEADBEEFUL };
        var parameters = Params(nameof(PropertyWithInt));

        var result = TestRunner.Run(settings, data =>
        {
            var args = ParameterStrategyResolver.Resolve(parameters, data);
            if ((int)args[0] > 5) { throw new Exception("fail"); }
        });

        Assert.False(result.Passed);
        var message = PropertyTestCaseRunner.BuildFailureMessage(result, parameters);
        Assert.Contains("Reproduce with: [Property(Seed = 0xDEADBEEF)]", message);
    }

    [Fact]
    public void BuildFailureMessage_MultipleParams_ContainsAllParamNames()
    {
        var settings = new ConjectureSettings { MaxExamples = 50, Seed = 7UL };
        var parameters = Params(nameof(PropertyWithIntAndBool));

        var result = TestRunner.Run(settings, data =>
        {
            var args = ParameterStrategyResolver.Resolve(parameters, data);
            throw new Exception("always fail");
        });

        Assert.False(result.Passed);
        var message = PropertyTestCaseRunner.BuildFailureMessage(result, parameters);
        Assert.Contains("x =", message);
        Assert.Contains("flag =", message);
    }

    [Fact]
    public void Run_PassingProperty_SeedIsStillCarried()
    {
        var settings = new ConjectureSettings { MaxExamples = 5, Seed = 99UL };

        var result = TestRunner.Run(settings, _ => { });

        Assert.True(result.Passed);
        Assert.Equal(99UL, result.Seed); // passing result also carries the seed used
    }
}
