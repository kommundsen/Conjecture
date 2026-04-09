// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Reflection;

using Conjecture.Core;
using Conjecture.Core.Internal;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Conjecture.MSTest.Tests;

/// <summary>
/// Tests that the MSTest [Property] adapter correctly wires CounterexampleFormatter,
/// ExampleDatabase, and StackTraceTrimmer into failure messages and database round-trips.
/// </summary>
[TestClass]
public sealed class MSTestReportingTests
{
    private string tempDir = null!;
    private ExampleDatabase db = null!;

    [TestInitialize]
    public void Initialize()
    {
        tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        db = new(Path.Combine(tempDir, "conjecture.db"));
    }

    [TestCleanup]
    public void Cleanup()
    {
        db?.Dispose();
        try { Directory.Delete(tempDir, recursive: true); }
        catch (IOException) { }
    }

#pragma warning disable IDE0060
    private static void PropertyWithInt(int x) { }
#pragma warning restore IDE0060

    private static ParameterInfo[] Params(string methodName) =>
        typeof(MSTestReportingTests)
            .GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static)!
            .GetParameters();

    [TestMethod]
    public async Task BuildFailureMessage_ContainsFalsifyingExampleText()
    {
        ConjectureSettings settings = new() { MaxExamples = 50, Seed = 1UL };
        ParameterInfo[] parameters = Params(nameof(PropertyWithInt));

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            int x = Generate.Integers<int>().Generate(data);
            if (x > 5) { throw new Exception("fail"); }
        });

        Assert.IsFalse(result.Passed);
        string message = TestCaseHelper.BuildFailureMessage(result, parameters);
        Assert.IsTrue(message.Contains("Falsifying example"), $"Expected 'Falsifying example' in: {message}");
    }

    [TestMethod]
    public async Task BuildFailureMessage_WhenShrunk_ContainsBothOriginalAndMinimalSections()
    {
        ConjectureSettings settings = new() { MaxExamples = 100, Seed = 42UL };
        ParameterInfo[] parameters = Params(nameof(PropertyWithInt));

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            int x = Generate.Integers<int>().Generate(data);
            if (x > 5) { throw new Exception("fail"); }
        });

        Assert.IsFalse(result.Passed);
        Assert.IsTrue(result.ShrinkCount > 0, "Expected shrinking to occur; initial value should not already be minimal.");
        string message = TestCaseHelper.BuildFailureMessage(result, parameters);
        Assert.IsTrue(message.Contains("Falsifying example"), $"Expected 'Falsifying example' in: {message}");
        Assert.IsTrue(message.Contains("Minimal counterexample"), $"Expected 'Minimal counterexample' in: {message}");
    }

    [TestMethod]
    public async Task BuildFailureMessage_ContainsSeedReproductionLine()
    {
        ConjectureSettings settings = new() { MaxExamples = 50, Seed = 0xDEADBEEFUL };
        ParameterInfo[] parameters = Params(nameof(PropertyWithInt));

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            int x = Generate.Integers<int>().Generate(data);
            if (x > 5) { throw new Exception("fail"); }
        });

        Assert.IsFalse(result.Passed);
        string message = TestCaseHelper.BuildFailureMessage(result, parameters);
        Assert.IsTrue(
            message.Contains("Reproduce with: [Property(Seed = 0xDEADBEEF)]"),
            $"Expected seed line in: {message}");
    }

    [TestMethod]
    public async Task Database_FailingRun_BufferSavedToDatabase()
    {
        ConjectureSettings settings = new() { MaxExamples = 10, UseDatabase = true };
        MethodInfo method = typeof(MSTestReportingTests)
            .GetMethod(nameof(Database_FailingRun_BufferSavedToDatabase))!;
        string testId = TestCaseHelper.ComputeTestId(method);

        await TestRunner.Run(settings, _ => throw new InvalidOperationException("fail"), db, testId);

        Assert.IsTrue(db.Load(testId).Count > 0);
    }

    [TestMethod]
    public async Task Database_SecondRun_ReplaysStoredBuffer()
    {
        ConjectureSettings settings = new() { MaxExamples = 10, UseDatabase = true };
        MethodInfo method = typeof(MSTestReportingTests)
            .GetMethod(nameof(Database_SecondRun_ReplaysStoredBuffer))!;
        string testId = TestCaseHelper.ComputeTestId(method);
        bool replayInvoked = false;

        await TestRunner.Run(settings, _ => throw new InvalidOperationException("fail"), db, testId);

        await TestRunner.Run(settings, data =>
        {
            if (data.IsReplay) { replayInvoked = true; }
            throw new InvalidOperationException("fail");
        }, db, testId);

        Assert.IsTrue(replayInvoked, "Stored buffer should have been replayed on the second run.");
    }
}