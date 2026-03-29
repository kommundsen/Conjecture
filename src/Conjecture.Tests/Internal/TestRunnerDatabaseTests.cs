using Conjecture.Core;
using Conjecture.Core.Internal;
using Conjecture.Core.Internal.Database;

namespace Conjecture.Tests.Internal;

public sealed class TestRunnerDatabaseTests : IDisposable
{
    private readonly string tempDir;
    private readonly string dbPath;
    private readonly ExampleDatabase db;

    public TestRunnerDatabaseTests()
    {
        tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        dbPath = Path.Combine(tempDir, ".conjecture", "examples", "conjecture.db");
        db = new(dbPath);
    }

    public void Dispose()
    {
        db.Dispose();
        try
        {
            Directory.Delete(tempDir, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public void Run_FailingTest_UseDatabase_SavesBufferToDatabase()
    {
        var settings = new ConjectureSettings { MaxExamples = 10, UseDatabase = true };
        string testId = "test-failing-saves";

        TestRunner.Run(settings, _ => throw new InvalidOperationException("fail"), db, testId);

        IReadOnlyList<byte[]> saved = db.Load(testId);
        Assert.NotEmpty(saved);
    }

    [Fact]
    public void Run_FailingThenSameId_ReplaysStoredBufferFirst()
    {
        var settings = new ConjectureSettings { MaxExamples = 10, UseDatabase = true };
        string testId = "test-replay-first";
        var replayInvoked = false;

        // First run: fails and saves
        TestRunner.Run(settings, _ => throw new InvalidOperationException("fail"), db, testId);
        Assert.NotEmpty(db.Load(testId));

        // Second run: replay buffer is attempted first
        TestRunner.Run(settings, data =>
        {
            if (data.IsReplay)
            {
                replayInvoked = true;
            }
            throw new InvalidOperationException("fail");
        }, db, testId);

        Assert.True(replayInvoked, "stored buffer should have been replayed on second run");
    }

    [Fact]
    public void Run_PassingReplay_RemovesBufferFromDatabase()
    {
        var settings = new ConjectureSettings { MaxExamples = 10, UseDatabase = true };
        string testId = "test-passing-removes";

        // Save a buffer that would be replayed
        db.Save(testId, [0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00]);

        // Run that passes — stored buffer should be removed
        TestRunner.Run(settings, _ => { }, db, testId);

        Assert.Empty(db.Load(testId));
    }

    [Fact]
    public void Run_UseDatabaseFalse_DoesNotSaveOnFailure()
    {
        var settings = new ConjectureSettings { MaxExamples = 10, UseDatabase = false };
        string testId = "test-no-db-save";

        TestRunner.Run(settings, _ => throw new InvalidOperationException("fail"), db, testId);

        Assert.Empty(db.Load(testId));
    }

    [Fact]
    public void Run_UseDatabaseFalse_DoesNotReplayStoredBuffer()
    {
        string testId = "test-no-db-replay";

        // Save a buffer manually
        db.Save(testId, [0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00]);

        var replayInvoked = false;
        var noDbSettings = new ConjectureSettings { MaxExamples = 10, UseDatabase = false };
        TestRunner.Run(noDbSettings, data =>
        {
            if (data.IsReplay)
            {
                replayInvoked = true;
            }
        }, db, testId);

        Assert.False(replayInvoked, "UseDatabase=false should skip DB load");
    }

    [Fact]
    public void Run_ExplicitSeed_DoesNotSaveOnFailure()
    {
        var settings = new ConjectureSettings { MaxExamples = 10, Seed = 42UL, UseDatabase = true };
        string testId = "test-seed-no-save";

        TestRunner.Run(settings, _ => throw new InvalidOperationException("fail"), db, testId);

        Assert.Empty(db.Load(testId));
    }

    [Fact]
    public void Run_ExplicitSeed_DoesNotReplayStoredBuffer()
    {
        string testId = "test-seed-no-replay";

        // Save a buffer manually
        db.Save(testId, [0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00]);

        var replayInvoked = false;
        var settings = new ConjectureSettings { MaxExamples = 10, Seed = 99UL, UseDatabase = true };
        TestRunner.Run(settings, data =>
        {
            if (data.IsReplay)
            {
                replayInvoked = true;
            }
        }, db, testId);

        Assert.False(replayInvoked, "explicit Seed should skip DB replay");
    }
}
