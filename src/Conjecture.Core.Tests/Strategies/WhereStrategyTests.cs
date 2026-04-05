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
        var strategy = Generate.Integers<int>(0, 10).Where(x => x % 2 == 0);
        var data = MakeData();
        for (var i = 0; i < 50; i++)
        {
            Assert.True(strategy.Generate(data) % 2 == 0, "Where() returned a value that didn't satisfy the predicate");
        }
    }

    [Fact]
    public void Where_AllowsValuesMatchingPredicate()
    {
        var strategy = Generate.Booleans().Where(x => x);
        var data = MakeData();
        for (var i = 0; i < 20; i++)
        {
            Assert.True(strategy.Generate(data));
        }
    }

    [Fact]
    public void Where_ExhaustedBudget_MarksInvalid()
    {
        var data = MakeData();
        var strategy = Generate.Integers<int>(0, 10).Where(_ => false);
        Assert.ThrowsAny<Exception>((Action)(() => strategy.Generate(data)));
        Assert.Equal(Status.Invalid, data.Status);
    }
}