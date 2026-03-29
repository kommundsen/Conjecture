using Microsoft.Data.Sqlite;

namespace Conjecture.Core.Internal.Database;

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
                created_at TEXT NOT NULL
            )
            """, tx);
        tx.Commit();
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
