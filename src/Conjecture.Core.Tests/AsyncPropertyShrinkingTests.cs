// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Diagnostics;

using Conjecture.Core;
using Conjecture.Core.Internal;

namespace Conjecture.Core.Tests;

public sealed class AsyncPropertyShrinkingTests : IDisposable
{
    private readonly string tempDir;
    private readonly ExampleDatabase db;

    public AsyncPropertyShrinkingTests()
    {
        tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        db = new(Path.Combine(tempDir, "conjecture.db"));
    }

    public void Dispose()
    {
        db.Dispose();
        try { Directory.Delete(tempDir, recursive: true); } catch (IOException) { }
    }

    [Fact]
    public async Task AsyncFailingTest_ShrinksToMinimalCounterexample()
    {
        TestRunResult result = await TestRunner.RunAsync(
            new ConjectureSettings { MaxExamples = 200, Seed = 1UL },
            async data =>
            {
                int x = Strategy.Integers<int>(0, 100).Generate(data);
                await Task.Yield();
                if (x > 5)
                {
                    throw new InvalidOperationException("too large");
                }
            });

        Assert.False(result.Passed);
        Assert.True(result.ShrinkCount > 0);
        ConjectureData replay = ConjectureData.ForRecord(result.Counterexample!);
        int shrunkValue = Strategy.Integers<int>(0, 100).Generate(replay);
        Assert.Equal(6, shrunkValue);
    }

    [Fact]
    public async Task AsyncProperty_Deadline_TerminatesLongRunningExample()
    {
        ConjectureSettings settings = new()
        {
            MaxExamples = 100,
            Deadline = TimeSpan.FromMilliseconds(200),
        };

        Stopwatch sw = Stopwatch.StartNew();
        await Assert.ThrowsAsync<ConjectureException>(async () =>
            await TestRunner.RunAsync(settings, async _ => await Task.Delay(10_000)));
        sw.Stop();

        Assert.True(sw.ElapsedMilliseconds < 5_000,
            $"Per-example deadline should terminate within 5s, took {sw.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task ShrinkingReplays_AreAsyncAware()
    {
        // Real async continuations within shrinking replays must complete correctly
        TestRunResult result = await TestRunner.RunAsync(
            new ConjectureSettings { MaxExamples = 200, Seed = 7UL },
            async data =>
            {
                int x = Strategy.Integers<int>(0, 500).Generate(data);
                await Task.Delay(0); // forces a real thread-pool continuation
                if (x > 10)
                {
                    throw new InvalidOperationException("too large");
                }
            });

        Assert.False(result.Passed);
        Assert.True(result.ShrinkCount > 0);
        ConjectureData replay = ConjectureData.ForRecord(result.Counterexample!);
        int shrunkValue = Strategy.Integers<int>(0, 500).Generate(replay);
        Assert.Equal(11, shrunkValue);
    }

    [Fact]
    public async Task RunAsync_WithDatabase_FailingTest_SavesBufferToDatabase()
    {
        var settings = new ConjectureSettings { MaxExamples = 10, UseDatabase = true };
        string testId = "async-fail-save";

        await TestRunner.RunAsync(settings, _ => throw new InvalidOperationException("fail"), db, testId);

        Assert.NotEmpty(db.Load(testId));
    }

    [Fact]
    public async Task RunAsync_WithDatabase_ReplaysStoredBufferOnSecondRun()
    {
        var settings = new ConjectureSettings { MaxExamples = 10, UseDatabase = true };
        string testId = "async-replay";
        bool replayInvoked = false;

        // First run: fails and saves
        await TestRunner.RunAsync(settings, _ => throw new InvalidOperationException("fail"), db, testId);
        Assert.NotEmpty(db.Load(testId));

        // Second run: replays stored buffer first
        await TestRunner.RunAsync(settings, async data =>
        {
            if (data.IsReplay)
            {
                replayInvoked = true;
            }

            await Task.Yield();
            throw new InvalidOperationException("fail");
        }, db, testId);

        Assert.True(replayInvoked, "stored buffer should be replayed on second run");
    }

    [Fact]
    public async Task RunAsync_WithDatabase_PassingReplay_ClearsDatabase()
    {
        var settings = new ConjectureSettings { MaxExamples = 10, UseDatabase = true };
        string testId = "async-passing-clears";

        db.Save(testId, [0x00, 0x00, 0x00, 0x00]);

        await TestRunner.RunAsync(settings, async _ => await Task.Yield(), db, testId);

        Assert.Empty(db.Load(testId));
    }
}