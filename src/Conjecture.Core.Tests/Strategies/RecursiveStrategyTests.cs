// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core;
using Conjecture.Core.Internal;

namespace Conjecture.Core.Tests.Strategies;

public class RecursiveStrategyTests
{
    private static ConjectureData MakeData(ulong seed = 42UL) =>
        ConjectureData.ForGeneration(new SplittableRandom(seed));

    private static RecursiveStrategy<int> IntRecursive(int maxDepth) =>
        new(Generate.Just(0),
            self => Generate.OneOf(Generate.Just(0), self.Select(n => n + 1)),
            maxDepth);

    [Fact]
    public void RecursiveStrategy_WithMaxDepth3_GeneratesValuesIn0To3()
    {
        RecursiveStrategy<int> strategy = IntRecursive(3);
        HashSet<int> seen = [];

        for (ulong seed = 0; seed < 50; seed++)
        {
            ConjectureData data = MakeData(seed);
            for (int i = 0; i < 20; i++)
            {
                int value = strategy.Generate(data);
                Assert.InRange(value, 0, 3);
                seen.Add(value);
            }
        }

        HashSet<int> expected = [0, 1, 2, 3];
        Assert.Equal(expected, seen);
    }

    [Fact]
    public void RecursiveStrategy_AtMaxDepth0_OnlyReturnsBaseCase()
    {
        RecursiveStrategy<int> strategy = IntRecursive(0);
        ConjectureData data = MakeData();

        for (int i = 0; i < 50; i++)
        {
            Assert.Equal(0, strategy.Generate(data));
        }
    }

    [Fact]
    public void RecursiveStrategy_AtMaxDepth1_GeneratesValuesIn0To1()
    {
        RecursiveStrategy<int> strategy = IntRecursive(1);
        ConjectureData data = MakeData();

        for (int i = 0; i < 200; i++)
        {
            Assert.InRange(strategy.Generate(data), 0, 1);
        }
    }

    [Fact]
    public void RecursiveStrategy_WhenDepthIsZero_ReturnsBaseCase()
    {
        RecursiveStrategy<int> strategy = IntRecursive(3);
        // Force depth = 0 via recorded stream — simulates shrunken counterexample
        ConjectureData data = ConjectureData.ForRecord([IRNode.ForInteger(0UL, 0UL, 3UL)]);

        int result = strategy.Generate(data);

        Assert.Equal(0, result);
    }

    [Fact]
    public void RecursiveStrategy_NegativeMaxDepth_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RecursiveStrategy<int>(Generate.Just(0), self => self, -1));
    }

    [Fact]
    public void RecursiveStrategy_WhenBaseCaseThrowsAssumption_PropagatesException()
    {
        Strategy<int> badBase = Generate.Just(0).Where(_ => false);
        RecursiveStrategy<int> strategy = new(badBase, self => self, 0);
        ConjectureData data = MakeData();

        Assert.Throws<UnsatisfiedAssumptionException>(() => strategy.Generate(data));
    }

    [Fact]
    public void RecursiveStrategy_WithComplexType_GeneratesValidExprTrees()
    {
        Strategy<Expr> numStrategy = Generate.Just<Expr>(new Num(1));
        RecursiveStrategy<Expr> strategy = new(
            numStrategy,
            self => Generate.OneOf(numStrategy, self.Select(e => (Expr)new Add(e, e))),
            maxDepth: 3);
        ConjectureData data = MakeData();

        for (int i = 0; i < 100; i++)
        {
            Expr expr = strategy.Generate(data);
            Assert.InRange(ExprDepth(expr), 0, 3);
        }
    }

    private static int ExprDepth(Expr expr) => expr switch
    {
        Num => 0,
        Add add => 1 + Math.Max(ExprDepth(add.Left), ExprDepth(add.Right)),
        _ => throw new InvalidOperationException($"Unknown Expr type: {expr.GetType().Name}")
    };

    private abstract class Expr { }
    private sealed class Num(int value) : Expr { internal int Value { get; } = value; }
    private sealed class Add(Expr left, Expr right) : Expr
    {
        internal Expr Left { get; } = left;
        internal Expr Right { get; } = right;
    }
}