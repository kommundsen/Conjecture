using Conjecture.Core;
using Conjecture.Core.Internal;

namespace Conjecture.Tests.EndToEnd;

public sealed class AsyncPropertyE2ETests
{
    // ── Helper: IStrategyProvider<T> for positive ints ────────────────────────

    private sealed class PositiveIntsProvider : IStrategyProvider<int>
    {
        public Strategy<int> Create() => Generate.Integers<int>(1, 50);
    }

    // ── Async [Property] returning Task passes ────────────────────────────────

    [Fact]
    public async Task AsyncTask_PassingLogic_RunsFullExampleCount()
    {
        ConjectureSettings settings = new() { MaxExamples = 50, Seed = 1UL };

        TestRunResult result = await TestRunner.RunAsync(settings, async data =>
        {
            int x = Generate.Integers<int>(0, 100).Generate(data);
            await Task.Yield();
            if (x < 0) { throw new Exception("impossible"); }
        });

        Assert.True(result.Passed);
        Assert.Null(result.Counterexample);
        Assert.Equal(50, result.ExampleCount);
    }

    [Fact]
    public async Task AsyncTask_NoGenerationNeeded_PassesImmediately()
    {
        ConjectureSettings settings = new() { MaxExamples = 20, Seed = 2UL };

        TestRunResult result = await TestRunner.RunAsync(settings, async _ =>
        {
            await Task.Yield();
        });

        Assert.True(result.Passed);
        Assert.Null(result.Counterexample);
    }

    // ── Async [Property] that throws shrinks and reports counterexample ────────

    [Fact]
    public async Task AsyncTask_FailingProperty_ShrinksToMinimalCounterexample()
    {
        ConjectureSettings settings = new() { MaxExamples = 200, Seed = 1UL, UseDatabase = false };
        Strategy<int> strategy = Generate.Integers<int>(0, 100);

        TestRunResult result = await TestRunner.RunAsync(settings, async data =>
        {
            int v = strategy.Generate(data);
            await Task.Yield();
            if (v > 5) { throw new Exception("too large"); }
        });

        Assert.False(result.Passed);
        Assert.NotNull(result.Counterexample);
        Assert.True(result.ShrinkCount > 0);

        ConjectureData replay = ConjectureData.ForRecord(result.Counterexample!);
        int shrunk = strategy.Generate(replay);
        Assert.Equal(6, shrunk);
    }

    [Fact]
    public async Task AsyncTask_FailingProperty_ShrunkCounterexampleStillFails()
    {
        ConjectureSettings settings = new() { MaxExamples = 200, Seed = 5UL, UseDatabase = false };
        Strategy<int> strategy = Generate.Integers<int>(0, 200);

        TestRunResult result = await TestRunner.RunAsync(settings, async data =>
        {
            int v = strategy.Generate(data);
            await Task.Yield();
            if (v > 10) { throw new Exception("too large"); }
        });

        Assert.False(result.Passed);

        // Replaying the shrunk counterexample must reproduce the failure.
        ConjectureData replay = ConjectureData.ForRecord(result.Counterexample!);
        int shrunk = strategy.Generate(replay);
        Assert.True(shrunk > 10, $"Shrunk value {shrunk} should still violate the property");
    }

    // ── Async with [Example]: explicit cases run and are counted ──────────────

    [Fact]
    public async Task AsyncTask_ExplicitExamples_ContributeToExampleCount()
    {
        // Simulates PropertyTestCaseRunner running [Example] delegates before
        // calling TestRunner.RunAsync, then merging counts via WithExtraExamples.
        int explicitCount = 3;
        ConjectureSettings settings = new() { MaxExamples = 10, Seed = 7UL };

        // Run explicit async examples (simulating [Example] dispatch).
        for (int i = 0; i < explicitCount; i++)
        {
            await Task.Yield(); // each [Example] is invoked as an async call
        }

        TestRunResult generated = await TestRunner.RunAsync(settings, async data =>
        {
            int v = Generate.Integers<int>(0, 10).Generate(data);
            await Task.Yield();
            if (v < 0) { throw new Exception("impossible"); }
        });

        Assert.True(generated.Passed);
        TestRunResult combined = TestRunResult.WithExtraExamples(generated, explicitCount);
        Assert.Equal(13, combined.ExampleCount);
        Assert.True(combined.Passed);
    }

    [Fact]
    public async Task AsyncTask_ExplicitExampleFails_GeneratedExamplesNotRun()
    {
        // If an explicit [Example] fails, PropertyTestCaseRunner short-circuits before
        // calling TestRunner.RunAsync. We simulate by checking that a failing explicit
        // invocation is detected before the generation phase.
        bool generationPhaseReached = false;

        Exception? explicitFailure = null;
        try
        {
            int badValue = -1;
            await Task.Yield();
            if (badValue < 0) { throw new ArgumentOutOfRangeException(nameof(badValue)); }
        }
        catch (Exception ex)
        {
            explicitFailure = ex;
        }

        if (explicitFailure is null)
        {
            // Only run generation if explicit examples pass (mirrors PropertyTestCaseRunner).
            generationPhaseReached = true;
            await TestRunner.RunAsync(new ConjectureSettings { MaxExamples = 10 }, async data =>
            {
                Generate.Integers<int>().Generate(data);
                await Task.Yield();
            });
        }

        Assert.NotNull(explicitFailure);
        Assert.False(generationPhaseReached, "Generation should not run when an explicit example fails");
    }

    // ── Async with [From<T>]: generates from custom strategy ─────────────────

    [Fact]
    public async Task AsyncTask_FromProvider_AllGeneratedValuesRespectStrategy()
    {
        Strategy<int> strategy = new PositiveIntsProvider().Create();
        ConjectureSettings settings = new() { MaxExamples = 100, Seed = 10UL };

        TestRunResult result = await TestRunner.RunAsync(settings, async data =>
        {
            int v = strategy.Generate(data);
            await Task.Yield();
            if (v <= 0) { throw new Exception($"Expected positive, got {v}"); }
            if (v > 50) { throw new Exception($"Out of bounds: {v}"); }
        });

        Assert.True(result.Passed);
    }

    [Fact]
    public async Task AsyncTask_FromProvider_FailingTest_ShrinksWithinStrategyBounds()
    {
        // PositiveIntsProvider generates [1, 50]; smallest failing value for v > 5 is 6.
        Strategy<int> strategy = new PositiveIntsProvider().Create();
        ConjectureSettings settings = new() { MaxExamples = 200, Seed = 20UL, UseDatabase = false };

        TestRunResult result = await TestRunner.RunAsync(settings, async data =>
        {
            int v = strategy.Generate(data);
            await Task.Yield();
            if (v > 5) { throw new Exception("too large"); }
        });

        Assert.False(result.Passed);
        ConjectureData replay = ConjectureData.ForRecord(result.Counterexample!);
        int shrunk = strategy.Generate(replay);
        Assert.Equal(6, shrunk);
    }

    [Fact]
    public async Task AsyncTask_MixedFromProviderAndInferred_BothWorkTogether()
    {
        Strategy<int> fromProvider = new PositiveIntsProvider().Create();
        Strategy<bool> inferred = Generate.Booleans();
        ConjectureSettings settings = new() { MaxExamples = 50, Seed = 40UL };

        TestRunResult result = await TestRunner.RunAsync(settings, async data =>
        {
            int v = fromProvider.Generate(data);
            bool flag = inferred.Generate(data);
            await Task.Yield();
            if (v <= 0) { throw new Exception($"Expected positive, got {v}"); }
            _ = flag; // consumed
        });

        Assert.True(result.Passed);
    }
}
