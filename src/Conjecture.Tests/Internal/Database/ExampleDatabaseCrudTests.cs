using Conjecture.Core.Internal.Database;

namespace Conjecture.Tests.Internal.Database;

public sealed class ExampleDatabaseCrudTests : IDisposable
{
    private readonly string tempDir;
    private readonly string dbPath;
    private readonly ExampleDatabase db;

    public ExampleDatabaseCrudTests()
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
    public void Load_UnknownKey_ReturnsEmptyList()
    {
        IReadOnlyList<byte[]> result = db.Load("nonexistent-hash");

        Assert.Empty(result);
    }

    [Fact]
    public void Save_ThenLoad_ReturnsSavedBuffer()
    {
        byte[] buffer = [1, 2, 3, 4];

        db.Save("hash-abc", buffer);
        IReadOnlyList<byte[]> result = db.Load("hash-abc");

        Assert.Single(result);
        Assert.Equal(buffer, result[0]);
    }

    [Fact]
    public void Save_MultipleBuffers_LoadReturnsAll()
    {
        byte[] buffer1 = [1, 2, 3];
        byte[] buffer2 = [4, 5, 6];

        db.Save("hash-multi", buffer1);
        db.Save("hash-multi", buffer2);
        IReadOnlyList<byte[]> result = db.Load("hash-multi");

        Assert.Equal(2, result.Count);
        Assert.Contains(result, r => r.SequenceEqual(buffer1));
        Assert.Contains(result, r => r.SequenceEqual(buffer2));
    }

    [Fact]
    public void Load_ReturnsOnlyBuffersForRequestedHash()
    {
        byte[] bufferA = [10, 20];
        byte[] bufferB = [30, 40];

        db.Save("hash-a", bufferA);
        db.Save("hash-b", bufferB);
        IReadOnlyList<byte[]> result = db.Load("hash-a");

        Assert.Single(result);
        Assert.Equal(bufferA, result[0]);
    }

    [Fact]
    public void Save_DuplicateBuffer_DoesNotCreateDuplicate()
    {
        byte[] buffer = [7, 8, 9];

        db.Save("hash-dup", buffer);
        db.Save("hash-dup", buffer);
        IReadOnlyList<byte[]> result = db.Load("hash-dup");

        Assert.Single(result);
    }
}
