using System.Reflection;
using Conjecture.Core;
using Conjecture.Core.Generation;
using Conjecture.Core.Internal;
using Conjecture.NUnit.Internal;
using NUnit.Framework;
using ConjectureProperty = Conjecture.NUnit.PropertyAttribute;

namespace Conjecture.NUnit.Tests.EndToEnd;

// Domain types decorated with [Arbitrary] at namespace level so the source
// generator can emit NuSourcePointArbitrary / NuSourceTagArbitrary / NuSourceEventArbitrary
// without nested-type FQN issues.

[Arbitrary]
public partial record NuSourcePoint(int X, int Y);

[Arbitrary]
public partial record NuSourceTag(string Label, int Value);

// NuSourceEvent contains a NuSourceTag field — generator must recurse into NuSourceTagArbitrary.
[Arbitrary]
public partial record NuSourceEvent(NuSourceTag Tag, bool Active);

// byte fields keep the shrinking raw-range in [0, 255]: RedistributionPass stays fast.
[Arbitrary]
public partial record NuSourceCoord(byte X, byte Y);

/// <summary>
/// End-to-end tests verifying that [Arbitrary] partial records, source-generated
/// providers, auto-discovery, nested types, and shrinking all integrate correctly
/// with the NUnit [Property] pipeline.
/// </summary>
[TestFixture]
public class SourceGeneratorNUnitE2ETests
{
    // ── Private helpers ─────────────────────────────────────────────────────────

#pragma warning disable IDE0060
    private static void NuSourcePointMethod(NuSourcePoint p) { }
    private static void NuSourceEventMethod(NuSourceEvent e) { }
    private static void NuSourceCoordMethod(NuSourceCoord c) { }
#pragma warning restore IDE0060

    private static ParameterInfo[] Params(string methodName) =>
        typeof(SourceGeneratorNUnitE2ETests)
            .GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static)!
            .GetParameters();

    // ── [Arbitrary] record + [Property] runs/passes ─────────────────────────────

#pragma warning disable IDE0060
    [ConjectureProperty(MaxExamples = 50, Seed = 1UL)]
    public void ArbitraryRecord_Property_RunsAndPasses(NuSourcePoint p) { }
#pragma warning restore IDE0060

    // ── Failing property shrinks ─────────────────────────────────────────────────

    [Test]
    public async Task GeneratedArbitrary_FailingProperty_ShrinksToCounterexample()
    {
        // Use NuSourceCoord (byte fields, range [0,255]) so RedistributionPass stays
        // O(max 255 iterations) rather than O(2 billion) for full int range.
        Strategy<NuSourceCoord> strategy = new NuSourceCoordArbitrary().Create();
        ConjectureSettings settings = new() { MaxExamples = 100, Seed = 1UL, UseDatabase = false };

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            NuSourceCoord c = strategy.Next(data);
            if (c.X > 5) { throw new Exception("X too large"); }
        });

        Assert.That(result.Passed, Is.False);
        Assert.That(result.Counterexample, Is.Not.Null);
        NuSourceCoord shrunk = strategy.Next(ConjectureData.ForRecord(result.Counterexample!));
        Assert.That(shrunk.X > 5, Is.True, $"Shrunk {shrunk} does not trigger the failure condition");
    }

    [Test]
    public async Task GeneratedArbitrary_FailureMessage_ContainsParamNameAndSeed()
    {
        // byte fields avoid the O(2^32) RedistributionPass issue with full int range.
        ConjectureSettings settings = new() { MaxExamples = 100, Seed = 3UL, UseDatabase = false };
        ParameterInfo[] parameters = Params(nameof(NuSourceCoordMethod));

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            object[] args = SharedParameterStrategyResolver.Resolve(parameters, data);
            NuSourceCoord c = (NuSourceCoord)args[0];
            if (c.X > 5) { throw new Exception("X too large"); }
        });

        Assert.That(result.Passed, Is.False);
        string message = PropertyTestBuilder.BuildFailureMessage(result, parameters);
        Assert.That(message, Does.Contain("c ="));
        Assert.That(message, Does.Contain("Reproduce with: [Property(Seed = 0x3)]"));
    }

    // ── Nested [Arbitrary] works ─────────────────────────────────────────────────

#pragma warning disable IDE0060
    [ConjectureProperty(MaxExamples = 50, Seed = 2UL)]
    public void NestedArbitraryRecord_Property_RunsAndPasses(NuSourceEvent e) { }
#pragma warning restore IDE0060

    [Test]
    public async Task NestedArbitraryRecord_PassingProperty_GeneratesValidValues()
    {
        ConjectureSettings settings = new() { MaxExamples = 30, Seed = 5UL };
        ParameterInfo[] parameters = Params(nameof(NuSourceEventMethod));

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            object[] args = SharedParameterStrategyResolver.Resolve(parameters, data);
            NuSourceEvent e = (NuSourceEvent)args[0];
            if (e.Tag is null) { throw new Exception("Tag must not be null"); }
        });

        Assert.That(result.Passed, Is.True);
    }

    // ── Auto-discovery: no explicit [From<T>] needed ─────────────────────────────

    [Test]
    public async Task AutoDiscovery_WithoutExplicitFrom_ResolvesGeneratedProvider()
    {
        ConjectureSettings settings = new() { MaxExamples = 50, Seed = 10UL };
        ParameterInfo[] parameters = Params(nameof(NuSourcePointMethod));

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            object[] args = SharedParameterStrategyResolver.Resolve(parameters, data);
            Assert.That(args[0], Is.InstanceOf<NuSourcePoint>());
        });

        Assert.That(result.Passed, Is.True);
    }

    [Test]
    public async Task AutoDiscovery_ValuesAreDeterministicAcrossReplays()
    {
        ParameterInfo[] parameters = Params(nameof(NuSourcePointMethod));

        NuSourcePoint First()
        {
            ConjectureData data = ConjectureData.ForGeneration(new SplittableRandom(7UL));
            object[] args = SharedParameterStrategyResolver.Resolve(parameters, data);
            return (NuSourcePoint)args[0];
        }

        NuSourcePoint p1 = First();
        NuSourcePoint p2 = First();
        Assert.That(p1, Is.EqualTo(p2));
    }
}
