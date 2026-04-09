// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Reflection;

using Conjecture.Core;
using Conjecture.Core.Internal;


namespace Conjecture.Xunit.Tests.EndToEnd;

// Domain types decorated with [Arbitrary] at namespace level so the source
// generator can emit SourcePointArbitrary / SourceTagArbitrary / SourceEventArbitrary
// without nested-type FQN issues.

[Arbitrary]
public partial record SourcePoint(int X, int Y);

[Arbitrary]
public partial record SourceTag(string Label, int Value);

// SourceEvent contains a SourceTag field — generator must recurse into SourceTagArbitrary.
[Arbitrary]
public partial record SourceEvent(SourceTag Tag, bool Active);

// byte fields keep the shrinking raw-range in [0, 255]: RedistributionPass stays fast.
[Arbitrary]
public partial record SourceCoord(byte X, byte Y);

/// <summary>
/// End-to-end tests verifying that [Arbitrary] partial records, source-generated
/// providers, auto-discovery, nested types, and shrinking all integrate correctly
/// with the xUnit [Property] pipeline.
/// </summary>
public class SourceGeneratorE2ETests
{
    // ── Private helpers ─────────────────────────────────────────────────────────

#pragma warning disable IDE0060
    private static void SourcePointMethod(SourcePoint p) { }
    private static void SourceEventMethod(SourceEvent e) { }
    private static void SourceCoordMethod(SourceCoord c) { }
#pragma warning restore IDE0060

    private static ParameterInfo[] Params(string methodName) =>
        typeof(SourceGeneratorE2ETests)
            .GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static)!
            .GetParameters();

    // ── [Arbitrary] record + [Property] runs/passes ─────────────────────────────

#pragma warning disable IDE0060
    [Property(MaxExamples = 50, Seed = 1UL)]
    public void ArbitraryRecord_Property_RunsAndPasses(SourcePoint p) { }
#pragma warning restore IDE0060

    // ── Failing property shrinks ─────────────────────────────────────────────────

    [Fact]
    public async Task GeneratedArbitrary_FailingProperty_ShrinksToCounterexample()
    {
        // Use SourceCoord (byte fields, range [0,255]) so RedistributionPass stays
        // O(max 255 iterations) rather than O(2 billion) for full int range.
        Core.Strategy<SourceCoord> strategy = new SourceCoordArbitrary().Create();
        ConjectureSettings settings = new() { MaxExamples = 100, Seed = 1UL, UseDatabase = false };

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            SourceCoord c = strategy.Generate(data);
            if (c.X > 5) { throw new Exception("X too large"); }
        });

        Assert.False(result.Passed);
        Assert.NotNull(result.Counterexample);
        SourceCoord shrunk = strategy.Generate(ConjectureData.ForRecord(result.Counterexample!));
        Assert.True(shrunk.X > 5, $"Shrunk {shrunk} does not trigger the failure condition");
    }

    [Fact]
    public async Task GeneratedArbitrary_FailureMessage_ContainsParamNameAndSeed()
    {
        // byte fields avoid the O(2^32) RedistributionPass issue with full int range.
        ConjectureSettings settings = new() { MaxExamples = 100, Seed = 3UL, UseDatabase = false };
        ParameterInfo[] parameters = Params(nameof(SourceCoordMethod));

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            object[] args = SharedParameterStrategyResolver.Resolve(parameters, data);
            SourceCoord c = (SourceCoord)args[0];
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
    public void NestedArbitraryRecord_Property_RunsAndPasses(SourceEvent e) { }
#pragma warning restore IDE0060

    [Fact]
    public async Task NestedArbitraryRecord_PassingProperty_GeneratesValidValues()
    {
        ConjectureSettings settings = new() { MaxExamples = 30, Seed = 5UL };
        ParameterInfo[] parameters = Params(nameof(SourceEventMethod));

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            object[] args = SharedParameterStrategyResolver.Resolve(parameters, data);
            SourceEvent e = (SourceEvent)args[0];
            if (e.Tag is null) { throw new Exception("Tag must not be null"); }
        });

        Assert.True(result.Passed);
    }

    // ── Auto-discovery: no explicit [From<T>] needed ─────────────────────────────

    [Fact]
    public async Task AutoDiscovery_WithoutExplicitFrom_ResolvesGeneratedProvider()
    {
        ConjectureSettings settings = new() { MaxExamples = 50, Seed = 10UL };
        ParameterInfo[] parameters = Params(nameof(SourcePointMethod));

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            object[] args = SharedParameterStrategyResolver.Resolve(parameters, data);
            Assert.IsType<SourcePoint>(args[0]);
        });

        Assert.True(result.Passed);
    }

    [Fact]
    public async Task AutoDiscovery_ValuesAreDeterministicAcrossReplays()
    {
        ParameterInfo[] parameters = Params(nameof(SourcePointMethod));

        SourcePoint First()
        {
            ConjectureData data = ConjectureData.ForGeneration(new SplittableRandom(7UL));
            object[] args = SharedParameterStrategyResolver.Resolve(parameters, data);
            return (SourcePoint)args[0];
        }

        SourcePoint p1 = First();
        SourcePoint p2 = First();
        Assert.Equal(p1, p2);
    }
}