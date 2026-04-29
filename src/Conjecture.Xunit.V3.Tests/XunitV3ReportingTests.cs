// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Reflection;

using Conjecture.Core;
using Conjecture.Core.Internal;
using Conjecture.Xunit.V3;

using Xunit;

namespace Conjecture.Xunit.V3.Tests;

/// <summary>
/// Tests that the xUnit v3 [Property] adapter correctly wires CounterexampleFormatter,
/// ExampleDatabase, and StackTraceTrimmer into failure messages and database round-trips.
/// </summary>
public sealed class XunitV3ReportingTests : IDisposable
{
    private readonly string tempDir;
    private readonly ExampleDatabase db;

    public XunitV3ReportingTests()
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

#pragma warning disable IDE0060
    private static void PropertyWithInt(int x) { }
#pragma warning restore IDE0060

    private static ParameterInfo[] Params(string methodName) =>
        typeof(XunitV3ReportingTests)
            .GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static)!
            .GetParameters();

    // 1. Failure message contains "Falsifying example"
    [Fact]
    public async Task BuildFailureMessage_ContainsFalsifyingExampleText()
    {
        ConjectureSettings settings = new() { MaxExamples = 50, Seed = 1UL };
        ParameterInfo[] parameters = Params(nameof(PropertyWithInt));

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            int x = Strategy.Integers<int>().Generate(data);
            if (x > 5) { throw new Exception("fail"); }
        });

        Assert.False(result.Passed);
        string message = TestCaseHelper.BuildFailureMessage(result, parameters);
        Assert.Contains("Falsifying example", message);
    }

    // 2. Failure message with shrinks shows both "Falsifying example" and "Minimal counterexample"
    [Fact]
    public async Task BuildFailureMessage_WhenShrunk_ContainsMinimalCounterexampleSection()
    {
        // Seed 42: Strategy.Integers<int>() draws large ints; x > 5 ensures the initial failure
        // is some large value that shrinks to 6 — extremely unlikely to already be minimal.
        ConjectureSettings settings = new() { MaxExamples = 100, Seed = 42UL };
        ParameterInfo[] parameters = Params(nameof(PropertyWithInt));

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            int x = Strategy.Integers<int>().Generate(data);
            if (x > 5) { throw new Exception("fail"); }
        });

        Assert.False(result.Passed);
        Assert.True(result.ShrinkCount > 0, "Expected shrinking to occur; initial failing value should not be minimal.");
        string message = TestCaseHelper.BuildFailureMessage(result, parameters);
        Assert.Contains("Falsifying example", message);
        Assert.Contains("Minimal counterexample", message);
    }

    // 3. Database stores counterexample buffer when a run fails
    [Fact]
    public async Task Database_FailingRun_BufferSavedToDatabase()
    {
        ConjectureSettings settings = new() { MaxExamples = 10, UseDatabase = true };
        MethodInfo method = typeof(XunitV3ReportingTests)
            .GetMethod(nameof(Database_FailingRun_BufferSavedToDatabase))!;
        string testId = TestCaseHelper.ComputeTestId(method);

        await TestRunner.Run(settings, _ => throw new InvalidOperationException("fail"), db, testId);

        Assert.NotEmpty(db.Load(testId));
    }

    // 4. Database replays the stored buffer on a subsequent run
    [Fact]
    public async Task Database_SecondRun_ReplaysStoredBuffer()
    {
        ConjectureSettings settings = new() { MaxExamples = 10, UseDatabase = true };
        MethodInfo method = typeof(XunitV3ReportingTests)
            .GetMethod(nameof(Database_SecondRun_ReplaysStoredBuffer))!;
        string testId = TestCaseHelper.ComputeTestId(method);
        bool replayInvoked = false;

        await TestRunner.Run(settings, _ => throw new InvalidOperationException("fail"), db, testId);

        await TestRunner.Run(settings, data =>
        {
            if (data.IsReplay) { replayInvoked = true; }
            throw new InvalidOperationException("fail");
        }, db, testId);

        Assert.True(replayInvoked, "Stored buffer should have been replayed on the second run.");
    }
}

/// <summary>
/// RED: Verifies that a test class with an ITestOutputHelper constructor
/// receives the helper via constructor injection when executed via [Property].
/// Currently fails because PropertyTestCase.Run calls Activator.CreateInstance(type)
/// without passing constructorArguments, causing a MissingMethodException for a
/// class that has no parameterless constructor.
/// </summary>
public sealed class XunitV3ReportingOutputHelperTests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper output = output;

    [Property(MaxExamples = 1, Seed = 1)]
    public void Property_WithOutputHelper_OutputHelperInjected_WritesWithoutThrowing(int x)
    {
        output.WriteLine($"Generated x = {x}");
    }
}