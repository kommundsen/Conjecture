// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core;
using Conjecture.Core.Internal;

using Microsoft.Extensions.Logging;

using NUnit.Framework;

using Conjecture.Abstractions.Testing;

namespace Conjecture.NUnit.Tests;

/// <summary>
/// Verifies that [Property] auto-wires TestContext.Out to ILogger.
/// </summary>
[TestFixture]
public class PropertyAttributeLoggingTests
{
    /// <summary>
    /// Integration test: [Property] on an NUnit test must not crash and should
    /// route Conjecture logs to TestContext.Out.
    /// </summary>
    [Property(MaxExamples = 5, Seed = 1UL)]
#pragma warning disable IDE0060
    public void Property_WithTestContextOut_PassesWithoutException(int x)
    {
    }
#pragma warning restore IDE0060

    /// <summary>
    /// Unit-level: TestOutputHelperLogger writes GenerationCompleted to a writeLine action.
    /// </summary>
    [Test]
    public async Task TestRunner_WithTestContextOutLogger_LogsGenerationCompleted()
    {
        List<string> lines = [];
        ILogger logger = TestOutputHelperLogger.FromWriteLine(lines.Add);
        ConjectureSettings settings = new() { MaxExamples = 5, Seed = 1UL, Logger = logger };

        await TestRunner.Run(settings, _ => { });

        foreach (string line in lines)
        {
            TestContext.Out.WriteLine(line);
        }

        Assert.That(lines, Has.Some.Contains("Generation complete"));
    }

    /// <summary>
    /// Unit-level: TestRunner with null writeLine (FromWriteLine(null)) does not throw.
    /// </summary>
    [Test]
    public async Task TestRunner_WithNullWriteLine_DoesNotThrow()
    {
        ILogger logger = TestOutputHelperLogger.FromWriteLine(null);
        ConjectureSettings settings = new() { MaxExamples = 5, Seed = 1UL, Logger = logger };

        await TestRunner.Run(settings, _ => { });
    }
}