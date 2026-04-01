using System.Reflection;
using Conjecture.Core;
using Conjecture.Core.Internal;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ConjectureProperty = Conjecture.MSTest.PropertyAttribute;

namespace Conjecture.MSTest.Tests.EndToEnd;

// Domain types decorated with [Arbitrary] at namespace level so the source
// generator can emit MsSourcePointArbitrary / MsSourceTagArbitrary / MsSourceEventArbitrary
// without nested-type FQN issues.

[Arbitrary]
public partial record MsSourcePoint(int X, int Y);

[Arbitrary]
public partial record MsSourceTag(string Label, int Value);

// MsSourceEvent contains a MsSourceTag field — generator must recurse into MsSourceTagArbitrary.
[Arbitrary]
public partial record MsSourceEvent(MsSourceTag Tag, bool Active);

// byte fields keep the shrinking raw-range in [0, 255]: RedistributionPass stays fast.
[Arbitrary]
public partial record MsSourceCoord(byte X, byte Y);

/// <summary>
/// End-to-end tests verifying that [Arbitrary] partial records, source-generated
/// providers, auto-discovery, nested types, and shrinking all integrate correctly
/// with the MSTest [Property] pipeline.
/// </summary>
[TestClass]
public class SourceGeneratorMSTestE2ETests
{
    // ── Private helpers ─────────────────────────────────────────────────────────

#pragma warning disable IDE0060
    private static void MsSourcePointMethod(MsSourcePoint p) { }
    private static void MsSourceEventMethod(MsSourceEvent e) { }
    private static void MsSourceCoordMethod(MsSourceCoord c) { }
#pragma warning restore IDE0060

    private static ParameterInfo[] Params(string methodName) =>
        typeof(SourceGeneratorMSTestE2ETests)
            .GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static)!
            .GetParameters();

    // ── [Arbitrary] record + [Property] runs/passes ─────────────────────────────

#pragma warning disable IDE0060
    [ConjectureProperty(MaxExamples = 50, Seed = 1UL)]
    public void ArbitraryRecord_Property_RunsAndPasses(MsSourcePoint p) { }
#pragma warning restore IDE0060

    // ── Failing property shrinks ─────────────────────────────────────────────────

    [TestMethod]
    public async Task GeneratedArbitrary_FailingProperty_ShrinksToCounterexample()
    {
        // Use MsSourceCoord (byte fields, range [0,255]) so RedistributionPass stays
        // O(max 255 iterations) rather than O(2 billion) for full int range.
        Strategy<MsSourceCoord> strategy = new MsSourceCoordArbitrary().Create();
        ConjectureSettings settings = new() { MaxExamples = 100, Seed = 1UL, UseDatabase = false };

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            MsSourceCoord c = strategy.Generate(data);
            if (c.X > 5) { throw new Exception("X too large"); }
        });

        Assert.IsFalse(result.Passed);
        Assert.IsNotNull(result.Counterexample);
        MsSourceCoord shrunk = strategy.Generate(ConjectureData.ForRecord(result.Counterexample!));
        Assert.IsTrue(shrunk.X > 5, $"Shrunk {shrunk} does not trigger the failure condition");
    }

    [TestMethod]
    public async Task GeneratedArbitrary_FailureMessage_ContainsParamNameAndSeed()
    {
        // byte fields avoid the O(2^32) RedistributionPass issue with full int range.
        ConjectureSettings settings = new() { MaxExamples = 100, Seed = 3UL, UseDatabase = false };
        ParameterInfo[] parameters = Params(nameof(MsSourceCoordMethod));

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            object[] args = SharedParameterStrategyResolver.Resolve(parameters, data);
            MsSourceCoord c = (MsSourceCoord)args[0];
            if (c.X > 5) { throw new Exception("X too large"); }
        });

        Assert.IsFalse(result.Passed);
        string message = TestCaseHelper.BuildFailureMessage(result, parameters);
        Assert.IsTrue(message.Contains("c ="), $"Expected 'c =' in: {message}");
        Assert.IsTrue(message.Contains("Reproduce with: [Property(Seed = 0x3)]"), $"Expected seed line in: {message}");
    }

    // ── Nested [Arbitrary] works ─────────────────────────────────────────────────

#pragma warning disable IDE0060
    [ConjectureProperty(MaxExamples = 50, Seed = 2UL)]
    public void NestedArbitraryRecord_Property_RunsAndPasses(MsSourceEvent e) { }
#pragma warning restore IDE0060

    [TestMethod]
    public async Task NestedArbitraryRecord_PassingProperty_GeneratesValidValues()
    {
        ConjectureSettings settings = new() { MaxExamples = 30, Seed = 5UL };
        ParameterInfo[] parameters = Params(nameof(MsSourceEventMethod));

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            object[] args = SharedParameterStrategyResolver.Resolve(parameters, data);
            MsSourceEvent e = (MsSourceEvent)args[0];
            if (e.Tag is null) { throw new Exception("Tag must not be null"); }
        });

        Assert.IsTrue(result.Passed);
    }

    // ── Auto-discovery: no explicit [From<T>] needed ─────────────────────────────

    [TestMethod]
    public async Task AutoDiscovery_WithoutExplicitFrom_ResolvesGeneratedProvider()
    {
        ConjectureSettings settings = new() { MaxExamples = 50, Seed = 10UL };
        ParameterInfo[] parameters = Params(nameof(MsSourcePointMethod));

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            object[] args = SharedParameterStrategyResolver.Resolve(parameters, data);
            Assert.IsInstanceOfType<MsSourcePoint>(args[0]);
        });

        Assert.IsTrue(result.Passed);
    }

    [TestMethod]
    public async Task AutoDiscovery_ValuesAreDeterministicAcrossReplays()
    {
        ParameterInfo[] parameters = Params(nameof(MsSourcePointMethod));

        MsSourcePoint First()
        {
            ConjectureData data = ConjectureData.ForGeneration(new SplittableRandom(7UL));
            object[] args = SharedParameterStrategyResolver.Resolve(parameters, data);
            return (MsSourcePoint)args[0];
        }

        MsSourcePoint p1 = First();
        MsSourcePoint p2 = First();
        Assert.AreEqual(p1, p2);
    }
}
