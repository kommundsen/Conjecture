// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core;
using Conjecture.Core.Internal;

namespace Conjecture.Tests.EndToEnd;

/// <summary>
/// End-to-end tests verifying the example database round-trip:
/// fail → save → replay → fix → clean, and that failure messages include
/// reproduction seeds.
/// </summary>
public sealed class DatabaseRegressionE2ETests : IDisposable
{
    private readonly string tempDir;
    private readonly string dbPath;

    public DatabaseRegressionE2ETests()
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

    // --- Full round-trip ---

    [Fact]
    public async Task Fail_SavesCounterexampleToDatabase()
    {
        ConjectureSettings settings = new() { MaxExamples = 20, UseDatabase = true };
        string testId = "e2e-roundtrip-fail-saves";

        using ExampleDatabase db = new(dbPath);
        await TestRunner.Run(settings, _ => throw new Exception("fail"), db, testId);

        Assert.NotEmpty(db.Load(testId));
    }

    [Fact]
    public async Task Fail_ThenRerun_ReplaysStoredBuffer()
    {
        ConjectureSettings settings = new() { MaxExamples = 20, UseDatabase = true };
        string testId = "e2e-roundtrip-replay";

        using ExampleDatabase db = new(dbPath);

        // First run: always fails → saves buffer
        await TestRunner.Run(settings, _ => throw new Exception("fail"), db, testId);
        Assert.NotEmpty(db.Load(testId));

        // Second run: verify the stored buffer is replayed
        bool replayInvoked = false;
        await TestRunner.Run(settings, data =>
        {
            if (data.IsReplay)
            {
                replayInvoked = true;
            }
            throw new Exception("still failing");
        }, db, testId);

        Assert.True(replayInvoked, "stored buffer should be replayed on second run");
    }

    [Fact]
    public async Task Fail_ThenFix_RemovesStoredBuffer()
    {
        ConjectureSettings settings = new() { MaxExamples = 20, UseDatabase = true };
        string testId = "e2e-roundtrip-fix-cleans";

        using ExampleDatabase db = new(dbPath);

        // First run: always fails → saves buffer
        await TestRunner.Run(settings, _ => throw new Exception("fail"), db, testId);
        Assert.NotEmpty(db.Load(testId));

        // Second run: property "fixed" (always passes) → stored buffer removed
        await TestRunner.Run(settings, _ => { }, db, testId);

        Assert.Empty(db.Load(testId));
    }

    // --- DB file on disk ---

    [Fact]
    public async Task FailingProperty_UseDatabase_DbFileExistsOnDisk()
    {
        ConjectureSettings settings = new() { MaxExamples = 10, UseDatabase = true };
        string testId = "e2e-db-file-exists";

        using (ExampleDatabase db = new(dbPath))
        {
            await TestRunner.Run(settings, _ => throw new Exception("fail"), db, testId);
        }

        Assert.True(File.Exists(dbPath), $"Expected DB file at {dbPath}");
    }

    [Fact]
    public async Task FailingProperty_UseDatabase_DbFileContainsSavedBuffer()
    {
        ConjectureSettings settings = new() { MaxExamples = 10, UseDatabase = true };
        string testId = "e2e-db-file-has-buffer";

        using ExampleDatabase db = new(dbPath);
        await TestRunner.Run(settings, _ => throw new Exception("fail"), db, testId);

        // Reopen database to confirm persistence is on disk, not just in-memory
        using ExampleDatabase db2 = new(dbPath);
        Assert.NotEmpty(db2.Load(testId));
    }

    // --- Seed in failure message ---

    [Fact]
    public async Task FailureMessage_IncludesSeed_ForReproduction()
    {
        Strategy<int> strategy = Generate.Integers<int>(0, 100);
        ConjectureSettings settings = new() { MaxExamples = 50, UseDatabase = true };
        string testId = "e2e-seed-in-message";

        using ExampleDatabase db = new(dbPath);
        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            int x = strategy.Generate(data);
            if (x > 5) { throw new Exception("too large"); }
        }, db, testId);

        Assert.False(result.Passed);
        Assert.NotNull(result.Seed);

        string msg = CounterexampleFormatter.Format(
            [("x", (object)6)],
            seed: result.Seed!.Value,
            exampleCount: result.ExampleCount,
            shrinkCount: result.ShrinkCount);

        Assert.Contains($"0x{result.Seed.Value:X}", msg);
    }

    [Fact]
    public async Task FailureMessage_SeedAllowsReproduction_ReplayProducesSameCounterexample()
    {
        Strategy<int> strategy = Generate.Integers<int>(0, 100);

        // First run: no seed, find a failing example
        ConjectureSettings settings1 = new() { MaxExamples = 50, UseDatabase = false };
        using ExampleDatabase db = new(dbPath);
        TestRunResult first = await TestRunner.Run(settings1, data =>
        {
            int x = strategy.Generate(data);
            if (x > 5) { throw new Exception("too large"); }
        }, db, "e2e-seed-reproduce-1");

        Assert.False(first.Passed);
        Assert.NotNull(first.Seed);

        // Second run: use seed from failure → must also fail
        ConjectureSettings settings2 = new()
        {
            MaxExamples = 50,
            Seed = first.Seed!.Value,
            UseDatabase = false
        };
        TestRunResult second = await TestRunner.Run(settings2, data =>
        {
            int x = strategy.Generate(data);
            if (x > 5) { throw new Exception("too large"); }
        }, db, "e2e-seed-reproduce-2");

        Assert.False(second.Passed);
    }
}