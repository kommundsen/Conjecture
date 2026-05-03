// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using Conjecture.Core;
using Conjecture.Core.Internal;

namespace Conjecture.Core.Tests.Strategies;

public class HalfStrategyTests
{
    private static ConjectureData MakeData(ulong seed = 42UL) =>
        ConjectureData.ForGeneration(new SplittableRandom(seed));

    // ─── Full-range Halves() ─────────────────────────────────────────────────

    [Fact]
    public void Halves_GeneratesHalfValues()
    {
        Strategy<Half> strategy = Strategy.Halves();
        Assert.All(strategy.WithSeed(42UL).Sample(100), v => Assert.IsType<Half>(v));
    }

    [Fact]
    public void Halves_IncludesPositiveAndNegativeValues()
    {
        Strategy<Half> strategy = Strategy.Halves();
        IReadOnlyList<Half> samples = strategy.WithSeed(0UL).Sample(1000);
        Assert.Contains(samples, v => v > (Half)0);
        Assert.Contains(samples, v => v < (Half)0);
    }

    [Fact]
    public void Halves_DeterministicWithSeed()
    {
        Strategy<Half> strategy = Strategy.Halves();
        IReadOnlyList<Half> results1 = strategy.WithSeed(77UL).Sample(20);
        IReadOnlyList<Half> results2 = strategy.WithSeed(77UL).Sample(20);
        Assert.Equal(results1, results2);
    }

    [Fact]
    public void Halves_ProducesDistinctValues()
    {
        Strategy<Half> strategy = Strategy.Halves();
        IReadOnlyList<Half> values = strategy.WithSeed(42UL).Sample(50);
        Assert.True(values.Distinct().Count() > 1, "Expected multiple distinct Half values");
    }

    // ─── Bounded Halves(min, max) ────────────────────────────────────────────

    [Fact]
    public void Halves_Range_StaysWithinBounds()
    {
        Strategy<Half> strategy = Strategy.Halves((Half)(-1f), (Half)1f);
        ConjectureData data = MakeData();

        for (int i = 0; i < 1000; i++)
        {
            Assert.InRange(strategy.Generate(data), (Half)(-1f), (Half)1f);
        }
    }

    [Fact]
    public void Halves_NegativeRange_StaysWithinBounds()
    {
        Strategy<Half> strategy = Strategy.Halves((Half)(-100f), (Half)(-1f));
        ConjectureData data = MakeData();

        for (int i = 0; i < 1000; i++)
        {
            Assert.InRange(strategy.Generate(data), (Half)(-100f), (Half)(-1f));
        }
    }

    [Fact]
    public void Halves_MinEqualsMax_ReturnsConstant()
    {
        Strategy<Half> strategy = Strategy.Halves((Half)2.5f, (Half)2.5f);
        ConjectureData data = MakeData();

        for (int i = 0; i < 20; i++)
        {
            Assert.Equal((Half)2.5f, strategy.Generate(data));
        }
    }

    [Fact]
    public void Halves_MinGreaterThanMax_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => Strategy.Halves((Half)1f, (Half)0f));
    }

    [Fact]
    public void Halves_Range_ProducesDistinctValues()
    {
        Strategy<Half> strategy = Strategy.Halves((Half)0f, (Half)1f);
        ConjectureData data = MakeData();
        List<Half> values = Enumerable.Range(0, 50).Select(_ => strategy.Generate(data)).ToList();
        Assert.True(values.Distinct().Count() > 1, "Expected multiple distinct values in range");
    }

    // ─── Special values ──────────────────────────────────────────────────────

    private static Half DrawHalf(ulong seed)
    {
        ConjectureData data = ConjectureData.ForGeneration(new SplittableRandom(seed));
        return Strategy.Halves().Generate(data);
    }

    [Fact]
    public void Halves_Unbounded_CanProduceNaN()
    {
        bool found = Enumerable.Range(0, 10_000).Any(s => Half.IsNaN(DrawHalf((ulong)s)));
        Assert.True(found, "Expected Strategy.Halves() to produce NaN over 10,000 seeds");
    }

    [Fact]
    public void Halves_Unbounded_CanProducePositiveInfinity()
    {
        bool found = Enumerable.Range(0, 10_000).Any(s => Half.IsPositiveInfinity(DrawHalf((ulong)s)));
        Assert.True(found, "Expected Strategy.Halves() to produce +Infinity over 10,000 seeds");
    }

    [Fact]
    public void Halves_Unbounded_CanProduceNegativeInfinity()
    {
        bool found = Enumerable.Range(0, 10_000).Any(s => Half.IsNegativeInfinity(DrawHalf((ulong)s)));
        Assert.True(found, "Expected Strategy.Halves() to produce -Infinity over 10,000 seeds");
    }

    [Fact]
    public void Halves_Unbounded_CanProduceSubnormals()
    {
        bool found = Enumerable.Range(0, 10_000).Any(s => Half.IsSubnormal(DrawHalf((ulong)s)));
        Assert.True(found, "Expected Strategy.Halves() to produce subnormal values over 10,000 seeds");
    }

    // ─── Shrinking ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Halves_FailingProperty_ShrinksTowardZero()
    {
        Strategy<Half> strategy = Strategy.Halves((Half)0f, Half.MaxValue);
        ConjectureSettings settings = new() { MaxExamples = 200, Seed = 1UL };

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            Half v = strategy.Generate(data);
            if (v > (Half)0f) { throw new Exception("non-zero"); }
        });

        Assert.False(result.Passed);
        ConjectureData replay = ConjectureData.ForRecord(result.Counterexample!);
        Half shrunk = strategy.Generate(replay);
        Assert.True(shrunk <= (Half)1f, $"Expected shrunk value near zero, got {shrunk}");
    }

    // ─── Strategy.For<Half> ──────────────────────────────────────────────────

    [Fact]
    public void ForHalf_ResolvesAndProducesValues()
    {
        Strategy<Half> strategy = Strategy.For<Half>();

        for (int i = 0; i < 50; i++)
        {
            Half v = strategy.Generate(MakeData((ulong)i));
            Assert.IsType<Half>(v);
        }
    }

    // ─── SharedParameterStrategyResolver with Half ───────────────────────────

#pragma warning disable IDE0060
    private static void HalfParamMethod(Half h) { }
#pragma warning restore IDE0060

    private static ParameterInfo[] ParamsOf(string methodName) =>
        typeof(HalfStrategyTests)
            .GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static)!
            .GetParameters();

    [Fact]
    public void Resolve_HalfParam_ReturnsHalf()
    {
        object[] args = SharedParameterStrategyResolver.Resolve(ParamsOf(nameof(HalfParamMethod)), MakeData());
        Assert.IsType<Half>(args[0]);
    }
}