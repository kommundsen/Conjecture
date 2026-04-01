// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Conjecture.Core;
using Conjecture.Core.Internal;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Conjecture.Xunit.Internal;

internal sealed class PropertyTestCaseRunner : XunitTestCaseRunner
{
    private readonly ConjectureSettings settings;

    internal PropertyTestCaseRunner(
        PropertyTestCase testCase,
        string displayName,
        string? skipReason,
        object[] constructorArguments,
        IMessageBus messageBus,
        ExceptionAggregator aggregator,
        CancellationTokenSource cancellationTokenSource)
        : base(testCase, displayName, skipReason, constructorArguments,
               Array.Empty<object>(), messageBus, aggregator, cancellationTokenSource)
    {
        settings = new ConjectureSettings
        {
            MaxExamples = testCase.MaxExamples,
            Seed = testCase.Seed,
            UseDatabase = testCase.UseDatabase,
            MaxStrategyRejections = testCase.MaxStrategyRejections,
            Deadline = testCase.DeadlineMs > 0 ? TimeSpan.FromMilliseconds(testCase.DeadlineMs) : null,
        };
    }

    [RequiresDynamicCode("xUnit test execution invokes test methods and creates instances via reflection.")]
    [RequiresUnreferencedCode("xUnit test execution accesses type and method metadata that may be trimmed.")]
    protected override async Task<RunSummary> RunTestAsync()
    {
        RunSummary summary = new() { Total = 1 };
        ITest test = CreateTest(TestCase, DisplayName);

        if (!MessageBus.QueueMessage(new TestStarting(test)))
        {
            CancellationTokenSource.Cancel();
        }

        Stopwatch sw = Stopwatch.StartNew();
        Exception? setupFailure = null;
        TestRunResult? result = null;

        try
        {
            object? testInstance = Activator.CreateInstance(TestClass);
            MethodInfo methodInfo = TestMethod;
            string dbPath = Path.Combine(settings.DatabasePath, "conjecture.db");
            string testIdHash = TestCaseHelper.ComputeTestId(methodInfo);

            ParameterInfo[] methodParams = methodInfo.GetParameters();

            ExampleAttribute[] exampleAttrs = methodInfo
                .GetCustomAttributes(typeof(ExampleAttribute), inherit: false)
                .Cast<ExampleAttribute>()
                .ToArray();

            foreach (ExampleAttribute exampleAttr in exampleAttrs)
            {
                TestCaseHelper.ValidateExampleArgs(exampleAttr, methodParams);
            }

            int explicitCount = 0;
            Exception? explicitFailure = null;

            foreach (ExampleAttribute exampleAttr in exampleAttrs)
            {
                try
                {
                    object? returnVal = methodInfo.Invoke(testInstance, exampleAttr.Arguments);
                    if (returnVal is Task task) { await task; }
                    else if (returnVal is ValueTask vt) { await vt; }
                    explicitCount++;
                }
                catch (TargetInvocationException ex) when (ex.InnerException is not null)
                {
                    explicitFailure = new Exception(TestCaseHelper.BuildExampleFailureMessage(exampleAttr, methodParams, ex.InnerException), ex.InnerException);
                    break;
                }
            }

            if (explicitFailure is not null)
            {
                setupFailure = explicitFailure;
            }
            else
            {
                using ExampleDatabase db = new(dbPath);
                if (TestCaseHelper.IsAsyncReturnType(methodInfo.ReturnType))
                {
                    result = await TestRunner.RunAsync(settings, async data =>
                    {
                        object[] args = SharedParameterStrategyResolver.Resolve(methodParams, data);
                        await TestCaseHelper.InvokeAsync(methodInfo, testInstance, args);
                    }, db, testIdHash);
                }
                else
                {
                    result = await TestRunner.Run(settings, data =>
                    {
                        object[] args = SharedParameterStrategyResolver.Resolve(methodParams, data);
                        TestCaseHelper.InvokeSync(methodInfo, testInstance, args);
                    }, db, testIdHash);
                }

                if (explicitCount > 0)
                {
                    result = TestRunResult.WithExtraExamples(result, explicitCount);
                }
            }
        }
        catch (Exception ex)
        {
            setupFailure = ex;
        }

        sw.Stop();
        decimal elapsed = (decimal)sw.Elapsed.TotalSeconds;

        if (setupFailure is null && result!.Passed)
        {
            if (!MessageBus.QueueMessage(new TestPassed(test, elapsed, null)))
            {
                CancellationTokenSource.Cancel();
            }
        }
        else
        {
            summary.Failed = 1;
            Exception failure = setupFailure
                ?? new Exception(TestCaseHelper.BuildFailureMessage(result!, TestMethod.GetParameters()));
            if (!MessageBus.QueueMessage(new TestFailed(test, elapsed, null, failure)))
            {
                CancellationTokenSource.Cancel();
            }
        }

        if (!MessageBus.QueueMessage(new TestFinished(test, elapsed, null)))
        {
            CancellationTokenSource.Cancel();
        }

        return summary;
    }
}