// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Generic;

using Conjecture.Core;
using Conjecture.Core.Internal;

namespace Conjecture.Core.Tests.Strategies;

public class VersionStrategyTests
{
    private static ConjectureData MakeData(ulong seed = 42UL) =>
        ConjectureData.ForGeneration(new SplittableRandom(seed));

    // ── Bounds ───────────────────────────────────────────────────────────────

    [Fact]
    public void Versions_Generates_Components_Within_Bounds()
    {
        Strategy<Version> strategy = Strategy.Versions(maxMajor: 5, maxMinor: 3, maxBuild: 7, maxRevision: 2);

        for (int i = 0; i < 200; i++)
        {
            Version v = strategy.Generate(MakeData((ulong)i));
            Assert.InRange(v.Major, 0, 5);
            Assert.InRange(v.Minor, 0, 3);
            Assert.InRange(v.Build, 0, 7);
            Assert.InRange(v.Revision, 0, 2);
        }
    }

    // ── Shrink ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Versions_Shrinks_Toward_Zero_Components()
    {
        Strategy<Version> strategy = Strategy.Versions();
        ConjectureSettings settings = new() { MaxExamples = 200, Seed = 1UL, Database = false };
        Version zero = new(0, 0, 0, 0);

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            _ = strategy.Generate(data);
            throw new Exception("always fails");
        });

        Assert.False(result.Passed);
        ConjectureData replay = ConjectureData.ForRecord(result.Counterexample!);
        Version shrunk = strategy.Generate(replay);
        Assert.Equal(zero, shrunk);
    }

    // ── Validation ────────────────────────────────────────────────────────────

    [Fact]
    public void Versions_NegativeMaxMajor_ThrowsWithCorrectParamName()
    {
        ArgumentOutOfRangeException ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => Strategy.Versions(maxMajor: -1));
        Assert.Equal("maxMajor", ex.ParamName);
    }

    [Fact]
    public void Versions_NegativeMaxMinor_ThrowsWithCorrectParamName()
    {
        ArgumentOutOfRangeException ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => Strategy.Versions(maxMinor: -1));
        Assert.Equal("maxMinor", ex.ParamName);
    }

    [Fact]
    public void Versions_NegativeMaxBuild_ThrowsWithCorrectParamName()
    {
        ArgumentOutOfRangeException ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => Strategy.Versions(maxBuild: -1));
        Assert.Equal("maxBuild", ex.ParamName);
    }

    [Fact]
    public void Versions_NegativeMaxRevision_ThrowsWithCorrectParamName()
    {
        ArgumentOutOfRangeException ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => Strategy.Versions(maxRevision: -1));
        Assert.Equal("maxRevision", ex.ParamName);
    }

    // ── Strategy.For<Version> ────────────────────────────────────────────────

    [Fact]
    public void Versions_Default_Resolves_Via_For_Version()
    {
        Strategy<Version> strategy = Strategy.For<Version>();

        for (int i = 0; i < 50; i++)
        {
            Version v = strategy.Generate(MakeData((ulong)i));
            Assert.NotNull(v);
            Assert.True(v.Major >= 0);
            Assert.True(v.Minor >= 0);
            Assert.True(v.Build >= 0);
            Assert.True(v.Revision >= 0);
        }
    }
}
