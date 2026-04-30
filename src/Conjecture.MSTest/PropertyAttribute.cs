// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;

using Conjecture.Core;
using Conjecture.Core.Internal;

using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Conjecture.MSTest;

/// <summary>Marks a method as a Conjecture property-based test (MSTest).</summary>
/// <inheritdoc/>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class PropertyAttribute(
    [CallerFilePath] string sourceFile = "",
    [CallerLineNumber] int sourceLine = -1) : TestMethodAttribute(sourceFile, sourceLine), IPropertyTest
{

    /// <summary>Maximum number of examples to generate. Defaults to 100.</summary>
    public int MaxExamples { get; set; } = 100;

    /// <summary>Optional fixed seed for deterministic runs. 0 means use a random seed.</summary>
    public ulong Seed { get; set; }

    /// <summary>Whether to use the example database. Defaults to <see langword="true"/>.</summary>
    public bool UseDatabase { get; set; } = true;

    /// <summary>Maximum number of times a strategy may reject a value. Defaults to 5.</summary>
    public int MaxStrategyRejections { get; set; } = 5;

    /// <summary>Deadline for each test run in milliseconds. 0 means no deadline.</summary>
    public int DeadlineMs { get; set; }

    /// <summary>Whether to run a targeting phase after generation. Defaults to <see langword="true"/>.</summary>
    public bool Targeting { get; set; } = true;

    /// <summary>Fraction of MaxExamples budget allocated to the targeting phase. Defaults to 0.5.</summary>
    public double TargetingProportion { get; set; } = 0.5;

    /// <inheritdoc/>
    [RequiresDynamicCode("Property test execution uses MakeGenericMethod for typed strategy dispatch.")]
    [RequiresUnreferencedCode("Property test execution accesses parameter type metadata that may be trimmed.")]
    public override async Task<TestResult[]> ExecuteAsync(ITestMethod testMethod)
    {
        MethodInfo methodInfo = testMethod.MethodInfo;
        ParameterInfo[] methodParams = testMethod.ParameterTypes;

        SampleAttribute[] sampleAttrs = methodInfo
            .GetCustomAttributes(typeof(SampleAttribute), inherit: false)
            .Cast<SampleAttribute>()
            .ToArray();

        foreach (SampleAttribute sa in sampleAttrs)
        {
            TestCaseHelper.ValidateSampleArgs(sa, methodParams);
        }

        ILogger logger = TestOutputHelperLogger.FromWriteLine(Console.WriteLine);
        ConjectureSettings settings = ConjectureSettings.From(this, logger);

        string dbPath = Path.Combine(settings.DatabasePath, "conjecture.db");
        string testIdHash = TestCaseHelper.ComputeTestId(methodInfo);

        int explicitCount = 0;
        Exception? explicitFailure = null;

        foreach (SampleAttribute sa in sampleAttrs)
        {
            object[] sampleArgs = Array.ConvertAll(sa.Arguments, a => a!);
            TestResult invResult = await testMethod.InvokeAsync(sampleArgs);
            if (invResult.Outcome != UnitTestOutcome.Passed)
            {
                explicitFailure = invResult.TestFailureException
                    ?? new Exception(TestCaseHelper.BuildSampleFailureMessage(
                        sa, methodParams, new Exception($"Sample failed with outcome: {invResult.Outcome}")));
                break;
            }

            explicitCount++;
        }

        if (explicitFailure is not null)
        {
            return [new TestResult { Outcome = UnitTestOutcome.Failed, TestFailureException = explicitFailure }];
        }

        using ExampleDatabase db = new(dbPath, settings.Logger);
        TestRunResult result = await TestRunner.RunAsync(settings, async data =>
        {
            object[] args = SharedParameterStrategyResolver.Resolve(methodParams, data);
            TestResult invResult = await testMethod.InvokeAsync(args);
            if (invResult.Outcome != UnitTestOutcome.Passed)
            {
                Exception ex = invResult.TestFailureException ?? new Exception("Property test invocation failed");
                ExceptionDispatchInfo.Capture(ex).Throw();
            }
        }, db, testIdHash);

        if (explicitCount > 0)
        {
            result = TestRunResult.WithExtraExamples(result, explicitCount);
        }

        return result.Passed
            ? [new TestResult { Outcome = UnitTestOutcome.Passed }]
            : [
            new TestResult
            {
                Outcome = UnitTestOutcome.Failed,
                TestFailureException = new AssertFailedException(
                    TestCaseHelper.BuildFailureMessage(result, methodParams)),
            }
        ];
    }
}