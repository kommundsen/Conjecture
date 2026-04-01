using Conjecture.Core.Internal;

namespace Conjecture.Tests.Internal.Database;

public sealed class ExampleDatabaseDeleteTests : IDisposable
{
    private readonly string tempDir;
    private readonly string dbPath;
    private readonly ExampleDatabase db;

    public ExampleDatabaseDeleteTests()
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
    public void Delete_ExistingKey_RemovesAllBuffers()
    {
        db.Save("hash-to-delete", [1, 2, 3]);
        db.Save("hash-to-delete", [4, 5, 6]);

        db.Delete("hash-to-delete");

        Assert.Empty(db.Load("hash-to-delete"));
    }

    [Fact]
    public void Delete_NonexistentKey_DoesNotThrow()
    {
        db.Delete("hash-does-not-exist");
    }

    [Fact]
    public void Delete_ThenLoad_ReturnsEmptyList()
    {
        db.Save("hash-del-load", [10, 20, 30]);

        db.Delete("hash-del-load");
        IReadOnlyList<byte[]> result = db.Load("hash-del-load");

        Assert.Empty(result);
    }

    [Fact]
    public void Delete_OnlyRemovesBuffersForTargetHash()
    {
        db.Save("hash-keep", [1, 2]);
        db.Save("hash-remove", [3, 4]);

        db.Delete("hash-remove");

        Assert.Single(db.Load("hash-keep"));
        Assert.Empty(db.Load("hash-remove"));
    }
}
