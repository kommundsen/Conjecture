using Microsoft.Data.Sqlite;

namespace Conjecture.Core.Internal;

internal sealed class ExampleDatabase : IDisposable
{
    private readonly SqliteConnection connection;

    internal ExampleDatabase(string dbPath)
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

    private void InitializeSchema()
    {
        Execute("PRAGMA journal_mode = WAL");

        using SqliteTransaction tx = connection.BeginTransaction();
        Execute("""
            CREATE TABLE IF NOT EXISTS schema_version (
                version INTEGER NOT NULL
            )
            """, tx);
        Execute("INSERT INTO schema_version (version) SELECT 1 WHERE NOT EXISTS (SELECT 1 FROM schema_version)", tx);
        Execute("""
            CREATE TABLE IF NOT EXISTS examples (
                test_id_hash TEXT NOT NULL,
                buffer BLOB NOT NULL,
                created_at TEXT NOT NULL,
                UNIQUE (test_id_hash, buffer)
            )
            """, tx);
        Execute("CREATE INDEX IF NOT EXISTS idx_examples_hash ON examples (test_id_hash)", tx);
        tx.Commit();
    }

    internal void Save(string testIdHash, byte[] buffer)
    {
        using SqliteCommand cmd = connection.CreateCommand();
        cmd.CommandText = "INSERT OR IGNORE INTO examples (test_id_hash, buffer, created_at) VALUES (@hash, @buffer, @created_at)";
        cmd.Parameters.AddWithValue("@hash", testIdHash);
        cmd.Parameters.AddWithValue("@buffer", buffer);
        cmd.Parameters.AddWithValue("@created_at", DateTime.UtcNow.ToString("O"));
        cmd.ExecuteNonQuery();
    }

    internal void Delete(string testIdHash)
    {
        using SqliteCommand cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM examples WHERE test_id_hash = @hash";
        cmd.Parameters.AddWithValue("@hash", testIdHash);
        cmd.ExecuteNonQuery();
    }

    internal IReadOnlyList<byte[]> Load(string testIdHash)
    {
        using SqliteCommand cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT buffer FROM examples WHERE test_id_hash = @hash";
        cmd.Parameters.AddWithValue("@hash", testIdHash);
        using SqliteDataReader reader = cmd.ExecuteReader();
        List<byte[]> results = [];
        while (reader.Read())
        {
            results.Add(reader.GetFieldValue<byte[]>(0));
        }
        return results;
    }

    private void Execute(string sql, SqliteTransaction? tx = null)
    {
        using SqliteCommand cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Transaction = tx;
        cmd.ExecuteNonQuery();
    }

    public void Dispose()
    {
        connection.Dispose();
    }
}
