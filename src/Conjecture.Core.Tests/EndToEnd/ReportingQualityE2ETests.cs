// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core;
using Conjecture.Core.Internal;

namespace Conjecture.Core.Tests.EndToEnd;

/// <summary>
/// End-to-end tests verifying the full reporting pipeline as experienced by users:
/// both "Falsifying example" (original) and "Minimal counterexample" (shrunk) sections,
/// registered formatters for strings and collections, trimmed stack traces, and seed reproducibility.
/// </summary>
public class ReportingQualityE2ETests
{
    // ── "Falsifying example" + "Minimal counterexample" sections ─────────────

    [Fact]
    public async Task FailingProperty_WithShrinks_MessageContainsBothFalsifyingAndMinimalSections()
    {
        Strategy<int> strategy = Strategy.Integers<int>(0, 1000);
        ConjectureSettings settings = new() { MaxExamples = 200, Seed = 1UL, Database = false };

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            int x = strategy.Generate(data);
            if (x > 5) { throw new Exception("too large"); }
        });

        Assert.False(result.Passed);
        Assert.True(result.ShrinkCount > 0, $"Expected shrinks, got ShrinkCount={result.ShrinkCount}");
        Assert.NotNull(result.OriginalCounterexample);

        ConjectureData shrunkReplay = ConjectureData.ForRecord(result.Counterexample!);
        int shrunkValue = strategy.Generate(shrunkReplay);

        ConjectureData originalReplay = ConjectureData.ForRecord(result.OriginalCounterexample!);
        int originalValue = strategy.Generate(originalReplay);

        (string, object)[] originalParams = [("x", (object)originalValue)];
        (string, object)[] shrunkParams = [("x", (object)shrunkValue)];

        string message = CounterexampleFormatter.Format(
            originalParams,
            shrunkParams,
            result.Seed!.Value,
            result.ExampleCount,
            result.ShrinkCount);

        Assert.Contains("Falsifying example", message);
        Assert.Contains("Minimal counterexample", message);
    }

    [Fact]
    public async Task FailingProperty_ZeroShrinks_MessageContainsOnlyFalsifyingSection()
    {
        // Min == value forces no shrinking possible.
        ConjectureSettings settings = new() { MaxExamples = 10, Seed = 2UL, Database = false };

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            ulong v = data.NextInteger(42, 42);
            if (v == 42) { throw new Exception("always fails"); }
        });

        Assert.False(result.Passed);

        (string, object)[] shrunkParams = [("v", (object)42UL)];
        (string, object)[] originalParams = shrunkParams;

        string message = CounterexampleFormatter.Format(
            originalParams,
            shrunkParams,
            result.Seed!.Value,
            result.ExampleCount,
            result.ShrinkCount);

        Assert.Contains("Falsifying example", message);
        Assert.DoesNotContain("Minimal counterexample", message);
    }

    // ── Formatters: string in quotes, list in brackets ─────────────────────

    [Fact]
    public async Task FailingProperty_StringValue_AppearsInQuotesInMessage()
    {
        Strategy<string> strategy = Strategy.Strings(minLength: 3, maxLength: 5);
        ConjectureSettings settings = new() { MaxExamples = 50, Seed = 10UL, Database = false };

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            string s = strategy.Generate(data);
            if (s.Length >= 3) { throw new Exception("too long"); }
        });

        Assert.False(result.Passed);

        ConjectureData replay = ConjectureData.ForRecord(result.Counterexample!);
        string shrunkValue = strategy.Generate(replay);

        string message = CounterexampleFormatter.Format(
            [("s", (object)shrunkValue)],
            seed: result.Seed!.Value,
            exampleCount: result.ExampleCount,
            shrinkCount: result.ShrinkCount);

        // Built-in string formatter wraps value in double quotes
        Assert.Contains($"s = \"{shrunkValue}\"", message);
        Assert.DoesNotContain("String", message); // no type name fallback
    }

    [Fact]
    public async Task FailingProperty_ListValue_AppearsInBracketsInMessage()
    {
        Strategy<List<int>> strategy = Strategy.Lists(Strategy.Integers<int>(0, 5), minSize: 2, maxSize: 4);
        ConjectureSettings settings = new() { MaxExamples = 100, Seed = 20UL, Database = false };

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            List<int> xs = strategy.Generate(data);
            if (xs.Count >= 2) { throw new Exception("too many"); }
        });

        Assert.False(result.Passed);

        ConjectureData replay = ConjectureData.ForRecord(result.Counterexample!);
        List<int> shrunkValue = strategy.Generate(replay);

        string message = CounterexampleFormatter.Format(
            [("xs", (object)shrunkValue)],
            seed: result.Seed!.Value,
            exampleCount: result.ExampleCount,
            shrinkCount: result.ShrinkCount);

        // Built-in list formatter uses bracket notation: [1, 2]
        Assert.Contains("xs = [", message);
        Assert.DoesNotContain("List`1", message); // no type name fallback
    }

    // ── Stack trace excludes Conjecture internals ─────────────────────────

    [Fact]
    public async Task FailingProperty_StackTrace_DoesNotContainConjectureInternalsAfterTrim()
    {
        ConjectureSettings settings = new() { MaxExamples = 10, Seed = 30UL, Database = false };

        TestRunResult result = await TestRunner.Run(settings, _ => throw new Exception("always fails"));

        Assert.False(result.Passed);

        string trimmed = StackTraceTrimmer.Trim(result.FailureStackTrace);

        Assert.DoesNotContain("Conjecture.Core.Internal", trimmed);
        Assert.DoesNotContain("Conjecture.Xunit.Internal", trimmed);
    }

    [Fact]
    public async Task FailingProperty_StackTrace_UserFramesArePreserved()
    {
        ConjectureSettings settings = new() { MaxExamples = 10, Seed = 31UL, Database = false };

        TestRunResult result = await TestRunner.Run(settings, _ => UserFail());

        Assert.False(result.Passed);

        // FailureStackTrace (raw) should contain this class's frame
        Assert.Contains(nameof(UserFail), result.FailureStackTrace ?? "");
    }

    private static void UserFail() => throw new Exception("user-level failure");

    // ── Seed is valid for reproduction ────────────────────────────────────

    [Fact]
    public async Task FailingProperty_SeedInMessage_ReproducesSameCounterexample()
    {
        ConjectureSettings settings = new() { MaxExamples = 100, Seed = 40UL, Database = false };
        Strategy<int> strategy = Strategy.Integers<int>(0, 1000);

        TestRunResult result1 = await TestRunner.Run(settings, data =>
        {
            int x = strategy.Generate(data);
            if (x > 42) { throw new Exception("fail"); }
        });

        Assert.False(result1.Passed);
        ulong seed = result1.Seed!.Value;

        string message = CounterexampleFormatter.Format(
            [("x", (object)6)],
            seed: seed,
            exampleCount: result1.ExampleCount,
            shrinkCount: result1.ShrinkCount);

        Assert.Contains($"Reproduce with: [Property(Seed = 0x{seed:X})]", message);

        // Running again with the extracted seed must reproduce the same shrunk counterexample.
        ConjectureSettings reproSettings = new() { MaxExamples = 100, Seed = seed, Database = false };
        TestRunResult result2 = await TestRunner.Run(reproSettings, data =>
        {
            int x = strategy.Generate(data);
            if (x > 42) { throw new Exception("fail"); }
        });

        Assert.False(result2.Passed);
        Assert.Equal(result1.Counterexample!.Count, result2.Counterexample!.Count);

        for (int i = 0; i < result1.Counterexample.Count; i++)
        {
            Assert.Equal(result1.Counterexample[i].Value, result2.Counterexample[i].Value);
        }
    }

    [Fact]
    public async Task FailingProperty_SeedFormat_IsHexadecimalInReproduceLine()
    {
        const ulong testSeed = 0xA7F3B2E1UL;
        ConjectureSettings settings = new() { MaxExamples = 5, Seed = testSeed, Database = false };

        TestRunResult result = await TestRunner.Run(settings, _ => throw new Exception("always fails"));

        string message = CounterexampleFormatter.Format(
            [("x", (object)1)],
            seed: result.Seed!.Value,
            exampleCount: result.ExampleCount,
            shrinkCount: result.ShrinkCount);

        Assert.Contains("Reproduce with: [Property(Seed = 0xA7F3B2E1)]", message);
    }
}