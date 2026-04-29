// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core;
using Conjecture.Core.Internal;

namespace Conjecture.Core.Tests.Strategies;

public class SelectManyStrategyTests
{
    private static ConjectureData MakeData(ulong seed = 42UL) =>
        ConjectureData.ForGeneration(new SplittableRandom(seed));

    [Fact]
    public void SelectMany_DependentGeneration()
    {
        var strategy = Strategy.Integers<int>(1, 5).SelectMany(n => Strategy.Integers<int>(0, n));
        var data = MakeData();
        for (var i = 0; i < 100; i++)
        {
            Assert.InRange(strategy.Generate(data), 0, 5);
        }
    }

    [Fact]
    public void SelectMany_QuerySyntax()
    {
        var strategy =
            from x in Strategy.Integers<int>(1, 3)
            from y in Strategy.Integers<int>(1, x)
            select x * 10 + y;

        var data = MakeData();
        for (var i = 0; i < 100; i++)
        {
            var result = strategy.Generate(data);
            // Valid results: 11, 21, 22, 31, 32, 33
            Assert.Contains(result, new[] { 11, 21, 22, 31, 32, 33 });
        }
    }

    [Fact]
    public void SelectMany_RecordsMultipleIRNodes()
    {
        ConjectureData data = MakeData();
        Strategy.Integers<int>(1, 5).SelectMany(n => Strategy.Integers<int>(0, n)).Generate(data);
        Assert.True(data.IRNodes.Count >= 2, $"Expected >= 2 IR nodes, got {data.IRNodes.Count}");
    }

    [Fact]
    public void SelectMany_ShrinkingPreservesDependency()
    {
        Strategy<int> strategy = Strategy.Integers<int>(1, 5).SelectMany(n => Strategy.Integers<int>(0, n));
        ConjectureData initial = MakeData(42UL);
        strategy.Generate(initial);
        Assert.Equal(2, initial.IRNodes.Count);

        IRNode[] replayNodes =
        [
            IRNode.ForInteger(2UL, 1UL, 5UL),
            IRNode.ForInteger(0UL, 0UL, 2UL),
        ];
        ConjectureData replayed = ConjectureData.ForRecord(replayNodes);
        int result = strategy.Generate(replayed);
        Assert.InRange(result, 0, 2);
    }

    [Fact]
    public void SelectMany_DirectPath_GeneratesCorrectly()
    {
        Strategy<int> strategy = Strategy.Integers<int>(1, 5)
            .SelectMany(static (n, d) => (int)d.NextInteger(0UL, (ulong)n));

        for (int i = 0; i < 100; i++)
        {
            ConjectureData data = MakeData((ulong)i);
            int result = strategy.Generate(data);
            Assert.InRange(result, 0, 5);
            Assert.True(data.IRNodes.Count >= 2, $"Expected >= 2 IR nodes, got {data.IRNodes.Count}");
        }
    }

    [Fact]
    public void SelectMany_DirectPath_AllocationBudget()
    {
        Strategy<int> baseline = Strategy.Integers<int>();
        Strategy<int> directPath = Strategy.Integers<int>(0, 100)
            .SelectMany(static (x, d) => (int)d.NextInteger(0UL, (ulong)x));

        // warm up
        for (int i = 0; i < 3; i++)
        {
            baseline.Generate(MakeData((ulong)i));
            directPath.Generate(MakeData((ulong)i));
        }

        long baselineTotal = 0L;
        long directPathTotal = 0L;
        int iterations = 10;

        for (int i = 0; i < iterations; i++)
        {
            ConjectureData bData = MakeData((ulong)(i + 100));
            long before = GC.GetTotalAllocatedBytes(precise: true);
            baseline.Generate(bData);
            long after = GC.GetTotalAllocatedBytes(precise: true);
            baselineTotal += after - before;
        }

        for (int i = 0; i < iterations; i++)
        {
            ConjectureData dData = MakeData((ulong)(i + 200));
            long before = GC.GetTotalAllocatedBytes(precise: true);
            directPath.Generate(dData);
            long after = GC.GetTotalAllocatedBytes(precise: true);
            directPathTotal += after - before;
        }

        long baselineAlloc = baselineTotal / iterations;
        long directPathAlloc = directPathTotal / iterations;
        Assert.True(
            directPathAlloc <= baselineAlloc + 16L,
            $"Direct path allocated {directPathAlloc}B avg, baseline {baselineAlloc}B avg (budget: baseline + 16)");
    }
}