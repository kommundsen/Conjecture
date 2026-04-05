// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core;
using Conjecture.Core.Internal;

namespace Conjecture.Core.Tests.Strategies;

public class EnumStrategyTests
{
    private static ConjectureData MakeData(ulong seed = 42UL) =>
        ConjectureData.ForGeneration(new SplittableRandom(seed));

    private enum Colour { Red, Green, Blue }

    [Fact]
    public void Enums_ReturnsOnlyValidDayOfWeekValues()
    {
        var strategy = Generate.Enums<DayOfWeek>();
        var data = MakeData();
        var valid = Enum.GetValues<DayOfWeek>();

        for (var i = 0; i < 100; i++)
        {
            Assert.Contains(strategy.Generate(data), valid);
        }
    }

    [Fact]
    public void Enums_CoversAllDayOfWeekMembersOverManyDraws()
    {
        var strategy = Generate.Enums<DayOfWeek>();
        var data = MakeData();
        var seen = new HashSet<DayOfWeek>();

        for (var i = 0; i < 1000; i++)
        {
            seen.Add(strategy.Generate(data));
        }

        foreach (var member in Enum.GetValues<DayOfWeek>())
        {
            Assert.Contains(member, seen);
        }
    }

    [Fact]
    public void Enums_WorksWithCustomEnum_ReturnsOnlyValidMembers()
    {
        var strategy = Generate.Enums<Colour>();
        var data = MakeData();
        var valid = Enum.GetValues<Colour>();

        for (var i = 0; i < 100; i++)
        {
            Assert.Contains(strategy.Generate(data), valid);
        }
    }

    [Fact]
    public void Enums_CustomEnum_CoversAllMembersOverManyDraws()
    {
        var strategy = Generate.Enums<Colour>();
        var data = MakeData();
        var seen = new HashSet<Colour>();

        for (var i = 0; i < 500; i++)
        {
            seen.Add(strategy.Generate(data));
        }

        foreach (var member in Enum.GetValues<Colour>())
        {
            Assert.Contains(member, seen);
        }
    }

    [Fact]
    public void Enums_DeterministicWithSeed()
    {
        var strategy = Generate.Enums<DayOfWeek>();

        var results1 = Enumerable.Range(0, 20)
            .Select(_ => strategy.Generate(MakeData(99UL)))
            .ToList();
        var results2 = Enumerable.Range(0, 20)
            .Select(_ => strategy.Generate(MakeData(99UL)))
            .ToList();

        Assert.Equal(results1, results2);
    }
}