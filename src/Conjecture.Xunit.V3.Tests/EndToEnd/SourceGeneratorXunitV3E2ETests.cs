using System.Reflection;
using Conjecture.Core;
using Conjecture.Core.Internal;
using Conjecture.Xunit.V3;
using Xunit;

namespace Conjecture.Xunit.V3.Tests.EndToEnd;

// Domain types decorated with [Arbitrary] at namespace level so the source
// generator can emit V3SourcePointArbitrary / V3SourceTagArbitrary / V3SourceEventArbitrary
// without nested-type FQN issues.

[Arbitrary]
public partial record V3SourcePoint(int X, int Y);

[Arbitrary]
public partial record V3SourceTag(string Label, int Value);

// V3SourceEvent contains a V3SourceTag field — generator must recurse into V3SourceTagArbitrary.
[Arbitrary]
public partial record V3SourceEvent(V3SourceTag Tag, bool Active);

// byte fields keep the shrinking raw-range in [0, 255]: RedistributionPass stays fast.
[Arbitrary]
public partial record V3SourceCoord(byte X, byte Y);

/// <summary>
/// End-to-end tests verifying that [Arbitrary] partial records, source-generated
/// providers, auto-discovery, nested types, and shrinking all integrate correctly
/// with the xUnit v3 [Property] pipeline.
/// </summary>
public class SourceGeneratorXunitV3E2ETests
{
    // ── Private helpers ─────────────────────────────────────────────────────────

#pragma warning disable IDE0060
    private static void V3SourcePointMethod(V3SourcePoint p) { }
    private static void V3SourceEventMethod(V3SourceEvent e) { }
    private static void V3SourceCoordMethod(V3SourceCoord c) { }
#pragma warning restore IDE0060

    private static ParameterInfo[] Params(string methodName) =>
        typeof(SourceGeneratorXunitV3E2ETests)
            .GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static)!
            .GetParameters();

    // ── [Arbitrary] record + [Property] runs/passes ─────────────────────────────

#pragma warning disable IDE0060
    [Property(MaxExamples = 50, Seed = 1UL)]
    public void ArbitraryRecord_Property_RunsAndPasses(V3SourcePoint p) { }
#pragma warning restore IDE0060

    // ── Failing property shrinks ─────────────────────────────────────────────────

    [Fact]
    public async Task GeneratedArbitrary_FailingProperty_ShrinksToCounterexample()
    {
        // Use V3SourceCoord (byte fields, range [0,255]) so RedistributionPass stays
        // O(max 255 iterations) rather than O(2 billion) for full int range.
        Strategy<V3SourceCoord> strategy = new V3SourceCoordArbitrary().Create();
        ConjectureSettings settings = new() { MaxExamples = 100, Seed = 1UL, UseDatabase = false };

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            V3SourceCoord c = strategy.Generate(data);
            if (c.X > 5) { throw new Exception("X too large"); }
        });

        Assert.False(result.Passed);
        Assert.NotNull(result.Counterexample);
        V3SourceCoord shrunk = strategy.Generate(ConjectureData.ForRecord(result.Counterexample!));
        Assert.True(shrunk.X > 5, $"Shrunk {shrunk} does not trigger the failure condition");
    }

    [Fact]
    public async Task GeneratedArbitrary_FailureMessage_ContainsParamNameAndSeed()
    {
        // byte fields avoid the O(2^32) RedistributionPass issue with full int range.
        ConjectureSettings settings = new() { MaxExamples = 100, Seed = 3UL, UseDatabase = false };
        ParameterInfo[] parameters = Params(nameof(V3SourceCoordMethod));

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            object[] args = SharedParameterStrategyResolver.Resolve(parameters, data);
            V3SourceCoord c = (V3SourceCoord)args[0];
            if (c.X > 5) { throw new Exception("X too large"); }
        });

        Assert.False(result.Passed);
        string message = TestCaseHelper.BuildFailureMessage(result, parameters);
        Assert.Contains("c =", message);
        Assert.Contains("Reproduce with: [Property(Seed = 0x3)]", message);
    }

    // ── Nested [Arbitrary] works ─────────────────────────────────────────────────

#pragma warning disable IDE0060
    [Property(MaxExamples = 50, Seed = 2UL)]
    public void NestedArbitraryRecord_Property_RunsAndPasses(V3SourceEvent e) { }
#pragma warning restore IDE0060

    [Fact]
    public async Task NestedArbitraryRecord_PassingProperty_GeneratesValidValues()
    {
        ConjectureSettings settings = new() { MaxExamples = 30, Seed = 5UL };
        ParameterInfo[] parameters = Params(nameof(V3SourceEventMethod));

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            object[] args = SharedParameterStrategyResolver.Resolve(parameters, data);
            V3SourceEvent e = (V3SourceEvent)args[0];
            if (e.Tag is null) { throw new Exception("Tag must not be null"); }
        });

        Assert.True(result.Passed);
    }

    // ── Auto-discovery: no explicit [From<T>] needed ─────────────────────────────

    [Fact]
    public async Task AutoDiscovery_WithoutExplicitFrom_ResolvesGeneratedProvider()
    {
        ConjectureSettings settings = new() { MaxExamples = 50, Seed = 10UL };
        ParameterInfo[] parameters = Params(nameof(V3SourcePointMethod));

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            object[] args = SharedParameterStrategyResolver.Resolve(parameters, data);
            Assert.IsType<V3SourcePoint>(args[0]);
        });

        Assert.True(result.Passed);
    }

    [Fact]
    public async Task AutoDiscovery_ValuesAreDeterministicAcrossReplays()
    {
        ParameterInfo[] parameters = Params(nameof(V3SourcePointMethod));

        V3SourcePoint First()
        {
            ConjectureData data = ConjectureData.ForGeneration(new SplittableRandom(7UL));
            object[] args = SharedParameterStrategyResolver.Resolve(parameters, data);
            return (V3SourcePoint)args[0];
        }

        V3SourcePoint p1 = First();
        V3SourcePoint p2 = First();
        Assert.Equal(p1, p2);
    }
}
