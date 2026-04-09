// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Reflection;

using Conjecture.Core;
using Conjecture.Core.Internal;

using NUnit.Framework;

namespace Conjecture.NUnit.Tests;

/// <summary>
/// Tests that the NUnit [Property] adapter correctly wires CounterexampleFormatter,
/// ExampleDatabase, and StackTraceTrimmer into failure messages and database round-trips.
/// </summary>
[TestFixture]
public sealed class NUnitReportingTests
{
    private string tempDir = null!;
    private ExampleDatabase db = null!;

    [SetUp]
    public void SetUp()
    {
        tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        db = new(Path.Combine(tempDir, "conjecture.db"));
    }

    [TearDown]
    public void TearDown()
    {
        db?.Dispose();
        try { Directory.Delete(tempDir, recursive: true); }
        catch (IOException) { }
    }

#pragma warning disable IDE0060
    private static void PropertyWithInt(int x) { }
#pragma warning restore IDE0060

    private static ParameterInfo[] Params(string methodName) =>
        typeof(NUnitReportingTests)
            .GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static)!
            .GetParameters();

    [Test]
    public async Task BuildFailureMessage_ContainsFalsifyingExampleText()
    {
        ConjectureSettings settings = new() { MaxExamples = 50, Seed = 1UL };
        ParameterInfo[] parameters = Params(nameof(PropertyWithInt));

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            int x = Generate.Integers<int>().Generate(data);
            if (x > 5) { throw new Exception("fail"); }
        });

        Assert.That(result.Passed, Is.False);
        string message = TestCaseHelper.BuildFailureMessage(result, parameters);
        Assert.That(message, Does.Contain("Falsifying example"));
    }

    [Test]
    public async Task BuildFailureMessage_WhenShrunk_ContainsBothOriginalAndMinimalSections()
    {
        ConjectureSettings settings = new() { MaxExamples = 100, Seed = 42UL };
        ParameterInfo[] parameters = Params(nameof(PropertyWithInt));

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            int x = Generate.Integers<int>().Generate(data);
            if (x > 5) { throw new Exception("fail"); }
        });

        Assert.That(result.Passed, Is.False);
        Assert.That(result.ShrinkCount, Is.GreaterThan(0), "Expected shrinking to occur; initial value should not already be minimal.");
        string message = TestCaseHelper.BuildFailureMessage(result, parameters);
        Assert.That(message, Does.Contain("Falsifying example"));
        Assert.That(message, Does.Contain("Minimal counterexample"));
    }

    [Test]
    public async Task BuildFailureMessage_ContainsSeedReproductionLine()
    {
        ConjectureSettings settings = new() { MaxExamples = 50, Seed = 0xDEADBEEFUL };
        ParameterInfo[] parameters = Params(nameof(PropertyWithInt));

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            int x = Generate.Integers<int>().Generate(data);
            if (x > 5) { throw new Exception("fail"); }
        });

        Assert.That(result.Passed, Is.False);
        string message = TestCaseHelper.BuildFailureMessage(result, parameters);
        Assert.That(message, Does.Contain("Reproduce with: [Property(Seed = 0xDEADBEEF)]"));
    }

    [Test]
    public async Task Database_FailingRun_BufferSavedToDatabase()
    {
        ConjectureSettings settings = new() { MaxExamples = 10, UseDatabase = true };
        MethodInfo method = typeof(NUnitReportingTests)
            .GetMethod(nameof(Database_FailingRun_BufferSavedToDatabase))!;
        string testId = TestCaseHelper.ComputeTestId(method);

        await TestRunner.Run(settings, _ => throw new InvalidOperationException("fail"), db, testId);

        Assert.That(db.Load(testId), Is.Not.Empty);
    }

    [Test]
    public async Task Database_SecondRun_ReplaysStoredBuffer()
    {
        ConjectureSettings settings = new() { MaxExamples = 10, UseDatabase = true };
        MethodInfo method = typeof(NUnitReportingTests)
            .GetMethod(nameof(Database_SecondRun_ReplaysStoredBuffer))!;
        string testId = TestCaseHelper.ComputeTestId(method);
        bool replayInvoked = false;

        await TestRunner.Run(settings, _ => throw new InvalidOperationException("fail"), db, testId);

        await TestRunner.Run(settings, data =>
        {
            if (data.IsReplay) { replayInvoked = true; }
            throw new InvalidOperationException("fail");
        }, db, testId);

        Assert.That(replayInvoked, Is.True, "Stored buffer should have been replayed on the second run.");
    }
}