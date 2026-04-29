// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Reflection;

using Conjecture.Core;
using Conjecture.Core.Internal;

using NUnit.Framework;

namespace Conjecture.NUnit.Tests.EndToEnd;

file sealed class BoundedPositiveInts : IStrategyProvider<int>
{
    public Strategy<int> Create() => Strategy.Integers<int>(1, 100);
}

/// <summary>
/// End-to-end tests for the NUnit adapter covering the full pipeline:
/// basic [Property], failing + shrinking, [Example], [From&lt;T&gt;], [FromFactory],
/// async, database round-trip, and settings propagation.
/// </summary>
[TestFixture]
public sealed class NUnitAdapterE2ETests
{
    private string tempDir = null!;

    [SetUp]
    public void SetUp()
    {
        tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
    }

    [TearDown]
    public void TearDown()
    {
        try { Directory.Delete(tempDir, recursive: true); }
        catch (IOException) { }
    }

    // ── Helpers for resolver-based tests ────────────────────────────────────────

    private static Strategy<int> EvenPositiveInts() =>
        Strategy.Integers<int>(1, 50).Where(n => n % 2 == 0);

#pragma warning disable IDE0060
    private static void IntMethod(int x) { }
    private static void TwoIntMethod(int x, int y) { }
    private static void IntFromFactoryMethod([FromFactory(nameof(EvenPositiveInts))] int x) { }
#pragma warning restore IDE0060

    private static ParameterInfo[] Params(string name) =>
        typeof(NUnitAdapterE2ETests)
            .GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)!
            .GetParameters();

    // ── Basic [Property] ─────────────────────────────────────────────────────────

#pragma warning disable IDE0060
    [Conjecture.NUnit.Property(MaxExamples = 20, Seed = 1UL)]
    public void BasicProperty_IntParam_Passes(int x) { }

    [Conjecture.NUnit.Property(MaxExamples = 20, Seed = 2UL)]
    public void BasicProperty_BoolParam_Passes(bool b) { }
#pragma warning restore IDE0060

    [Test]
    public async Task BasicProperty_MaxExamples_RunsExactCount()
    {
        int count = 0;
        ConjectureSettings settings = new() { MaxExamples = 13, Seed = 1UL };
        TestRunResult result = await TestRunner.Run(settings, _ => count++);

        Assert.That(result.Passed, Is.True);
        Assert.That(count, Is.EqualTo(13));
    }

    // ── Failing + shrinking ──────────────────────────────────────────────────────

    [Test]
    public async Task FailingProperty_ProducesCounterexample()
    {
        ConjectureSettings settings = new() { MaxExamples = 20, Seed = 1UL };
        TestRunResult result = await TestRunner.Run(settings, _ => throw new Exception("always fails"));

        Assert.That(result.Passed, Is.False);
        Assert.That(result.Counterexample, Is.Not.Null);
    }

    [Test]
    public async Task FailingIntProperty_ShrinksToBoundary()
    {
        // Property fails when x > 5; minimal counterexample is x = 6.
        ConjectureSettings settings = new() { MaxExamples = 100, Seed = 1UL };
        ParameterInfo[] parameters = Params(nameof(IntMethod));

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            object[] args = SharedParameterStrategyResolver.Resolve(parameters, data);
            if ((int)args[0] > 5) { throw new Exception("too large"); }
        });

        Assert.That(result.Passed, Is.False);
        ConjectureData replay = ConjectureData.ForRecord(result.Counterexample!);
        object[] shrunk = SharedParameterStrategyResolver.Resolve(parameters, replay);
        Assert.That((int)shrunk[0], Is.EqualTo(6));
    }

    [Test]
    public async Task FailingProperty_FailureMessage_ContainsParamNameAndSeed()
    {
        ConjectureSettings settings = new() { MaxExamples = 100, Seed = 5UL };
        ParameterInfo[] parameters = Params(nameof(IntMethod));

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            object[] args = SharedParameterStrategyResolver.Resolve(parameters, data);
            if ((int)args[0] > 5) { throw new Exception("too large"); }
        });

        Assert.That(result.Passed, Is.False);
        string message = TestCaseHelper.BuildFailureMessage(result, parameters);
        Assert.That(message, Does.Contain("x ="));
        Assert.That(message, Does.Contain("Reproduce with: [Property(Seed = 0x5)]"));
    }

    [Test]
    public async Task FailingProperty_MultipleParams_AllInMessage()
    {
        ConjectureSettings settings = new() { MaxExamples = 100, Seed = 7UL };
        ParameterInfo[] parameters = Params(nameof(TwoIntMethod));

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            object[] args = SharedParameterStrategyResolver.Resolve(parameters, data);
            if ((int)args[0] + (int)args[1] > 10) { throw new Exception("sum too large"); }
        });

        Assert.That(result.Passed, Is.False);
        string message = TestCaseHelper.BuildFailureMessage(result, parameters);
        Assert.That(message, Does.Contain("x ="));
        Assert.That(message, Does.Contain("y ="));
    }

    // ── [Example] ───────────────────────────────────────────────────────────────

