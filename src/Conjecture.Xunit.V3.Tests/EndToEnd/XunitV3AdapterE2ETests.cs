// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Reflection;

using Conjecture.Core;
using Conjecture.Core.Internal;
using Conjecture.Xunit.V3;

using Xunit;

namespace Conjecture.Xunit.V3.Tests.EndToEnd;

file sealed class BoundedPositiveInts : IStrategyProvider<int>
{
    public Strategy<int> Create() => Strategy.Integers<int>(1, 100);
}

/// <summary>
/// End-to-end tests for the xUnit v3 adapter covering the full pipeline:
/// basic [Property], failing + shrinking, [Sample], [From&lt;T&gt;], [FromMethod],
/// async, database round-trip, and settings propagation.
/// </summary>
public sealed class XunitV3AdapterE2ETests : IDisposable
{
    private readonly string tempDir;

    public XunitV3AdapterE2ETests()
    {
        tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
    }

    public void Dispose()
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
    private static void IntFromMethod([FromMethod(nameof(EvenPositiveInts))] int x) { }
#pragma warning restore IDE0060

    private static ParameterInfo[] Params(string name) =>
        typeof(XunitV3AdapterE2ETests)
            .GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)!
            .GetParameters();

    // ── Basic [Property] ─────────────────────────────────────────────────────────

    [Property(MaxExamples = 20, Seed = 1UL)]
    public void BasicProperty_IntParam_Passes(int x) { _ = x; }

    [Property(MaxExamples = 20, Seed = 2UL)]
    public void BasicProperty_BoolParam_Passes(bool b) { _ = b; }

    [Fact]
    public async Task BasicProperty_MaxExamples_RunsExactCount()
    {
        int count = 0;
        ConjectureSettings settings = new() { MaxExamples = 13, Seed = 1UL };
        TestRunResult result = await TestRunner.Run(settings, _ => count++);

        Assert.True(result.Passed);
        Assert.Equal(13, count);
    }

    // ── Failing + shrinking ──────────────────────────────────────────────────────

    [Fact]
    public async Task FailingProperty_ProducesCounterexample()
    {
        ConjectureSettings settings = new() { MaxExamples = 20, Seed = 1UL };
        TestRunResult result = await TestRunner.Run(settings, _ => throw new Exception("always fails"));

        Assert.False(result.Passed);
        Assert.NotNull(result.Counterexample);
    }

    [Fact]
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

        Assert.False(result.Passed);
        ConjectureData replay = ConjectureData.ForRecord(result.Counterexample!);
        object[] shrunk = SharedParameterStrategyResolver.Resolve(parameters, replay);
        Assert.Equal(6, (int)shrunk[0]);
    }

    [Fact]
    public async Task FailingProperty_FailureMessage_ContainsParamNameAndSeed()
    {
        ConjectureSettings settings = new() { MaxExamples = 100, Seed = 5UL };
        ParameterInfo[] parameters = Params(nameof(IntMethod));

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            object[] args = SharedParameterStrategyResolver.Resolve(parameters, data);
            if ((int)args[0] > 5) { throw new Exception("too large"); }
        });

        Assert.False(result.Passed);
        string message = TestCaseHelper.BuildFailureMessage(result, parameters);
        Assert.Contains("x =", message);
        Assert.Contains("Reproduce with: [Property(Seed = 0x5)]", message);
    }

    [Fact]
    public async Task FailingProperty_MultipleParams_AllInMessage()
    {
        ConjectureSettings settings = new() { MaxExamples = 100, Seed = 7UL };
        ParameterInfo[] parameters = Params(nameof(TwoIntMethod));

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            object[] args = SharedParameterStrategyResolver.Resolve(parameters, data);
            if ((int)args[0] + (int)args[1] > 10) { throw new Exception("sum too large"); }
        });

        Assert.False(result.Passed);
        string message = TestCaseHelper.BuildFailureMessage(result, parameters);
        Assert.Contains("x =", message);
        Assert.Contains("y =", message);
    }

    // ── [Sample] ───────────────────────────────────────────────────────────────

    [Property(MaxExamples = 5, Seed = 1UL)]
    [Sample(42, true)]
    [Sample(0, false)]
    public void SampleAttribute_ExplicitCasesRunAlongGenerated(int x, bool flag) { _ = x; _ = flag; }

    [Fact]
    public async Task SampleAttribute_ExplicitCountMergesWithGeneratedCount()
    {
        ConjectureSettings settings = new() { MaxExamples = 10, Seed = 1UL };
        TestRunResult generated = await TestRunner.Run(settings, _ => { });

        TestRunResult combined = TestRunResult.WithExtraExamples(generated, 3);

        Assert.Equal(13, combined.ExampleCount);
        Assert.True(combined.Passed);
    }

    // ── [From<T>] ───────────────────────────────────────────────────────────────

    [Property(MaxExamples = 50, Seed = 1UL)]
    public void FromAttribute_BoundedPositiveInts_AllValuesInRange([From<BoundedPositiveInts>] int x)
    {
        Assert.True(x >= 1 && x <= 100, $"Expected x in [1, 100], got {x}");
    }

    [Fact]
    public async Task FromAttribute_ConstrainedStrategy_AllValuesPassPredicate()
    {
        Strategy<int> strategy = new BoundedPositiveInts().Create();
        ConjectureSettings settings = new() { MaxExamples = 100, Seed = 3UL };

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            int v = strategy.Generate(data);
            if (v < 1 || v > 100) { throw new Exception($"Out of range: {v}"); }
        });

        Assert.True(result.Passed);
    }

    [Fact]
    public async Task FromAttribute_ConstrainedStrategy_FailingShrinksToBoundary()
    {
        // [From<BoundedPositiveInts>] generates [1, 100]; property fails when v > 5 → shrinks to 6.
        Strategy<int> strategy = new BoundedPositiveInts().Create();
        ConjectureSettings settings = new() { MaxExamples = 200, Seed = 20UL, Database = false };

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            int v = strategy.Generate(data);
            if (v > 5) { throw new Exception("too large"); }
        });

        Assert.False(result.Passed);
        ConjectureData replay = ConjectureData.ForRecord(result.Counterexample!);
        Assert.Equal(6, strategy.Generate(replay));
    }

    // ── [FromMethod] ────────────────────────────────────────────────────────────

    [Property(MaxExamples = 20, Seed = 1UL)]
    public void FromMethodAttribute_EvenPositiveInts_AllValuesEvenAndInRange(
        [FromMethod(nameof(EvenPositiveInts))] int x)
    {
        Assert.True(x >= 1 && x <= 50 && x % 2 == 0, $"Expected even in [1,50], got {x}");
    }

    [Fact]
    public async Task FromMethodAttribute_ViaResolver_ConstrainsValues()
    {
        ConjectureSettings settings = new() { MaxExamples = 50, Seed = 11UL };
        ParameterInfo[] parameters = Params(nameof(IntFromMethod));

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            object[] args = SharedParameterStrategyResolver.Resolve(parameters, data);
            int x = (int)args[0];
            if (x < 1 || x > 50 || x % 2 != 0) { throw new Exception($"Expected even in [1,50], got {x}"); }
        });

        Assert.True(result.Passed);
    }

    // ── Async ────────────────────────────────────────────────────────────────────

    [Property(MaxExamples = 20, Seed = 1UL)]
    public async Task AsyncProperty_TaskReturn_Passes(int x)
    {
        await Task.Yield();
        _ = x;
    }

    [Fact]
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

        Assert.True(result.Passed);
        Assert.Equal(12, count);
    }

    [Fact]
    public async Task AsyncProperty_Failing_FindsCounterexample()
    {
        ConjectureSettings settings = new() { MaxExamples = 10, Seed = 1UL };
        TestRunResult result = await TestRunner.RunAsync(settings, async data =>
        {
            await Task.Yield();
            throw new InvalidOperationException("async always fails");
        });

        Assert.False(result.Passed);
        Assert.NotNull(result.Counterexample);
    }

    // ── Database ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Database_FailingRun_SavesCounterexampleBuffer()
    {
        string dbPath = Path.Combine(tempDir, "conjecture.db");
        ConjectureSettings settings = new() { MaxExamples = 10, Database = true };
        string testId = "v3-e2e-fail-saves";

        using ExampleDatabase db = new(dbPath);
        await TestRunner.Run(settings, _ => throw new Exception("fail"), db, testId);

        Assert.NotEmpty(db.Load(testId));
    }

    [Fact]
    public async Task Database_SecondRun_ReplaysStoredBuffer()
    {
        string dbPath = Path.Combine(tempDir, "conjecture.db");
        ConjectureSettings settings = new() { MaxExamples = 10, Database = true };
        string testId = "v3-e2e-replay";
        bool replayInvoked = false;

        using ExampleDatabase db = new(dbPath);
        await TestRunner.Run(settings, _ => throw new Exception("fail"), db, testId);

        await TestRunner.Run(settings, data =>
        {
            if (data.IsReplay) { replayInvoked = true; }
            throw new Exception("still failing");
        }, db, testId);

        Assert.True(replayInvoked);
    }

    [Fact]
    public async Task Database_FixedProperty_RemovesStoredBuffer()
    {
        string dbPath = Path.Combine(tempDir, "conjecture.db");
        ConjectureSettings settings = new() { MaxExamples = 10, Database = true };
        string testId = "v3-e2e-fix-clears";

        using ExampleDatabase db = new(dbPath);
        await TestRunner.Run(settings, _ => throw new Exception("fail"), db, testId);
        Assert.NotEmpty(db.Load(testId));

        await TestRunner.Run(settings, _ => { }, db, testId);
        Assert.Empty(db.Load(testId));
    }

    // ── Settings ─────────────────────────────────────────────────────────────────

    [Fact]
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

        Assert.Equal(
            run1.Counterexample!.Select(n => n.Value),
            run2.Counterexample!.Select(n => n.Value));
    }

    [Fact]
    public async Task Settings_MaxExamples_IsRespected()
    {
        int count = 0;
        ConjectureSettings settings = new() { MaxExamples = 7, Seed = 1UL };
        await TestRunner.Run(settings, _ => count++);

        Assert.Equal(7, count);
    }
}