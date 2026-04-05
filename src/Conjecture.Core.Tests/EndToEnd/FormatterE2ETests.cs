// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core;
using Conjecture.Core.Internal;

namespace Conjecture.Core.Tests.EndToEnd;

/// <summary>
/// End-to-end tests verifying that the formatter pipeline integrates with the
/// test runner: example counts, shrink counts, and registered formatters all
/// appear correctly in failure output.
/// </summary>
public class FormatterE2ETests
{
    // --- Example count ---

    [Fact]
    public async Task FailingProperty_ExampleCount_AppearsInFailureMessage()
    {
        Strategy<int> strategy = Generate.Integers<int>(0, 100);
        ConjectureSettings settings = new() { MaxExamples = 50, Seed = 1UL };

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            int x = strategy.Generate(data);
            if (x > 5) { throw new Exception("fail"); }
        });

        Assert.False(result.Passed);
        string msg = CounterexampleFormatter.Format(
            [("x", (object)6)],
            seed: result.Seed!.Value,
            exampleCount: result.ExampleCount,
            shrinkCount: result.ShrinkCount);

        Assert.Contains($"Falsifying example found after {result.ExampleCount} examples", msg);
    }

    // --- Shrink count ---

    [Fact]
    public async Task FailingProperty_ShrinkCount_AppearsInFailureMessage()
    {
        Strategy<int> strategy = Generate.Integers<int>(0, 100);
        ConjectureSettings settings = new() { MaxExamples = 100, Seed = 2UL };

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            int x = strategy.Generate(data);
            if (x > 5) { throw new Exception("fail"); }
        });

        Assert.False(result.Passed);
        string msg = CounterexampleFormatter.Format(
            [("x", (object)6)],
            seed: result.Seed!.Value,
            exampleCount: result.ExampleCount,
            shrinkCount: result.ShrinkCount);

        Assert.Contains($"Shrunk {result.ShrinkCount} times from original", msg);
    }

    [Fact]
    public async Task FailingProperty_WithLargeInitialValue_ShrinkCountIsPositive()
    {
        // min=50 forces values far above threshold; shrinker must make several passes to reach 50
        Strategy<int> strategy = Generate.Integers<int>(50, 100);
        ConjectureSettings settings = new() { MaxExamples = 100, Seed = 3UL };

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            int x = strategy.Generate(data);
            if (x > 5) { throw new Exception("fail"); }
        });

        Assert.False(result.Passed);
        Assert.True(result.ShrinkCount > 0, $"Expected positive shrink count, got {result.ShrinkCount}");
    }

    // --- Built-in formatters ---

    [Fact]
    public void FailureMessage_IntValue_FormattedAsNumericLiteral()
    {
        // BuiltInFormatters registers int; value must appear as "42", not the type name
        string msg = CounterexampleFormatter.Format(
            [("x", (object)42)],
            seed: 0UL, exampleCount: 1, shrinkCount: 0);

        Assert.Contains("x = 42", msg);
        Assert.DoesNotContain("Int32", msg);
    }

    [Fact]
    public async Task FailureMessage_WithRunnerResult_IntValuesUseBuiltInFormatter()
    {
        Strategy<int> strategy = Generate.Integers<int>(0, 100);
        ConjectureSettings settings = new() { MaxExamples = 100, Seed = 4UL };

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            int x = strategy.Generate(data);
            if (x > 5) { throw new Exception("fail"); }
        });

        Assert.False(result.Passed);
        ConjectureData replay = ConjectureData.ForRecord(result.Counterexample!);
        int shrunkValue = strategy.Generate(replay);

        string msg = CounterexampleFormatter.Format(
            [("x", (object)shrunkValue)],
            seed: result.Seed!.Value,
            exampleCount: result.ExampleCount,
            shrinkCount: result.ShrinkCount);

        Assert.Contains($"x = {shrunkValue}", msg);
        Assert.DoesNotContain("Int32", msg);
    }

    // --- Custom formatter ---

    [Fact]
    public async Task CustomFormatter_RegisteredViaFormatterRegistry_AppearsInFailureOutput()
    {
        FormatterRegistry.Register<CustomPoint>(new CustomPointFormatter());
        try
        {
            Strategy<CustomPoint> strategy = Generate.Just(new CustomPoint(3, 7));
            ConjectureSettings settings = new() { MaxExamples = 10, Seed = 5UL };

            TestRunResult result = await TestRunner.Run(settings, data =>
            {
                CustomPoint pt = strategy.Generate(data);
                if (pt.X > 0) { throw new Exception("fail"); }
            });

            Assert.False(result.Passed);
            ConjectureData replay = ConjectureData.ForRecord(result.Counterexample!);
            CustomPoint shrunk = strategy.Generate(replay);

            string msg = CounterexampleFormatter.Format(
                [("pt", (object)shrunk)],
                seed: result.Seed!.Value,
                exampleCount: result.ExampleCount,
                shrinkCount: result.ShrinkCount);

            Assert.Contains("pt = Point(3, 7)", msg);
        }
        finally
        {
            FormatterRegistry.Register<CustomPoint>(null);
        }
    }

    [Fact]
    public void CustomFormatter_ReplacesDefault_ToStringFallback()
    {
        // Before registration, falls back to ToString(); after, uses formatter
        FormatterRegistry.Register<NamedThing>(null); // ensure no prior registration
        NamedThing value = new("widget");
        (string, object)[] paramsBefore = [("v", (object)value)];
        string msgBefore = CounterexampleFormatter.Format(paramsBefore, seed: 0UL, exampleCount: 1, shrinkCount: 0);
        Assert.Contains("v = widget", msgBefore); // ToString fallback

        FormatterRegistry.Register<NamedThing>(new NamedThingFormatter());
        try
        {
            (string, object)[] paramsAfter = [("v", (object)value)];
            string msgAfter = CounterexampleFormatter.Format(paramsAfter, seed: 0UL, exampleCount: 1, shrinkCount: 0);
            Assert.Contains("v = [NamedThing: widget]", msgAfter);
        }
        finally
        {
            FormatterRegistry.Register<NamedThing>(null);
        }
    }

    private sealed class CustomPoint(int x, int y)
    {
        public int X => x;
        public int Y => y;
    }

    private sealed class CustomPointFormatter : IStrategyFormatter<CustomPoint>
    {
        public string Format(CustomPoint value) => $"Point({value.X}, {value.Y})";
    }

    private sealed class NamedThing(string name)
    {
        public override string ToString() => name;
    }

    private sealed class NamedThingFormatter : IStrategyFormatter<NamedThing>
    {
        public string Format(NamedThing value) => $"[NamedThing: {value}]";
    }
}