// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core;
using Conjecture.Core.Internal;

namespace Conjecture.Core.Tests.Strategies;

public class IndexStrategyTests
{
    private static ConjectureData MakeData(ulong seed = 42UL) =>
        ConjectureData.ForGeneration(new SplittableRandom(seed));

    [Fact]
    public void Indices_ReturnsValuesWithinMaxValue()
    {
        Strategy<Index> strategy = Strategy.Indices();
        ConjectureData data = MakeData();

        for (int i = 0; i < 200; i++)
        {
            Index index = strategy.Generate(data);
            Assert.True(index.Value >= 0, $"Expected Value >= 0 but got {index.Value}");
            Assert.True(index.Value <= 100, $"Expected Value <= 100 but got {index.Value}");
        }
    }

    [Fact]
    public void Indices_FromEndBranchReachable()
    {
        Strategy<Index> strategy = Strategy.Indices();
        ConjectureData data = MakeData();
        bool seenFromEnd = false;
        bool seenFromStart = false;

        for (int i = 0; i < 500; i++)
        {
            Index index = strategy.Generate(data);
            if (index.IsFromEnd)
            {
                seenFromEnd = true;
            }
            else
            {
                seenFromStart = true;
            }

            if (seenFromEnd && seenFromStart)
            {
                break;
            }
        }

        Assert.True(seenFromEnd, "No from-end Index was generated in 500 draws");
        Assert.True(seenFromStart, "No from-start Index was generated in 500 draws");
    }

    [Fact]
    public async Task Indices_ShrinksTowardZero()
    {
        Strategy<Index> strategy = Strategy.Indices();
        ConjectureSettings settings = new() { MaxExamples = 200, Seed = 1UL };

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            Index index = strategy.Generate(data);
            throw new Exception($"always fails: {index}");
        });

        Assert.False(result.Passed);
        ConjectureData replay = ConjectureData.ForRecord(result.Counterexample!);
        Index shrunk = strategy.Generate(replay);
        Assert.Equal(0, shrunk.Value);
        Assert.False(shrunk.IsFromEnd);
    }

    [Fact]
    public void Indices_RespectsCustomMaxValue()
    {
        Strategy<Index> strategy = Strategy.Indices(10);
        ConjectureData data = MakeData();

        for (int i = 0; i < 200; i++)
        {
            Index index = strategy.Generate(data);
            Assert.True(index.Value <= 10, $"Expected Value <= 10 but got {index.Value}");
        }
    }

    [Fact]
    public void Indices_ThrowsOnNegativeMaxValue()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Strategy.Indices(-1));
    }
}
