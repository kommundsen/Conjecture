// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core.Internal;
using Microsoft.Extensions.Logging;

namespace Conjecture.Tests.Internal;

public sealed class ExampleDatabaseLoggingTests : IDisposable
{
    private sealed class CollectingLogger : ILogger
    {
        public List<(LogLevel Level, EventId EventId)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Entries.Add((logLevel, eventId));
        }
    }

    private readonly string tempDir;

    public ExampleDatabaseLoggingTests()
    {
        tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
    }

    private string DbPath() => Path.Combine(tempDir, "test.db");

    [Fact]
    public void Load_WithStoredExamples_LogsDatabaseReplaying()
    {
        CollectingLogger logger = new();
        using ExampleDatabase db = new(DbPath(), logger);
        db.Save("hash1", [1, 2, 3]);

        db.Load("hash1");

        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Information && e.EventId.Id == 6);
    }

    [Fact]
    public void Save_WithCollectingLogger_LogsDatabaseSaved()
    {
        CollectingLogger logger = new();
        using ExampleDatabase db = new(DbPath(), logger);

        db.Save("hash1", [1, 2, 3]);

        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Debug && e.EventId.Id == 11);
    }

    [Fact]
    public void Constructor_WithCorruptDatabase_LogsDatabaseError()
    {
        string dbPath = DbPath();
        File.WriteAllText(dbPath, "not a sqlite database");
        CollectingLogger logger = new();

        using ExampleDatabase db = new(dbPath, logger);

        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning && e.EventId.Id == 8);
    }

    [Fact]
    public void Load_EmptyDatabase_DoesNotLogDatabaseReplaying()
    {
        CollectingLogger logger = new();
        using ExampleDatabase db = new(DbPath(), logger);

        db.Load("hash1");

        Assert.DoesNotContain(logger.Entries, e => e.EventId.Id == 6);
    }

    public void Dispose()
    {
        try { Directory.Delete(tempDir, recursive: true); } catch { }
    }
}
