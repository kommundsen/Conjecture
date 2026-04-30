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
    public void Schema_HasExamplesTableWithIrColumn()
    {
        using ExampleDatabase db = new(dbPath);

        string columnType = GetColumnType("ir");

        Assert.Equal("BLOB", columnType);
    }

    [Fact]
    public void Migration_RenamesLegacyBufferColumnToIr()
    {
        // Pre-create a database with the legacy 'buffer' schema.
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        using (SqliteConnection seed = new($"Data Source={dbPath}"))
        {
            seed.Open();
            using SqliteCommand cmd = seed.CreateCommand();
            cmd.CommandText = """
                CREATE TABLE examples (
                    test_id_hash TEXT NOT NULL,
                    buffer BLOB NOT NULL,
                    created_at TEXT NOT NULL,
                    UNIQUE (test_id_hash, buffer)
                );
                INSERT INTO examples (test_id_hash, buffer, created_at) VALUES ('legacy', x'01', '2026-01-01T00:00:00Z');
                """;
            cmd.ExecuteNonQuery();
        }

        using ExampleDatabase db = new(dbPath);

        Assert.Equal("BLOB", GetColumnType("ir"));
        Assert.Throws<InvalidOperationException>(() => GetColumnType("buffer"));

        IReadOnlyList<byte[]> loaded = db.Load("legacy");
        Assert.Single(loaded);
        Assert.Equal(new byte[] { 0x01 }, loaded[0]);
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