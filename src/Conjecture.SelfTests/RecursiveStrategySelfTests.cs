// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core;
using Conjecture.Core.Internal;
using Conjecture.TestingPlatform;

using Xunit;

namespace Conjecture.SelfTests;

public class RecursiveStrategySelfTests
{
    // Value equals depth of recursion: 0 = base case, n = n levels deep.
    // Strategy.Just(0) as base case ensures the only way to get a value > 0 is
    // through actual recursive expansion, so the generated int directly tracks depth.
    private static readonly Strategy<int> DepthCountingStrategy = Strategy.Recursive<int>(
        Strategy.Just(0),
        self => Strategy.OneOf(
            Strategy.Just(0),
            self.Select(n => n + 1)),
        maxDepth: 5);

    private static int ReplayDepth(IReadOnlyList<IRNode> nodes)
    {
        int depth = 0;
        SelfTestHelpers.Replay(nodes, data => depth = DepthCountingStrategy.Generate(data));
        return depth;
    }

    [Property]
    public async Task RecursiveStrategy_SameIRNodes_ProduceSameValue()
    {
        ConjectureSettings settings = new() { Seed = 7UL, MaxExamples = 50, UseDatabase = false };

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            _ = DepthCountingStrategy.Generate(data);
            throw new InvalidOperationException("capture");
        });

        Assert.False(result.Passed);
        IReadOnlyList<IRNode> nodes = result.Counterexample!;

        int first = ReplayDepth(nodes);
        int second = ReplayDepth(nodes);
        Assert.Equal(first, second);
    }

    [Property]
    public async Task RecursiveStrategy_ShrunkValue_HasDepthAtMostOriginal()
    {
        ConjectureSettings settings = new() { Seed = 42UL, MaxExamples = 50, UseDatabase = false };

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            int depth = DepthCountingStrategy.Generate(data);
            if (depth > 0)
            {
                throw new InvalidOperationException($"depth {depth} > 0");
            }
        });

        Assert.False(result.Passed);
        int originalDepth = ReplayDepth(result.OriginalCounterexample!);
        int shrunkDepth = ReplayDepth(result.Counterexample!);
        Assert.True(shrunkDepth <= originalDepth,
            $"Shrunk depth {shrunkDepth} exceeded original depth {originalDepth}");
    }
}