// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core.Internal;

using Microsoft.Data.Sqlite;

namespace Conjecture.Core.Tests.Internal.Database;

public sealed class ExampleDatabaseSchemaTests : IDisposable
{
    private readonly string tempDir;
    private readonly string dbPath;

    public ExampleDatabaseSchemaTests()
    {
        tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        dbPath = Path.Combine(tempDir, ".conjecture", "examples", "conjecture.db");
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
    public void Constructor_CreatesDbFileAtSpecifiedPath()
    {
        using ExampleDatabase db = new(dbPath);

        Assert.True(File.Exists(dbPath));
    }

    [Fact]
    public void Schema_HasVersionTableWithVersion1()
    {
        using ExampleDatabase db = new(dbPath);

        using SqliteConnection conn = new($"Data Source={dbPath}");
        conn.Open();
        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT version FROM schema_version";
        long version = (long)cmd.ExecuteScalar()!;

        Assert.Equal(1L, version);
    }

    [Fact]
    public void Schema_HasExamplesTableWithTestIdHashColumn()
    {
        using ExampleDatabase db = new(dbPath);

        string columnType = GetColumnType("test_id_hash");

        Assert.Equal("TEXT", columnType);
    }

    [Fact]
    public void Schema_HasExamplesTableWithBufferColumn()
    {
        using ExampleDatabase db = new(dbPath);

        string columnType = GetColumnType("buffer");

        Assert.Equal("BLOB", columnType);
    }

    [Fact]
    public void Schema_HasExamplesTableWithCreatedAtColumn()
    {
        using ExampleDatabase db = new(dbPath);

        string columnType = GetColumnType("created_at");

        Assert.Equal("TEXT", columnType);
    }

    [Fact]
    public void Schema_WalModeEnabled()
    {
        using ExampleDatabase db = new(dbPath);

        using SqliteConnection conn = new($"Data Source={dbPath}");
        conn.Open();
        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA journal_mode";
        string? mode = (string?)cmd.ExecuteScalar();

        Assert.Equal("wal", mode);
    }

    private string GetColumnType(string columnName)
    {
        using SqliteConnection conn = new($"Data Source={dbPath}");
        conn.Open();
        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA table_info(examples)";
        using SqliteDataReader reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            if (reader.GetString(1) == columnName)
            {
                return reader.GetString(2);
            }
        }
        throw new InvalidOperationException($"Column '{columnName}' not found in examples table.");
    }
}