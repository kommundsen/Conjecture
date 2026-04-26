// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Text.RegularExpressions;

using Conjecture.Core.Internal;

namespace Conjecture.Core.Tests.Strategies;

public class VersionStringStrategyTests
{
    private static ConjectureData MakeData(ulong seed = 42UL) =>
        ConjectureData.ForGeneration(new SplittableRandom(seed));

    // ── Pattern ──────────────────────────────────────────────────────────────

    [Fact]
    public void VersionStrings_MatchesDotSeparatedThreeComponentPattern()
    {
        Strategy<string> strategy = Generate.VersionStrings();
        _ = MakeData();

        for (int i = 0; i < 50; i++)
        {
            string value = strategy.Generate(MakeData((ulong)i));
            Assert.Matches(@"^\d+\.\d+\.\d+$", value);
        }
    }

    // ── Bounds ───────────────────────────────────────────────────────────────

    [Fact]
    public void VersionStrings_MajorComponentDoesNotExceedMaxMajor()
    {
        Strategy<string> strategy = Generate.VersionStrings(maxMajor: 3);

        for (int i = 0; i < 100; i++)
        {
            string value = strategy.Generate(MakeData((ulong)i));
            int major = int.Parse(value.Split('.')[0]);
            Assert.InRange(major, 0, 3);
        }
    }

    [Fact]
    public void VersionStrings_MinorComponentDoesNotExceedMaxMinor()
    {
        Strategy<string> strategy = Generate.VersionStrings(maxMinor: 5);

        for (int i = 0; i < 100; i++)
        {
            string value = strategy.Generate(MakeData((ulong)i));
            int minor = int.Parse(value.Split('.')[1]);
            Assert.InRange(minor, 0, 5);
        }
    }

    [Fact]
    public void VersionStrings_PatchComponentDoesNotExceedMaxPatch()
    {
        Strategy<string> strategy = Generate.VersionStrings(maxPatch: 2);

        for (int i = 0; i < 100; i++)
        {
            string value = strategy.Generate(MakeData((ulong)i));
            int patch = int.Parse(value.Split('.')[2]);
            Assert.InRange(patch, 0, 2);
        }
    }

    // ── Minimum value via replay ──────────────────────────────────────────────

    [Fact]
    public void VersionStrings_AllZeroDrawsProducesZeroZeroZero()
    {
        // Build a replay node array that returns 0 for every draw.
        // VersionStringStrategy draws: for each of major/minor/patch:
        //   NextStringLength(0, maxX)  → 1 node
        //   NextStringChar('0','9') × len  → len nodes
        // With length=1 and char='0', each component produces "0".
        // Nodes: [Len(1,0,max), Char('0',ord('0'),ord('9'))] × 3

        List<IRNode> nodes = [];
        for (int i = 0; i < 3; i++)
        {
            nodes.Add(IRNode.ForStringLength(1UL, 0UL, 9UL));
            nodes.Add(IRNode.ForStringChar((ulong)'0', (ulong)'0', (ulong)'9'));
        }

        Strategy<string> strategy = Generate.VersionStrings();
        ConjectureData data = ConjectureData.ForRecord(nodes);
        string result = strategy.Generate(data);

        Assert.Equal("0.0.0", result);
    }
}