#pragma warning disable IDE0060
    [Conjecture.NUnit.Property(MaxExamples = 5, Seed = 1UL)]
    [Example(42, true)]
    [Example(0, false)]
    public void ExampleAttribute_ExplicitCasesRunAlongGenerated(int x, bool flag) { }
#pragma warning restore IDE0060

    [Test]
    public async Task ExampleAttribute_ExplicitCountMergesWithGeneratedCount()
    {
        ConjectureSettings settings = new() { MaxExamples = 10, Seed = 1UL };
        TestRunResult generated = await TestRunner.Run(settings, _ => { });

        TestRunResult combined = TestRunResult.WithExtraExamples(generated, 3);

        Assert.That(combined.ExampleCount, Is.EqualTo(13));
        Assert.That(combined.Passed, Is.True);
    }

    // ── [From<T>] ───────────────────────────────────────────────────────────────

    [Conjecture.NUnit.Property(MaxExamples = 50, Seed = 1UL)]
    public void FromAttribute_BoundedPositiveInts_AllValuesInRange([From<BoundedPositiveInts>] int x)
    {
        Assert.That(x >= 1 && x <= 100, Is.True, $"Expected x in [1, 100], got {x}");
    }

    [Test]
    public async Task FromAttribute_ConstrainedStrategy_AllValuesPassPredicate()
    {
        Strategy<int> strategy = new BoundedPositiveInts().Create();
        ConjectureSettings settings = new() { MaxExamples = 100, Seed = 3UL };

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            int v = strategy.Generate(data);
            if (v < 1 || v > 100) { throw new Exception($"Out of range: {v}"); }
        });

        Assert.That(result.Passed, Is.True);
    }

    [Test]
    public async Task FromAttribute_ConstrainedStrategy_FailingShrinksToBoundary()
    {
        Strategy<int> strategy = new BoundedPositiveInts().Create();
        ConjectureSettings settings = new() { MaxExamples = 200, Seed = 20UL, UseDatabase = false };

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            int v = strategy.Generate(data);
            if (v > 5) { throw new Exception("too large"); }
        });

        Assert.That(result.Passed, Is.False);
        ConjectureData replay = ConjectureData.ForRecord(result.Counterexample!);
        Assert.That(strategy.Generate(replay), Is.EqualTo(6));
    }

    // ── [FromFactory] ────────────────────────────────────────────────────────────

    [Conjecture.NUnit.Property(MaxExamples = 20, Seed = 1UL)]
    public void FromFactoryAttribute_EvenPositiveInts_AllValuesEvenAndInRange(
        [FromFactory(nameof(EvenPositiveInts))] int x)
    {
        Assert.That(x >= 1 && x <= 50 && x % 2 == 0, Is.True, $"Expected even in [1,50], got {x}");
    }

    [Test]
    public async Task FromFactoryAttribute_ViaResolver_ConstrainsValues()
    {
        ConjectureSettings settings = new() { MaxExamples = 50, Seed = 11UL };
        ParameterInfo[] parameters = Params(nameof(IntFromFactoryMethod));

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            object[] args = SharedParameterStrategyResolver.Resolve(parameters, data);
            int x = (int)args[0];
            if (x < 1 || x > 50 || x % 2 != 0) { throw new Exception($"Expected even in [1,50], got {x}"); }
        });

        Assert.That(result.Passed, Is.True);
    }

    // ── Async ────────────────────────────────────────────────────────────────────

