using System.Runtime.CompilerServices;
using Conjecture.Core;
using Conjecture.Core.Internal;

namespace Conjecture.Tests.EndToEnd;

public class AdvancedShrinkingE2ETests
{
    // ── Float > 100.0 shrinks to smallest double above 100.0 ──────────────────

    [Fact]
    public async Task Float_GreaterThan100_ShrinksToSmallestDoubleAbove100()
    {
        ConjectureSettings settings = new() { MaxExamples = 200, Seed = 1UL, UseDatabase = false };

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            ulong raw = data.NextFloat64(0UL, ulong.MaxValue);
            double x = Unsafe.BitCast<ulong, double>(raw);
            if (x > 100.0)
            {
                throw new Exception("too large");
            }
        });

        Assert.False(result.Passed);
        ulong shrunkBits = result.Counterexample![0].Value;
        double shrunkValue = Unsafe.BitCast<ulong, double>(shrunkBits);

        Assert.True(shrunkValue > 100.0, $"Shrunk value {shrunkValue} must be > 100.0");
        Assert.False(double.IsNaN(shrunkValue), "Shrunk value must not be NaN");
        Assert.False(double.IsInfinity(shrunkValue), "Shrunk value must not be infinite");
        Assert.Equal(Math.BitIncrement(100.0), shrunkValue);
    }

    // ── String containing "err" shrinks to exactly "err" ─────────────────────

    [Fact]
    public async Task String_ContainsErr_ShrinksToExactlyErr()
    {
        Strategy<string> strategy = Generate.Strings(alphabet: "er");
        ConjectureSettings settings = new() { MaxExamples = 50, Seed = 2UL, UseDatabase = false };

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            string s = strategy.Generate(data);
            if (s.Contains("err", StringComparison.Ordinal))
            {
                throw new Exception("contains err");
            }
        });

        Assert.False(result.Passed);
        ConjectureData replay = ConjectureData.ForRecord(result.Counterexample!);
        string shrunk = strategy.Generate(replay);

        Assert.Equal("err", shrunk);
    }

    // ── List with sum > 100 shrinks to minimal single-element list ────────────

    [Fact]
    public async Task List_SumGreaterThan100_ShrinksToSingleElementList()
    {
        Strategy<List<int>> strategy = Generate.Lists(Generate.Integers<int>(0, 200));
        ConjectureSettings settings = new() { MaxExamples = 200, Seed = 3UL, UseDatabase = false };

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            List<int> xs = strategy.Generate(data);
            if (xs.Sum() > 100)
            {
                throw new Exception("sum too large");
            }
        });

        Assert.False(result.Passed);
        ConjectureData replay = ConjectureData.ForRecord(result.Counterexample!);
        List<int> shrunk = strategy.Generate(replay);

        int single = Assert.Single(shrunk);
        Assert.Equal(101, single);
    }

    // ── Two-param sum > 100 shrinks to lex-minimal pair (0, 101) ─────────────

    [Fact]
    public async Task TwoParams_SumGreaterThan100_ShrinksToLexMinimalPair()
    {
        ConjectureSettings settings = new() { MaxExamples = 200, Seed = 4UL, UseDatabase = false };

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            ulong a = data.NextInteger(0, 200);
            ulong b = data.NextInteger(0, 200);
            if (a + b > 100)
            {
                throw new Exception("sum too large");
            }
        });

        Assert.False(result.Passed);
        Assert.Equal(2, result.Counterexample!.Count);
        ulong shrunkA = result.Counterexample[0].Value;
        ulong shrunkB = result.Counterexample[1].Value;
        Assert.True(shrunkA + shrunkB > 100, $"Sum {shrunkA + shrunkB} must still be > 100");
        Assert.Equal(0UL, shrunkA);
        Assert.Equal(101UL, shrunkB);
    }

    // ── Deadline setting does not prevent correct shrinking ──────────────────

    [Fact]
    public async Task WithDeadline_PropertyFails_StillShrinksToMinimal()
    {
        ConjectureSettings settings = new()
        {
            MaxExamples = 200,
            Seed = 5UL,
            Deadline = TimeSpan.FromSeconds(30),
            UseDatabase = false,
        };

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            ulong v = data.NextInteger(0, 10000);
            if (v >= 42)
            {
                throw new Exception("too large");
            }
        });

        Assert.False(result.Passed);
        Assert.Equal(42UL, result.Counterexample![0].Value);
    }
}
