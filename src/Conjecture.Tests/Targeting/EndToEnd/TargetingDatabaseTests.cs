// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core;
using Conjecture.Core.Internal;

namespace Conjecture.Tests.Targeting.EndToEnd;

public sealed class TargetingDatabaseTests : IDisposable
{
    // Generate.Just(0) draws no IR nodes per element, so HillClimber optimises only the size node.
    private static readonly Strategy<List<int>> ListStrategy =
        Generate.Lists(Generate.Just(0), 0, 100);

    private readonly string tempDir;
    private readonly string dbPath;

    public TargetingDatabaseTests()
    {
        tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        dbPath = Path.Combine(tempDir, "conjecture.db");
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(tempDir, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public async Task TargetingFailure_SavesCounterexampleToDatabase()
    {
        // No explicit Seed so UseDatabase remains active.
        // Gen budget = 9, targeting budget = 81 — HillClimber reliably climbs to >= 99.
        ConjectureSettings settings = new()
        {
            MaxExamples = 90,
            UseDatabase = true,
            Targeting = true,
            TargetingProportion = 0.9,
        };
        string testId = "targeting-db-stores";

        using ExampleDatabase db = new(dbPath);
        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            List<int> xs = ListStrategy.Generate(data);
            Target.Maximize(xs.Count);
            if (xs.Count >= 99)
            {
                throw new InvalidOperationException("xs too large");
            }
        }, db, testId);

        Assert.False(result.Passed);
        Assert.NotEmpty(db.Load(testId));
    }

    [Fact]
    public async Task TargetingFailure_SecondRun_ReplaysStoredBuffer()
    {
        ConjectureSettings settings = new()
        {
            MaxExamples = 90,
            UseDatabase = true,
            Targeting = true,
            TargetingProportion = 0.9,
        };
        string testId = "targeting-db-replay";

        using ExampleDatabase db = new(dbPath);

        TestRunResult first = await TestRunner.Run(settings, data =>
        {
            List<int> xs = ListStrategy.Generate(data);
            Target.Maximize(xs.Count);
            if (xs.Count >= 99)
            {
                throw new InvalidOperationException("xs too large");
            }
        }, db, testId);

        Assert.False(first.Passed);

        bool replayInvoked = false;
        TestRunResult second = await TestRunner.Run(settings, data =>
        {
            if (data.IsReplay)
            {
                replayInvoked = true;
            }

            List<int> xs = ListStrategy.Generate(data);
            Target.Maximize(xs.Count);
            if (xs.Count >= 99)
            {
                throw new InvalidOperationException("xs too large");
            }
        }, db, testId);

        Assert.False(second.Passed);
        Assert.True(replayInvoked, "stored counterexample should be replayed on second run");
    }

    [Fact]
    public async Task TargetingFailure_SeedReproduction_RefindsFailure()
    {
        // UseDatabase=false so the Seed property is not nullified by db logic.
        // Same seed must produce the same targeting path and re-find the failure.
        ConjectureSettings firstSettings = new()
        {
            MaxExamples = 200,
            UseDatabase = false,
            Targeting = true,
            TargetingProportion = 0.5,
        };

        TestRunResult first = await TestRunner.Run(firstSettings, data =>
        {
            List<int> xs = ListStrategy.Generate(data);
            Target.Maximize(xs.Count);
            if (xs.Count > 50)
            {
                throw new InvalidOperationException("xs too large");
            }
        });

        Assert.False(first.Passed);
        Assert.NotNull(first.Seed);

        ConjectureSettings replaySettings = new()
        {
            MaxExamples = firstSettings.MaxExamples,
            Seed = first.Seed,
            UseDatabase = false,
            Targeting = firstSettings.Targeting,
            TargetingProportion = firstSettings.TargetingProportion,
        };

        TestRunResult replay = await TestRunner.Run(replaySettings, data =>
        {
            List<int> xs = ListStrategy.Generate(data);
            Target.Maximize(xs.Count);
            if (xs.Count > 50)
            {
                throw new InvalidOperationException("xs too large");
            }
        });

        Assert.False(replay.Passed);
    }
}
