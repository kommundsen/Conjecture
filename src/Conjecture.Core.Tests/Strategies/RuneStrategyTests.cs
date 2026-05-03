// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

using Conjecture.Core;
using Conjecture.Core.Internal;

namespace Conjecture.Core.Tests.Strategies;

public class RuneStrategyTests
{
    private static ConjectureData MakeData(ulong seed = 42UL) =>
        ConjectureData.ForGeneration(new SplittableRandom(seed));

    // ─── Surrogate gap exclusion ─────────────────────────────────────────────

    [Fact]
    public void Runes_NeverProducesSurrogate()
    {
        Strategy<Rune> strategy = Strategy.Runes();
        ConjectureData data = MakeData();

        for (int i = 0; i < 10_000; i++)
        {
            Rune value = strategy.Generate(data);
            Assert.False(
                value.Value >= 0xD800 && value.Value <= 0xDFFF,
                $"Surrogate codepoint U+{value.Value:X4} produced");
        }
    }

    // ─── BMP and supplementary plane coverage ───────────────────────────────

    [Fact]
    public void Runes_ProducesValuesAcrossPlanes()
    {
        Strategy<Rune> strategy = Strategy.Runes();
        ConjectureData data = MakeData(0UL);
        bool foundBmp = false;
        bool foundSupplementary = false;

        for (int i = 0; i < 10_000; i++)
        {
            Rune value = strategy.Generate(data);
            if (value.Value <= 0xFFFF)
            {
                foundBmp = true;
            }

            if (value.Value > 0xFFFF)
            {
                foundSupplementary = true;
            }

            if (foundBmp && foundSupplementary)
            {
                break;
            }
        }

        Assert.True(foundBmp, "Expected BMP codepoints (U+0000–U+FFFF) in 10,000 draws");
        Assert.True(foundSupplementary, "Expected supplementary codepoints (>U+FFFF) in 10,000 draws");
    }

    // ─── Bounded Runes(min, max) ─────────────────────────────────────────────

    [Fact]
    public void Runes_BoundedRange_RespectsInclusiveBounds()
    {
        Rune min = new(0x0041);  // 'A'
        Rune max = new(0x007A);  // 'z'
        Strategy<Rune> strategy = Strategy.Runes(min, max);
        ConjectureData data = MakeData();

        for (int i = 0; i < 1_000; i++)
        {
            Rune value = strategy.Generate(data);
            Assert.InRange(value.Value, min.Value, max.Value);
        }
    }

    [Fact]
    public void Runes_RangeStraddlingSurrogates_NeverEmitsSurrogate()
    {
        Rune min = new(0xD000);
        Rune max = new(0xE000);
        Strategy<Rune> strategy = Strategy.Runes(min, max);
        ConjectureData data = MakeData();

        for (int i = 0; i < 10_000; i++)
        {
            Rune value = strategy.Generate(data);
            Assert.False(
                value.Value >= 0xD800 && value.Value <= 0xDFFF,
                $"Surrogate codepoint U+{value.Value:X4} produced in straddled range");
        }
    }

    // ─── Constant case ───────────────────────────────────────────────────────

    [Fact]
    public void Runes_MinEqualsMax_YieldsConstant()
    {
        Rune only = new(0x1F600);  // U+1F600 GRINNING FACE
        Strategy<Rune> strategy = Strategy.Runes(only, only);
        ConjectureData data = MakeData();

        for (int i = 0; i < 20; i++)
        {
            Assert.Equal(only, strategy.Generate(data));
        }
    }

    // ─── Shrinking ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Runes_FailingProperty_ShrinksTowardRuneZero()
    {
        Strategy<Rune> strategy = Strategy.Runes();
        ConjectureSettings settings = new() { MaxExamples = 200, Seed = 1UL };

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            Rune value = strategy.Generate(data);
            throw new Exception($"always fails: U+{value.Value:X4}");
        });

        Assert.False(result.Passed);
        ConjectureData replay = ConjectureData.ForRecord(result.Counterexample!);
        Rune shrunk = strategy.Generate(replay);
        Assert.Equal(new Rune(0), shrunk);
    }

    // ─── Strategy.For<Rune> ──────────────────────────────────────────────────

    [Fact]
    public void ForRune_ResolvesAndProducesValues()
    {
        Strategy<Rune> strategy = Strategy.For<Rune>();

        for (int i = 0; i < 50; i++)
        {
            Rune value = strategy.Generate(MakeData((ulong)i));
            Assert.False(
                value.Value >= 0xD800 && value.Value <= 0xDFFF,
                $"Strategy.For<Rune>() produced surrogate U+{value.Value:X4}");
        }
    }

    // ─── SharedParameterStrategyResolver ────────────────────────────────────

#pragma warning disable IDE0060
    private static void RuneParamMethod(Rune r) { }
#pragma warning restore IDE0060

    private static ParameterInfo[] ParamsOf(string methodName) =>
        typeof(RuneStrategyTests)
            .GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static)!
            .GetParameters();

    [Fact]
    public void Resolve_RuneParam_ReturnsRune()
    {
        object[] args = SharedParameterStrategyResolver.Resolve(ParamsOf(nameof(RuneParamMethod)), MakeData());
        Assert.IsType<Rune>(args[0]);
    }
}