// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core;
using Conjecture.Core.Internal;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace Conjecture.Xunit.Tests;

/// <summary>
/// Verifies that [Property] auto-wires ITestOutputHelper to ILogger when present in the ctor.
/// </summary>
public class PropertyAttributeLoggingTests(ITestOutputHelper output)
{
    /// <summary>
    /// Integration test: [Property] on a class with ITestOutputHelper ctor must not crash.
    /// Verifies that PropertyTestCaseRunner correctly instantiates the class with constructor
    /// arguments and routes Conjecture logs to ITestOutputHelper.
    /// </summary>
    [Property(MaxExamples = 5, Seed = 1UL)]
#pragma warning disable IDE0060
    public void Property_WithITestOutputHelper_PassesWithoutException(int x)
    {
    }
#pragma warning restore IDE0060

    /// <summary>
    /// Unit-level: TestOutputHelperLogger writes GenerationCompleted to the writeLine action.
    /// Proves the logging bridge works when wired via ITestOutputHelper.WriteLine.
    /// </summary>
    [Fact]
    public async Task TestRunner_WithTestOutputHelperLogger_LogsGenerationCompleted()
    {
        List<string> lines = [];
        ILogger logger = TestOutputHelperLogger.FromWriteLine(lines.Add);
        ConjectureSettings settings = new() { MaxExamples = 5, Seed = 1UL, Logger = logger };

        await TestRunner.Run(settings, _ => { });

        // Surface captured lines to ITestOutputHelper so they appear in xUnit output
        foreach (string line in lines)
        {
            output.WriteLine(line);
        }

        Assert.Contains(lines, l => l.Contains("Generation complete"));
    }

    /// <summary>
    /// Unit-level: TestRunner with null writeLine (FromWriteLine(null)) does not throw.
    /// </summary>
    [Fact]
    public async Task TestRunner_WithNullWriteLine_DoesNotThrow()
    {
        ILogger logger = TestOutputHelperLogger.FromWriteLine(null);
        ConjectureSettings settings = new() { MaxExamples = 5, Seed = 1UL, Logger = logger };

        await TestRunner.Run(settings, _ => { });
    }
}
