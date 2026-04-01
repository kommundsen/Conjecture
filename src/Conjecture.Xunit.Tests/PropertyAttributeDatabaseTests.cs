using System.Reflection;
using Conjecture.Core;
using Conjecture.Core.Internal;


namespace Conjecture.Xunit.Tests;

/// <summary>
/// Tests that TestCaseHelper wires ExampleDatabase and TestIdHasher into TestRunner.
/// Drives: TestCaseHelper.ComputeTestId(MethodInfo).
/// </summary>
public sealed class PropertyAttributeDatabaseTests : IDisposable
{
    private readonly string tempDir;
    private readonly ExampleDatabase db;

    public PropertyAttributeDatabaseTests()
    {
        tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        db = new(Path.Combine(tempDir, "conjecture.db"));
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
    public void FailingTest_UseDatabase_BufferSavedToDatabase()
    {
        ConjectureSettings settings = new() { MaxExamples = 10, UseDatabase = true };
        string testId = "db-test-failing";

        TestRunner.Run(settings, _ => throw new InvalidOperationException("fail"), db, testId);

        Assert.NotEmpty(db.Load(testId));
    }

    [Fact]
    public void FailingTest_SecondRun_ReplaysStoredBuffer()
    {
        ConjectureSettings settings = new() { MaxExamples = 10, UseDatabase = true };
        string testId = "db-test-replay";
        bool replayInvoked = false;

        // First run: fails and saves buffer
        TestRunner.Run(settings, _ => throw new InvalidOperationException("fail"), db, testId);

        // Second run: stored buffer should be replayed
        TestRunner.Run(settings, data =>
        {
            if (data.IsReplay)
            {
                replayInvoked = true;
            }

            throw new InvalidOperationException("fail");
        }, db, testId);

        Assert.True(replayInvoked, "stored buffer should have been replayed on the second run");
    }

    [Fact]
    public void PassingTest_AfterStoredFailure_ClearsBuffer()
    {
        ConjectureSettings settings = new() { MaxExamples = 10, UseDatabase = true };
        string testId = "db-test-clear";

        // Pre-save a buffer as if a previous failure occurred
        db.Save(testId, [0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00]);

        // Run that now passes — stored buffer should be removed
        TestRunner.Run(settings, _ => { }, db, testId);

        Assert.Empty(db.Load(testId));
    }

    [Fact]
    public void ComputeTestId_ReturnsNonEmptyString()
    {
        MethodInfo method = typeof(PropertyAttributeDatabaseTests)
            .GetMethod(nameof(ComputeTestId_ReturnsNonEmptyString))!;

        string testId = TestCaseHelper.ComputeTestId(method);

        Assert.NotEmpty(testId);
    }

    [Fact]
    public void ComputeTestId_SameMethod_SameHash()
    {
        MethodInfo method = typeof(PropertyAttributeDatabaseTests)
            .GetMethod(nameof(ComputeTestId_SameMethod_SameHash))!;

        string id1 = TestCaseHelper.ComputeTestId(method);
        string id2 = TestCaseHelper.ComputeTestId(method);

        Assert.Equal(id1, id2);
    }

    [Fact]
    public void ComputeTestId_DifferentMethods_DifferentHashes()
    {
        MethodInfo method1 = typeof(PropertyAttributeDatabaseTests)
            .GetMethod(nameof(ComputeTestId_SameMethod_SameHash))!;
        MethodInfo method2 = typeof(PropertyAttributeDatabaseTests)
            .GetMethod(nameof(ComputeTestId_DifferentMethods_DifferentHashes))!;

        string id1 = TestCaseHelper.ComputeTestId(method1);
        string id2 = TestCaseHelper.ComputeTestId(method2);

        Assert.NotEqual(id1, id2);
    }
}