#pragma warning disable IDE0060
    [Conjecture.NUnit.Property(MaxExamples = 20, Seed = 1UL)]
    public async Task AsyncProperty_TaskReturn_Passes(int x)
    {
        await Task.Yield();
        _ = x;
    }
#pragma warning restore IDE0060

    [Test]
    public async Task AsyncProperty_RunsViaRunAsync_MaxExamplesRespected()
    {
        int count = 0;
        ConjectureSettings settings = new() { MaxExamples = 12, Seed = 1UL };
        TestRunResult result = await TestRunner.RunAsync(settings, async data =>
        {
            await Task.Yield();
            _ = Strategy.Integers<int>().Generate(data);
            count++;
        });

        Assert.That(result.Passed, Is.True);
        Assert.That(count, Is.EqualTo(12));
    }

    [Test]
    public async Task AsyncProperty_Failing_FindsCounterexample()
    {
        ConjectureSettings settings = new() { MaxExamples = 10, Seed = 1UL };
        TestRunResult result = await TestRunner.RunAsync(settings, async data =>
        {
            await Task.Yield();
            throw new InvalidOperationException("async always fails");
        });

        Assert.That(result.Passed, Is.False);
        Assert.That(result.Counterexample, Is.Not.Null);
    }

    // ── Database ─────────────────────────────────────────────────────────────────

    [Test]
    public async Task Database_FailingRun_SavesCounterexampleBuffer()
    {
        string dbPath = Path.Combine(tempDir, "conjecture.db");
        ConjectureSettings settings = new() { MaxExamples = 10, UseDatabase = true };
        string testId = "nunit-e2e-fail-saves";

        using ExampleDatabase db = new(dbPath);
        await TestRunner.Run(settings, _ => throw new Exception("fail"), db, testId);

        Assert.That(db.Load(testId), Is.Not.Empty);
    }

    [Test]
    public async Task Database_SecondRun_ReplaysStoredBuffer()
    {
        string dbPath = Path.Combine(tempDir, "conjecture.db");
        ConjectureSettings settings = new() { MaxExamples = 10, UseDatabase = true };
        string testId = "nunit-e2e-replay";
        bool replayInvoked = false;

        using ExampleDatabase db = new(dbPath);
        await TestRunner.Run(settings, _ => throw new Exception("fail"), db, testId);

        await TestRunner.Run(settings, data =>
        {
            if (data.IsReplay) { replayInvoked = true; }
            throw new Exception("still failing");
        }, db, testId);

        Assert.That(replayInvoked, Is.True);
    }

    [Test]
    public async Task Database_FixedProperty_RemovesStoredBuffer()
    {
        string dbPath = Path.Combine(tempDir, "conjecture.db");
        ConjectureSettings settings = new() { MaxExamples = 10, UseDatabase = true };
        string testId = "nunit-e2e-fix-clears";

        using ExampleDatabase db = new(dbPath);
        await TestRunner.Run(settings, _ => throw new Exception("fail"), db, testId);
        Assert.That(db.Load(testId), Is.Not.Empty);

        await TestRunner.Run(settings, _ => { }, db, testId);
        Assert.That(db.Load(testId), Is.Empty);
    }

    // ── Settings ─────────────────────────────────────────────────────────────────

    [Test]
    public async Task Settings_Seed_ProducesDeterministicCounterexample()
    {
        ConjectureSettings settings = new() { MaxExamples = 50, Seed = 42UL };

        TestRunResult run1 = await TestRunner.Run(settings, data =>
        {
            ulong v = data.NextInteger(0, 100);
            if (v > 70) { throw new Exception("fail"); }
        });

        TestRunResult run2 = await TestRunner.Run(settings, data =>
        {
            ulong v = data.NextInteger(0, 100);
            if (v > 70) { throw new Exception("fail"); }
        });

        Assert.That(
            run1.Counterexample!.Select(n => n.Value),
            Is.EqualTo(run2.Counterexample!.Select(n => n.Value)));
    }

    [Test]
    public async Task Settings_MaxExamples_IsRespected()
    {
        int count = 0;
        ConjectureSettings settings = new() { MaxExamples = 7, Seed = 1UL };
        await TestRunner.Run(settings, _ => count++);

        Assert.That(count, Is.EqualTo(7));
    }
}