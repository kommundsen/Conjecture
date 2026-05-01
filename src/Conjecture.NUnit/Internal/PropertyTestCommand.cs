// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using Conjecture.Core;
using Conjecture.Core.Internal;

using NUnit.Framework;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;
using NUnit.Framework.Internal.Commands;

using ILogger = Microsoft.Extensions.Logging.ILogger;

using Conjecture.Abstractions.Testing;

namespace Conjecture.NUnit.Internal;

internal sealed class PropertyTestCommand(TestCommand innerCommand, IPropertyTest attr)
    : DelegatingTestCommand(innerCommand)
{

    [RequiresDynamicCode("Property test execution uses MakeGenericMethod for typed strategy dispatch.")]
    [RequiresUnreferencedCode("Property test execution accesses parameter type metadata that may be trimmed.")]
    public override TestResult Execute(TestExecutionContext context)
    {
        MethodInfo methodInfo = context.CurrentTest.Method!.MethodInfo;
        object? testInstance = context.TestObject;
        ParameterInfo[] methodParams = methodInfo.GetParameters();

        SampleAttribute[] sampleAttrs = methodInfo
            .GetCustomAttributes(typeof(SampleAttribute), inherit: false)
            .Cast<SampleAttribute>()
            .ToArray();

        foreach (SampleAttribute sa in sampleAttrs)
        {
            TestCaseHelper.ValidateSampleArgs(sa, methodParams);
        }

        ILogger logger = TestOutputLogger.FromWriteLine(msg => TestContext.Out.WriteLine(msg));
        ConjectureSettings settings = ConjectureSettings.From(attr, logger);

        string dbPath = Path.Combine(settings.DatabasePath, "conjecture.db");
        string testIdHash = TestCaseHelper.ComputeTestId(methodInfo);
        bool isAsync = TestCaseHelper.IsAsyncReturnType(methodInfo.ReturnType);

        try
        {
            int explicitCount = 0;
            Exception? explicitFailure = null;

            foreach (SampleAttribute sa in sampleAttrs)
            {
                try
                {
                    object? returnVal = methodInfo.Invoke(testInstance, sa.Arguments);
                    if (returnVal is Task t) { RunTask(t); }
                    else if (returnVal is ValueTask vt) { RunTask(vt.AsTask()); }
                    explicitCount++;
                }
                catch (TargetInvocationException ex) when (ex.InnerException is not null)
                {
                    explicitFailure = new Exception(
                        TestCaseHelper.BuildSampleFailureMessage(sa, methodParams, ex.InnerException),
                        ex.InnerException);
                    break;
                }
            }

            if (explicitFailure is not null)
            {
                context.CurrentResult.SetResult(ResultState.Failure, explicitFailure.Message);
                return context.CurrentResult;
            }

            using ExampleDatabase db = new(dbPath, settings.Logger);
            Task<TestRunResult> runTask = isAsync
                ? TestRunner.RunAsync(settings, async data =>
                {
                    object[] args = SharedParameterStrategyResolver.Resolve(methodParams, data);
                    await TestCaseHelper.InvokeAsync(methodInfo, testInstance, args);
                }, db, testIdHash)
                : TestRunner.Run(settings, data =>
                {
                    object[] args = SharedParameterStrategyResolver.Resolve(methodParams, data);
                    TestCaseHelper.InvokeSync(methodInfo, testInstance, args);
                }, db, testIdHash);

            // NUnit constraint: DelegatingTestCommand.Execute() has no async override.
            // TODO: revisit when NUnit provides an async test command API.
            // GetAwaiter().GetResult() is unavoidable here; safe on NUnit's thread-pool threads.
            TestRunResult result = runTask.GetAwaiter().GetResult();

            if (explicitCount > 0)
            {
                result = TestRunResult.WithExtraExamples(result, explicitCount);
            }

            if (result.Passed)
            {
                context.CurrentResult.SetResult(ResultState.Success);
            }
            else
            {
                context.CurrentResult.SetResult(
                    ResultState.Failure,
                    TestCaseHelper.BuildFailureMessage(result, methodParams));
            }
        }
        catch (Exception ex)
        {
            context.CurrentResult.RecordException(ex);
        }

        return context.CurrentResult;
    }

    // NUnit constraint: Execute() is sync-only; no async alternative exists.
    private static void RunTask(Task task)
    {
        task.GetAwaiter().GetResult();
    }
}