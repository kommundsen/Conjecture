// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core;
using Conjecture.Core.Internal;

namespace Conjecture.Core.Tests.EndToEnd;

public sealed class AttributeE2ETests
{
    // ── IStrategyProvider<T> implementations ──────────────────────────────────

    private sealed class PositiveIntsProvider : IStrategyProvider<int>
    {
        public Strategy<int> Create() => Generate.Integers<int>(1, int.MaxValue);
    }

    private sealed class PositiveBoundedProvider : IStrategyProvider<int>
    {
        public Strategy<int> Create() => Generate.Integers<int>(1, 100);
    }

    // ── Factory method (mirrors [FromFactory(nameof(EvenInts))]) ──────────────

    private static Strategy<int> EvenInts() =>
        Generate.Integers<int>(0, 50).Where(n => n % 2 == 0);

    // ── [Example] before generated: explicit count merges with TestRunner count ─

    [Fact]
    public async Task ExplicitExamples_CombinedCount_EqualsExplicitPlusGenerated()
    {
        // WithExtraExamples is how PropertyTestCaseRunner merges [Example] runs
        // into the TestRunner result before reporting.
        int explicitCount = 2;

        ConjectureSettings settings = new() { MaxExamples = 10, Seed = 1UL };
        TestRunResult generated = await TestRunner.Run(settings, data => _ = Generate.Integers<int>(0, 5).Generate(data));

        Assert.True(generated.Passed);
        Assert.Equal(10, generated.ExampleCount);

        TestRunResult combined = TestRunResult.WithExtraExamples(generated, explicitCount);
        Assert.Equal(12, combined.ExampleCount);
        Assert.True(combined.Passed);
    }

    [Fact]
    public async Task ExplicitExamples_WhenGeneratedFails_ExplicitCountPreserved()
    {
        int explicitCount = 2;

        ConjectureSettings settings = new() { MaxExamples = 100, Seed = 2UL, UseDatabase = false };
        TestRunResult generated = await TestRunner.Run(settings, data =>
        {
            int v = Generate.Integers<int>(0, 100).Generate(data);
            if (v > 5) { throw new Exception("too large"); }
        });

        Assert.False(generated.Passed);

        TestRunResult combined = TestRunResult.WithExtraExamples(generated, explicitCount);
        Assert.False(combined.Passed);
        Assert.Equal(generated.ExampleCount + explicitCount, combined.ExampleCount);
    }

    // ── [From<PositiveIntsProvider>]: generates only positive ints ─────────────

    [Fact]
    public async Task From_PositiveIntsProvider_AllGeneratedValuesArePositive()
    {
        Strategy<int> strategy = new PositiveIntsProvider().Create();
        ConjectureSettings settings = new() { MaxExamples = 100, Seed = 10UL };

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            int v = strategy.Generate(data);
            if (v <= 0) { throw new Exception($"Expected positive, got {v}"); }
        });

        Assert.True(result.Passed);
    }

    // ── [FromFactory(nameof(EvenInts))]: generates only even ints in [0, 50] ───

    [Fact]
    public async Task FromFactory_EvenInts_AllValuesAreEvenAndInRange()
    {
        Strategy<int> strategy = EvenInts();
        ConjectureSettings settings = new() { MaxExamples = 100, Seed = 30UL };

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            int v = strategy.Generate(data);
            if (v < 0 || v > 50) { throw new Exception($"Out of bounds: {v}"); }
            if (v % 2 != 0) { throw new Exception($"Expected even, got {v}"); }
        });

        Assert.True(result.Passed);
    }

    // ── Mixed: provider + inferred in same run ─────────────────────────────────

    [Fact]
    public async Task Mixed_ProviderAndInferred_BothStrategiesWork()
    {
        Strategy<int> fromProvider = new PositiveIntsProvider().Create();
        Strategy<bool> inferred = Generate.Booleans();
        ConjectureSettings settings = new() { MaxExamples = 50, Seed = 40UL };

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            int v = fromProvider.Generate(data);
            inferred.Generate(data);
            if (v <= 0) { throw new Exception($"Expected positive int, got {v}"); }
        });

        Assert.True(result.Passed);
    }

    // ── Failing [From]-constrained test shrinks within strategy bounds ──────────

    [Fact]
    public async Task From_PositiveBounded_FailingTest_ShrinksToMinimalWithinBounds()
    {
        // Provider generates [1, 100]; minimal failing value for v > 5 is 6.
        Strategy<int> strategy = new PositiveBoundedProvider().Create();
        ConjectureSettings settings = new() { MaxExamples = 200, Seed = 20UL, UseDatabase = false };

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            int v = strategy.Generate(data);
            if (v > 5) { throw new Exception("too large"); }
        });

        Assert.False(result.Passed);
        ConjectureData replay = ConjectureData.ForRecord(result.Counterexample!);
        int shrunk = strategy.Generate(replay);

        Assert.Equal(6, shrunk);
    }

    [Fact]
    public async Task From_PositiveBounded_ShrunkCounterexample_RespectsUpperBound()
    {
        Strategy<int> strategy = new PositiveBoundedProvider().Create();
        ConjectureSettings settings = new() { MaxExamples = 50, Seed = 21UL, UseDatabase = false };

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            int v = strategy.Generate(data);
            if (v > 0) { throw new Exception("always fails"); }
        });

        Assert.False(result.Passed);
        ConjectureData replay = ConjectureData.ForRecord(result.Counterexample!);
        int shrunk = strategy.Generate(replay);

        Assert.True(shrunk >= 1 && shrunk <= 100, $"Shrunk value {shrunk} must be in [1, 100]");
    }
}