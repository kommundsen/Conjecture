using Conjecture.Core;
using Conjecture.Core.Internal;
using Conjecture.Core.Generation;

namespace Conjecture.Tests.Strategies;

public class EnumStrategyTests
{
    private static ConjectureData MakeData(ulong seed = 42UL) =>
        ConjectureData.ForGeneration(new SplittableRandom(seed));

    private enum Colour { Red, Green, Blue }

    [Fact]
    public void Enums_ReturnsOnlyValidDayOfWeekValues()
    {
        var strategy = Gen.Enums<DayOfWeek>();
        var data = MakeData();
        var valid = Enum.GetValues<DayOfWeek>();

        for (var i = 0; i < 100; i++)
        {
            Assert.Contains(strategy.Next(data), valid);
        }
    }

    [Fact]
    public void Enums_CoversAllDayOfWeekMembersOverManyDraws()
    {
        var strategy = Gen.Enums<DayOfWeek>();
        var data = MakeData();
        var seen = new HashSet<DayOfWeek>();

        for (var i = 0; i < 1000; i++)
        {
            seen.Add(strategy.Next(data));
        }

        foreach (var member in Enum.GetValues<DayOfWeek>())
        {
            Assert.Contains(member, seen);
        }
    }

    [Fact]
    public void Enums_WorksWithCustomEnum_ReturnsOnlyValidMembers()
    {
        var strategy = Gen.Enums<Colour>();
        var data = MakeData();
        var valid = Enum.GetValues<Colour>();

        for (var i = 0; i < 100; i++)
        {
            Assert.Contains(strategy.Next(data), valid);
        }
    }

    [Fact]
    public void Enums_CustomEnum_CoversAllMembersOverManyDraws()
    {
        var strategy = Gen.Enums<Colour>();
        var data = MakeData();
        var seen = new HashSet<Colour>();

        for (var i = 0; i < 500; i++)
        {
            seen.Add(strategy.Next(data));
        }

        foreach (var member in Enum.GetValues<Colour>())
        {
            Assert.Contains(member, seen);
        }
    }

    [Fact]
    public void Enums_DeterministicWithSeed()
    {
        var strategy = Gen.Enums<DayOfWeek>();

        var results1 = Enumerable.Range(0, 20)
            .Select(_ => strategy.Next(MakeData(99UL)))
            .ToList();
        var results2 = Enumerable.Range(0, 20)
            .Select(_ => strategy.Next(MakeData(99UL)))
            .ToList();

        Assert.Equal(results1, results2);
    }
}
