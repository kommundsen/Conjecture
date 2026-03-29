using System.Reflection;
using Conjecture.Core;
using Conjecture.Core.Internal;
using Conjecture.Xunit.Internal;

namespace Conjecture.Xunit.Tests.EndToEnd;

/// <summary>
/// End-to-end tests verifying that [Property] with float/double parameters
/// runs correctly and that special IEEE 754 values (NaN) can be generated.
/// </summary>
public class FloatingPointPropertyE2ETests
{
    // [Property]-decorated methods xUnit discovers directly — must pass.
#pragma warning disable IDE0060
    [Property(MaxExamples = 20, Seed = 1UL)]
    public void DoubleParameter_NoAssertion_Passes(double d) { }

    [Property(MaxExamples = 20, Seed = 1UL)]
    public void FloatParameter_NoAssertion_Passes(float f) { }
#pragma warning restore IDE0060

    private static void DoubleMethod(double d) { }
    private static void FloatMethod(float f) { }

    private static ParameterInfo[] Params(string methodName) =>
        typeof(FloatingPointPropertyE2ETests)
            .GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static)!
            .GetParameters();

    // --- [Property] with double param runs MaxExamples without exception ---

    [Fact]
    public void DoubleParameter_PassingProperty_RunsMaxExamplesWithoutException()
    {
        int count = 0;
        var settings = new ConjectureSettings { MaxExamples = 50, Seed = 1UL };
        var parameters = Params(nameof(DoubleMethod));

        var result = TestRunner.Run(settings, data =>
        {
            ParameterStrategyResolver.Resolve(parameters, data);
            count++;
        });

        Assert.True(result.Passed);
        Assert.Equal(50, count);
        Assert.Null(result.Counterexample);
    }

    // --- [Property] with float param runs MaxExamples without exception ---

    [Fact]
    public void FloatParameter_PassingProperty_RunsMaxExamplesWithoutException()
    {
        int count = 0;
        var settings = new ConjectureSettings { MaxExamples = 50, Seed = 2UL };
        var parameters = Params(nameof(FloatMethod));

        var result = TestRunner.Run(settings, data =>
        {
            ParameterStrategyResolver.Resolve(parameters, data);
            count++;
        });

        Assert.True(result.Passed);
        Assert.Equal(50, count);
        Assert.Null(result.Counterexample);
    }

    // --- Special values (NaN) can be generated through ParameterStrategyResolver ---

    [Fact]
    public void DoubleParameter_CanProduceNaN_OverManySamples()
    {
        var parameters = Params(nameof(DoubleMethod));
        bool foundNaN = false;

        for (ulong seed = 0; seed < 10_000 && !foundNaN; seed++)
        {
            var settings = new ConjectureSettings { MaxExamples = 1, Seed = seed };
            TestRunner.Run(settings, data =>
            {
                object[] args = ParameterStrategyResolver.Resolve(parameters, data);
                if (double.IsNaN((double)args[0])) { foundNaN = true; }
            });
        }

        Assert.True(foundNaN, "Expected Gen.Doubles() to produce NaN over 10,000 seeds through ParameterStrategyResolver");
    }

    [Fact]
    public void FloatParameter_CanProduceNaN_OverManySamples()
    {
        var parameters = Params(nameof(FloatMethod));
        bool foundNaN = false;

        for (ulong seed = 0; seed < 10_000 && !foundNaN; seed++)
        {
            var settings = new ConjectureSettings { MaxExamples = 1, Seed = seed };
            TestRunner.Run(settings, data =>
            {
                object[] args = ParameterStrategyResolver.Resolve(parameters, data);
                if (float.IsNaN((float)args[0])) { foundNaN = true; }
            });
        }

        Assert.True(foundNaN, "Expected Gen.Floats() to produce NaN over 10,000 seeds through ParameterStrategyResolver");
    }

    // --- Failing double property produces counterexample ---

    [Fact]
    public void DoubleParameter_FailingProperty_ProducesCounterexample()
    {
        var settings = new ConjectureSettings { MaxExamples = 200, Seed = 3UL };
        var parameters = Params(nameof(DoubleMethod));

        var result = TestRunner.Run(settings, data =>
        {
            object[] args = ParameterStrategyResolver.Resolve(parameters, data);
            double d = (double)args[0];
            // Fails when value is finite and > 0 — should find a counterexample quickly.
            if (double.IsFinite(d) && d > 0) { throw new Exception("positive finite"); }
        });

        Assert.False(result.Passed);
        Assert.NotNull(result.Counterexample);
    }

    // --- Failure message contains param name and seed ---

    [Fact]
    public void DoubleParameter_FailureMessage_ContainsParamNameAndSeed()
    {
        var settings = new ConjectureSettings { MaxExamples = 200, Seed = 5UL };
        var parameters = Params(nameof(DoubleMethod));

        var result = TestRunner.Run(settings, data =>
        {
            object[] args = ParameterStrategyResolver.Resolve(parameters, data);
            double d = (double)args[0];
            if (double.IsFinite(d) && d > 0) { throw new Exception("positive finite"); }
        });

        Assert.False(result.Passed);
        string message = PropertyTestCaseRunner.BuildFailureMessage(result, parameters);
        Assert.Contains("d =", message);
        Assert.Contains("Reproduce with: [Property(Seed = 0x5)]", message);
    }
}
