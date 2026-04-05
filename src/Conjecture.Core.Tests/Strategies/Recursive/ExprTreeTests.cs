// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core;
using Conjecture.Core.Internal;

namespace Conjecture.Core.Tests.Strategies.Recursive;

public class ExprTreeTests
{
    private abstract class Expr { }

    private sealed class Literal(int value) : Expr
    {
        internal int Value { get; } = value;
    }

    private sealed class Add(Expr left, Expr right) : Expr
    {
        internal Expr Left { get; } = left;
        internal Expr Right { get; } = right;
    }

    private sealed class Mul(Expr left, Expr right) : Expr
    {
        internal Expr Left { get; } = left;
        internal Expr Right { get; } = right;
    }

    private static int Eval(Expr expr)
    {
        return expr switch
        {
            Literal lit => lit.Value,
            Add add => Eval(add.Left) + Eval(add.Right),
            Mul mul => Eval(mul.Left) * Eval(mul.Right),
            _ => throw new InvalidOperationException($"Unknown Expr: {expr.GetType().Name}"),
        };
    }

    // Planted bug: Mul uses subtraction instead of multiplication.
    // Mul(Literal(0), Literal(1)) => 0 - 1 = -1 < 0, violating eval >= 0.
    private static int FaultyEval(Expr expr)
    {
        return expr switch
        {
            Literal lit => lit.Value,
            Add add => FaultyEval(add.Left) + FaultyEval(add.Right),
            Mul mul => FaultyEval(mul.Left) - FaultyEval(mul.Right),
            _ => throw new InvalidOperationException($"Unknown Expr: {expr.GetType().Name}"),
        };
    }

    private static int ExprDepth(Expr expr)
    {
        return expr switch
        {
            Literal => 0,
            Add add => 1 + Math.Max(ExprDepth(add.Left), ExprDepth(add.Right)),
            Mul mul => 1 + Math.Max(ExprDepth(mul.Left), ExprDepth(mul.Right)),
            _ => throw new InvalidOperationException($"Unknown Expr: {expr.GetType().Name}"),
        };
    }

    private static Strategy<Expr> ExprStrategy(int maxDepth)
    {
        Strategy<Expr> baseCase = Generate.Integers<int>(0, 10).Select(n => (Expr)new Literal(n));
        return Generate.Recursive<Expr>(
            baseCase,
            self => Generate.OneOf(
                baseCase,
                Generate.Tuples(self, self).Select(t => (Expr)new Add(t.Item1, t.Item2)),
                Generate.Tuples(self, self).Select(t => (Expr)new Mul(t.Item1, t.Item2))),
            maxDepth);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(5)]
    public async Task ExprTree_AllGeneratedTrees_HaveDepthAtMostMaxDepth(int maxDepth)
    {
        ConjectureSettings settings = new() { MaxExamples = 200, Seed = 1UL };
        Strategy<Expr> strategy = ExprStrategy(maxDepth);

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            Expr expr = strategy.Generate(data);
            int depth = ExprDepth(expr);
            if (depth > maxDepth)
            {
                throw new InvalidOperationException($"Generated tree depth {depth} exceeds maxDepth {maxDepth}");
            }
        });

        Assert.True(result.Passed);
    }

    [Fact]
    public async Task ExprTree_FaultyEval_FindsCounterexample()
    {
        ConjectureSettings settings = new() { MaxExamples = 200, Seed = 1UL };
        Strategy<Expr> strategy = ExprStrategy(maxDepth: 5);

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            Expr expr = strategy.Generate(data);
            if (FaultyEval(expr) < 0)
            {
                throw new InvalidOperationException($"FaultyEval({expr.GetType().Name}) < 0");
            }
        });

        Assert.False(result.Passed);
        Assert.NotNull(result.Counterexample);
    }

    [Fact]
    public async Task ExprTree_SignedLiteralBug_ShrinksToBaseCase()
    {
        // Reducing the depth-selection node to 0 always yields a failing Literal(negative),
        // so the shrinker reaches depth 0 without needing to restructure multiple IR nodes.
        ConjectureSettings settings = new() { MaxExamples = 200, Seed = 1UL };
        Strategy<Expr> signedBase = Generate.Integers<int>(-10, 10).Select(n => (Expr)new Literal(n));
        Strategy<Expr> signedStrategy = Generate.Recursive<Expr>(
            signedBase,
            self => Generate.OneOf(
                signedBase,
                Generate.Tuples(self, self).Select(t => (Expr)new Add(t.Item1, t.Item2)),
                Generate.Tuples(self, self).Select(t => (Expr)new Mul(t.Item1, t.Item2))),
            maxDepth: 5);
        int? lastFailingDepth = null;

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            Expr expr = signedStrategy.Generate(data);
            if (Eval(expr) < 0)
            {
                lastFailingDepth = ExprDepth(expr);
                throw new InvalidOperationException("eval < 0");
            }
        });

        Assert.False(result.Passed);
        Assert.NotNull(lastFailingDepth);
        Assert.True(lastFailingDepth <= 1,
            $"Shrunk tree depth {lastFailingDepth} should be 0 (Literal) or 1 (single-level)");
    }

    [Fact]
    public async Task ExprTree_WithMaxDepth20_DoesNotStackOverflow()
    {
        ConjectureSettings settings = new() { MaxExamples = 100, Seed = 7UL };
        Strategy<Expr> strategy = ExprStrategy(maxDepth: 20);

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            Expr expr = strategy.Generate(data);
            _ = Eval(expr);
        });

        Assert.True(result.Passed);
    }
}
