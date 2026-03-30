using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using Conjecture.Core;
using Conjecture.Core.Internal;
using Conjecture.Core.Internal.Database;
using Conjecture.MSTest.Internal;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Conjecture.MSTest;

/// <summary>Marks a method as a Conjecture property-based test (MSTest).</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class PropertyAttribute : TestMethodAttribute
{
    /// <inheritdoc/>
    public PropertyAttribute(
        [CallerFilePath] string sourceFile = "",
        [CallerLineNumber] int sourceLine = -1)
        : base(sourceFile, sourceLine) { }

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

    /// <inheritdoc/>
    [RequiresDynamicCode("Property test execution uses MakeGenericMethod for typed strategy dispatch.")]
    [RequiresUnreferencedCode("Property test execution accesses parameter type metadata that may be trimmed.")]
    public override async Task<TestResult[]> ExecuteAsync(ITestMethod testMethod)
    {
        MethodInfo methodInfo = testMethod.MethodInfo;
        ParameterInfo[] methodParams = testMethod.ParameterTypes;

        ExampleAttribute[] exampleAttrs = methodInfo
            .GetCustomAttributes(typeof(ExampleAttribute), inherit: false)
            .Cast<ExampleAttribute>()
            .ToArray();

        foreach (ExampleAttribute ea in exampleAttrs)
        {
            PropertyTestMethodAttribute.ValidateExampleArgs(ea, methodParams);
        }

        ConjectureSettings settings = new()
        {
            MaxExamples = MaxExamples,
            Seed = Seed == 0UL ? null : Seed,
            UseDatabase = UseDatabase,
            MaxStrategyRejections = MaxStrategyRejections,
            Deadline = DeadlineMs > 0 ? TimeSpan.FromMilliseconds(DeadlineMs) : null,
        };

        string dbPath = Path.Combine(settings.DatabasePath, "conjecture.db");
        string testIdHash = PropertyTestMethodAttribute.ComputeTestId(methodInfo);

        int explicitCount = 0;
        Exception? explicitFailure = null;

        foreach (ExampleAttribute ea in exampleAttrs)
        {
            object[] exampleArgs = Array.ConvertAll(ea.Arguments, a => a!);
            TestResult invResult = await testMethod.InvokeAsync(exampleArgs);
            if (invResult.Outcome != UnitTestOutcome.Passed)
            {
                explicitFailure = invResult.TestFailureException
                    ?? new Exception(PropertyTestMethodAttribute.BuildExampleFailureMessage(
                        ea, methodParams, new Exception($"Example failed with outcome: {invResult.Outcome}")));
                break;
            }

            explicitCount++;
        }

        if (explicitFailure is not null)
        {
            return [new TestResult { Outcome = UnitTestOutcome.Failed, TestFailureException = explicitFailure }];
        }

        using ExampleDatabase db = new(dbPath);
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

        if (result.Passed)
        {
            return [new TestResult { Outcome = UnitTestOutcome.Passed }];
        }

        return
        [
            new TestResult
            {
                Outcome = UnitTestOutcome.Failed,
                TestFailureException = new AssertFailedException(
                    PropertyTestMethodAttribute.BuildFailureMessage(result, methodParams)),
            }
        ];
    }
}