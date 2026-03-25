using Conjecture.Core;
using Conjecture.Core.Internal;
using Conjecture.Core.Generation;

namespace Conjecture.Tests.Generation;

public class WhereStrategyTests
{
    private static ConjectureData MakeData(ulong seed = 42UL) =>
        ConjectureData.ForGeneration(new SplittableRandom(seed));

    [Fact]
    public void Where_FiltersOutput()
    {
        var strategy = Gen.Integers<int>(0, 10).Where(x => x % 2 == 0);
        var data = MakeData();
        for (var i = 0; i < 50; i++)
            Assert.True(strategy.Next(data) % 2 == 0, "Where() returned a value that didn't satisfy the predicate");
    }

    [Fact]
    public void Where_AllowsValuesMatchingPredicate()
    {
        var strategy = Gen.Booleans().Where(x => x);
        var data = MakeData();
        for (var i = 0; i < 20; i++)
            Assert.True(strategy.Next(data));
    }

    [Fact]
    public void Where_ExhaustedBudget_MarksInvalid()
    {
        var data = MakeData();
        var strategy = Gen.Integers<int>(0, 10).Where(_ => false);
        Assert.ThrowsAny<Exception>((Action)(() => strategy.Next(data)));
        Assert.Equal(Status.Invalid, data.Status);
    }
}
