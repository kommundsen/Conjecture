// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core;
using Conjecture.Core.Internal;

namespace Conjecture.Core.Tests.Strategies;

public class WhereStrategyTests
{
    private static ConjectureData MakeData(ulong seed = 42UL) =>
        ConjectureData.ForGeneration(new SplittableRandom(seed));

    [Fact]
    public void Where_FiltersOutput()
    {
        Strategy<int> strategy = Strategy.Integers<int>(0, 10).Where(x => x % 2 == 0);
        ConjectureData data = MakeData();
        for (int i = 0; i < 50; i++)
        {
            Assert.True(strategy.Generate(data) % 2 == 0, "Where() returned a value that didn't satisfy the predicate");
        }
    }

    [Fact]
    public void Where_AllowsValuesMatchingPredicate()
    {
        Strategy<bool> strategy = Strategy.Booleans().Where(x => x);
        ConjectureData data = MakeData();
        for (int i = 0; i < 20; i++)
        {
            Assert.True(strategy.Generate(data));
        }
    }

    [Fact]
    public void Where_ExhaustedBudget_MarksInvalid()
    {
        ConjectureData data = MakeData();
        Strategy<int> strategy = Strategy.Integers<int>(0, 10).Where(_ => false);
        Assert.ThrowsAny<Exception>((Action)(() => strategy.Generate(data)));
        Assert.Equal(Status.Invalid, data.Status);
    }

    [Fact]
    public void Where_RejectedAttempts_RolledBackFromIRNodes()
    {
        ConjectureData data = ConjectureData.ForRecord(
        [
            IRNode.ForInteger(0UL, 0UL, 1UL),
            IRNode.ForInteger(1UL, 0UL, 1UL),
        ]);

        Strategy<int> strategy = Strategy.Integers<int>(0, 1).Where(x => x == 1);
        int result = strategy.Generate(data);

        Assert.Equal(1, result);
        Assert.Single(data.IRNodes);
    }

    [Fact]
    public void Where_SingleAcceptedDraw_AllocatesAtMostBaselinePlus16Bytes()
    {
        Strategy<int> baseline = Strategy.Integers<int>(0, 100);
        Strategy<int> where = Strategy.Integers<int>(0, 100).Where(_ => true);

        ConjectureData warmupData = ConjectureData.ForRecord([IRNode.ForInteger(42UL, 0UL, 100UL)]);
        baseline.Generate(warmupData);
        ConjectureData warmupData2 = ConjectureData.ForRecord([IRNode.ForInteger(42UL, 0UL, 100UL)]);
        where.Generate(warmupData2);

        ConjectureData baselineData = ConjectureData.ForRecord([IRNode.ForInteger(42UL, 0UL, 100UL)]);
        long beforeBaseline = GC.GetAllocatedBytesForCurrentThread();
        baseline.Generate(baselineData);
        long afterBaseline = GC.GetAllocatedBytesForCurrentThread();
        long baselineAlloc = afterBaseline - beforeBaseline;

        ConjectureData whereData = ConjectureData.ForRecord([IRNode.ForInteger(42UL, 0UL, 100UL)]);
        long beforeWhere = GC.GetAllocatedBytesForCurrentThread();
        where.Generate(whereData);
        long afterWhere = GC.GetAllocatedBytesForCurrentThread();
        long whereAlloc = afterWhere - beforeWhere;

        Assert.True(
            whereAlloc <= baselineAlloc + 16,
            $"Expected where alloc ({whereAlloc}) <= baseline alloc ({baselineAlloc}) + 16");
    }
}