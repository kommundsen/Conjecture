// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

// Derived from the Python Hypothesis library.
// Original copyright: Copyright (c) 2013-present, David R. MacIver and contributors.

using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Conjecture.Core.Internal;

internal sealed class ExampleDatabase : IDisposable
{
    private readonly SqliteConnection? connection;
    private readonly ILogger logger;

    internal ExampleDatabase(string dbPath, ILogger? logger = null)
    {
        this.logger = logger ?? NullLogger.Instance;
        try
        {
            string? dir = Path.GetDirectoryName(dbPath);
            if (dir is not null)
            {
                Directory.CreateDirectory(dir);
            }

            connection = new($"Data Source={dbPath}");
            connection.Open();
            InitializeSchema();
        }
        catch (Exception ex)
        {
            Log.DatabaseError(this.logger, ex.Message, ex);
            connection?.Dispose();
            connection = null;
        }
    }

    private void InitializeSchema()
    {
        Execute("PRAGMA journal_mode = WAL");

        using SqliteTransaction tx = connection!.BeginTransaction();
        Execute("""
            CREATE TABLE IF NOT EXISTS schema_version (
                version INTEGER NOT NULL
            )
            """, tx);
        Execute("INSERT INTO schema_version (version) SELECT 1 WHERE NOT EXISTS (SELECT 1 FROM schema_version)", tx);
        Execute("""
            CREATE TABLE IF NOT EXISTS examples (
                test_id_hash TEXT NOT NULL,
                ir BLOB NOT NULL,
                created_at TEXT NOT NULL,
                UNIQUE (test_id_hash, ir)
            )
            """, tx);
        MigrateBufferToIr(tx);
        Execute("CREATE INDEX IF NOT EXISTS idx_examples_hash ON examples (test_id_hash)", tx);
        tx.Commit();
    }

    // Renames the legacy 'buffer' column to 'ir' on databases created before the rename.
    // Idempotent: no-op when the column has already been renamed or the table was just created.
    private void MigrateBufferToIr(SqliteTransaction tx)
    {
        bool hasBuffer = false;
        bool hasIr = false;
        using (SqliteCommand cmd = connection!.CreateCommand())
        {
            cmd.CommandText = "PRAGMA table_info(examples)";
            cmd.Transaction = tx;
            using SqliteDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                string name = reader.GetString(1);
                if (name == "buffer")
                {
                    hasBuffer = true;
                }
                else if (name == "ir")
                {
                    hasIr = true;
                }
            }
        }

        if (hasBuffer && !hasIr)
        {
            Execute("ALTER TABLE examples RENAME COLUMN buffer TO ir", tx);
        }
    }

    internal void Save(string testIdHash, byte[] ir)
    {
        if (connection is null)
        {
            return;
        }

        try
        {
            using SqliteCommand cmd = connection.CreateCommand();
            cmd.CommandText = "INSERT OR IGNORE INTO examples (test_id_hash, ir, created_at) VALUES (@hash, @ir, @created_at)";
            cmd.Parameters.AddWithValue("@hash", testIdHash);
            cmd.Parameters.AddWithValue("@ir", ir);
            cmd.Parameters.AddWithValue("@created_at", DateTime.UtcNow.ToString("O"));
            cmd.ExecuteNonQuery();
            Log.DatabaseSaved(logger, testIdHash);
        }
        catch (Exception ex)
        {
            Log.DatabaseError(logger, ex.Message, ex);
        }
    }

    internal void Delete(string testIdHash)
    {
        if (connection is null)
        {
            return;
        }

        try
        {
            using SqliteCommand cmd = connection.CreateCommand();
            cmd.CommandText = "DELETE FROM examples WHERE test_id_hash = @hash";
            cmd.Parameters.AddWithValue("@hash", testIdHash);
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            Log.DatabaseError(logger, ex.Message, ex);
        }
    }

    internal IReadOnlyList<byte[]> Load(string testIdHash)
    {
        if (connection is null)
        {
            return [];
        }

        try
        {
            using SqliteCommand cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT ir FROM examples WHERE test_id_hash = @hash";
            cmd.Parameters.AddWithValue("@hash", testIdHash);
            using SqliteDataReader reader = cmd.ExecuteReader();
            List<byte[]> results = [];
            while (reader.Read())
            {
                results.Add(reader.GetFieldValue<byte[]>(0));
            }

            if (results.Count > 0)
            {
                Log.DatabaseReplaying(logger, results.Count);
            }

            return results;
        }
        catch (Exception ex)
        {
            Log.DatabaseError(logger, ex.Message, ex);
            return [];
        }
    }

    private void Execute(string sql, SqliteTransaction? tx = null)
    {
        using SqliteCommand cmd = connection!.CreateCommand();
        cmd.CommandText = sql;
        cmd.Transaction = tx;
        cmd.ExecuteNonQuery();
    }

    public void Dispose()
    {
        connection?.Dispose();
    }
}