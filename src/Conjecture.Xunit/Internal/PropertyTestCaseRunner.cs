// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using Conjecture.Core;
using Conjecture.Core.Internal;

using Microsoft.Extensions.Logging;

using Xunit.Abstractions;
using Xunit.Sdk;

namespace Conjecture.Xunit.Internal;

internal sealed class PropertyTestCaseRunner : XunitTestCaseRunner
{
    private readonly ConjectureSettings settings;
    // Stored so we can call Initialize(MessageBus, ITest) before the test runs and
    // Uninitialize() afterwards. xUnit v2 passes a Func<TestOutputHelper> factory in
    // constructorArguments; calling factory() yields the (not-yet-active) instance.
    private readonly TestOutputHelper? testOutputHelper;

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
        Func<TestOutputHelper>? factory = constructorArguments
            .OfType<Func<TestOutputHelper>>()
            .FirstOrDefault();
        ITestOutputHelper? direct = constructorArguments
            .OfType<ITestOutputHelper>()
            .FirstOrDefault();

        testOutputHelper = factory?.Invoke();

        Action<string>? writeLine = testOutputHelper is not null
            ? msg => testOutputHelper.WriteLine(msg)
            : direct is not null
                ? msg => direct.WriteLine(msg)
                : null;

        ILogger logger = TestOutputHelperLogger.FromWriteLine(writeLine);
        settings = new ConjectureSettings
        {
            MaxExamples = testCase.MaxExamples,
            Seed = testCase.Seed,
            UseDatabase = testCase.UseDatabase,
            MaxStrategyRejections = testCase.MaxStrategyRejections,
            Deadline = testCase.DeadlineMs > 0 ? TimeSpan.FromMilliseconds(testCase.DeadlineMs) : null,
            Targeting = testCase.Targeting,
            TargetingProportion = testCase.TargetingProportion,
            Logger = logger,
            ExportReproOnFailure = testCase.ExportReproOnFailure,
            ReproOutputPath = testCase.ReproOutputPath,
        };
    }

    [RequiresDynamicCode("xUnit test execution invokes test methods and creates instances via reflection.")]
    [RequiresUnreferencedCode("xUnit test execution accesses type and method metadata that may be trimmed.")]
    protected override async Task<RunSummary> RunTestAsync()
    {
        RunSummary summary = new() { Total = 1 };
        ITest test = CreateTest(TestCase, DisplayName);

        // Initialize TestOutputHelper so log writes during the test don't throw.
        testOutputHelper?.Initialize(MessageBus, test);

        if (!MessageBus.QueueMessage(new TestStarting(test)))
        {
            CancellationTokenSource.Cancel();
        }

        Stopwatch sw = Stopwatch.StartNew();
        Exception? setupFailure = null;
        TestRunResult? result = null;

        try
        {
            // TestOutputHelper is only active during a test — substitute the pre-initialized instance.
            object?[] resolvedArgs = Array.ConvertAll(ConstructorArguments,
                arg => arg is Func<TestOutputHelper> ? (object?)testOutputHelper : arg);
            object? testInstance = resolvedArgs.Length > 0
                ? TestClass.GetConstructors()
                    .FirstOrDefault(c => c.GetParameters().Length == resolvedArgs.Length)
                    ?.Invoke(resolvedArgs)
                    ?? Activator.CreateInstance(TestClass)
                : Activator.CreateInstance(TestClass);
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
                using ExampleDatabase db = new(dbPath, settings.Logger);
                result = TestCaseHelper.IsAsyncReturnType(methodInfo.ReturnType)
                    ? await TestRunner.RunAsync(settings, async data =>
                    {
                        object[] args = SharedParameterStrategyResolver.Resolve(methodParams, data);
                        await TestCaseHelper.InvokeAsync(methodInfo, testInstance, args);
                    }, db, testIdHash)
                    : await TestRunner.Run(settings, data =>
                    {
                        object[] args = SharedParameterStrategyResolver.Resolve(methodParams, data);
                        TestCaseHelper.InvokeSync(methodInfo, testInstance, args);
                    }, db, testIdHash);

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
            if (settings.ExportReproOnFailure && result is not null && !result.Passed && result.Counterexample is not null)
            {
                try
                {
                    MethodInfo methodInfo = TestMethod;
                    ParameterInfo[] methodParams = methodInfo.GetParameters();
                    ConjectureData replay = ConjectureData.ForRecord(result.Counterexample);
                    object[] values = SharedParameterStrategyResolver.Resolve(methodParams, replay);
                    IEnumerable<(string Name, object? Value, Type Type)> parameters = methodParams.Zip(values,
                        static (p, v) => (p.Name!, (object?)v, p.ParameterType));
                    ReproContext context = new(
                        TestClass.Name,
                        methodInfo.Name,
                        TestCaseHelper.IsAsyncReturnType(methodInfo.ReturnType),
                        parameters,
                        result.Seed!.Value,
                        result.ExampleCount,
                        result.ShrinkCount,
                        Conjecture.Core.Internal.TestFramework.Xunit,
                        DateTimeOffset.UtcNow);
                    ReproFileBuilder.WriteToFile(context, settings.ReproOutputPath);
                }
                catch (Exception ex)
                {
                    settings.Logger.LogError(ex, "Failed to write repro file");
                }
            }

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

        testOutputHelper?.Uninitialize();

        return summary;
    }
}