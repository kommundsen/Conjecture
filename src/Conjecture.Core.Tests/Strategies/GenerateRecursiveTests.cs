// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core;
using Conjecture.Core.Internal;

namespace Conjecture.Core.Tests.Strategies;

public class GenerateRecursiveTests
{
    private static ConjectureData MakeData(ulong seed = 42UL) =>
        ConjectureData.ForGeneration(new SplittableRandom(seed));

    [Fact]
    public void Recursive_WithExplicitMaxDepth_GeneratesValuesInRange()
    {
        Strategy<int> strategy = Strategy.Recursive<int>(
            Strategy.Just(0),
            self => Strategy.OneOf(Strategy.Just(0), self.Select(n => n + 1)),
            maxDepth: 3);

        for (ulong seed = 0; seed < 50; seed++)
        {
            ConjectureData data = MakeData(seed);
            for (int i = 0; i < 20; i++)
            {
                Assert.InRange(strategy.Generate(data), 0, 3);
            }
        }
    }

    [Fact]
    public void Recursive_DefaultMaxDepth_GeneratesValuesUpTo5()
    {
        Strategy<int> strategy = Strategy.Recursive<int>(
            Strategy.Just(0),
            self => Strategy.OneOf(Strategy.Just(0), self.Select(n => n + 1)));

        for (ulong seed = 0; seed < 50; seed++)
        {
            ConjectureData data = MakeData(seed);
            for (int i = 0; i < 20; i++)
            {
                Assert.InRange(strategy.Generate(data), 0, 5);
            }
        }
    }

    [Fact]
    public void Recursive_NullBaseCase_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            Strategy.Recursive<int>(null!, self => self));
    }

    [Fact]
    public void Recursive_NullRecursive_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            Strategy.Recursive<int>(Strategy.Just(0), null!));
    }

    [Fact]
    public void Recursive_ComposesWithSelect()
    {
        Strategy<string> strategy = Strategy.Recursive<int>(
            Strategy.Just(0),
            self => Strategy.OneOf(Strategy.Just(0), self.Select(n => n + 1)),
            maxDepth: 3)
            .Select(n => n.ToString());

        ConjectureData data = MakeData();
        for (int i = 0; i < 50; i++)
        {
            string result = strategy.Generate(data);
            Assert.True(int.TryParse(result, out _));
        }
    }

    [Fact]
    public void Recursive_ComposesWithWhere()
    {
        Strategy<int> strategy = Strategy.Recursive<int>(
            Strategy.Just(0),
            self => Strategy.OneOf(Strategy.Just(0), self.Select(n => n + 1)),
            maxDepth: 3)
            .Where(n => n % 2 == 0);

        ConjectureData data = MakeData();
        for (int i = 0; i < 50; i++)
        {
            Assert.True(strategy.Generate(data) % 2 == 0);
        }
    }

    [Fact]
    public void Recursive_ComposesWithSelectMany()
    {
        Strategy<(int, int)> strategy = Strategy.Recursive<int>(
            Strategy.Just(0),
            self => Strategy.OneOf(Strategy.Just(0), self.Select(n => n + 1)),
            maxDepth: 2)
            .SelectMany(n => Strategy.Just(n * 10).Select(m => (n, m)));

        ConjectureData data = MakeData();
        for (int i = 0; i < 50; i++)
        {
            (int n, int m) = strategy.Generate(data);
            Assert.Equal(n * 10, m);
        }
    }

    [Fact]
    public void Recursive_InGenCompose_WorksViaCtxGenerate()
    {
        Strategy<int> recursive = Strategy.Recursive<int>(
            Strategy.Just(0),
            self => Strategy.OneOf(Strategy.Just(0), self.Select(n => n + 1)),
            maxDepth: 3);

        Strategy<int> composed = Strategy.Compose(ctx => ctx.Generate(recursive));

        ConjectureData data = MakeData();
        for (int i = 0; i < 50; i++)
        {
            Assert.InRange(composed.Generate(data), 0, 3);
        }
    }
}