using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text;
using Conjecture.Core;
using Conjecture.Core.Internal;
using Conjecture.Core.Internal.Database;
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

    [RequiresUnreferencedCode("Accesses type and parameter metadata via reflection; not trim-safe.")]
    internal static string ComputeTestId(MethodInfo method)
    {
        StringBuilder sb = new();
        sb.Append(method.DeclaringType?.FullName ?? method.DeclaringType?.Name ?? "Unknown");
        sb.Append('.');
        sb.Append(method.Name);
        sb.Append('(');
        ParameterInfo[] parameters = method.GetParameters();
        for (int i = 0; i < parameters.Length; i++)
        {
            if (i > 0)
            {
                sb.Append(',');
            }

            sb.Append(parameters[i].ParameterType.FullName ?? parameters[i].ParameterType.Name);
        }

        sb.Append(')');
        return TestIdHasher.Hash(sb.ToString());
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
            string testIdHash = ComputeTestId(methodInfo);

            ParameterInfo[] methodParams = methodInfo.GetParameters();
            using ExampleDatabase db = new(dbPath);
            result = TestRunner.Run(settings, data =>
            {
                object[] args = ParameterStrategyResolver.Resolve(methodParams, data);
                try
                {
                    methodInfo.Invoke(testInstance, args);
                }
                catch (TargetInvocationException ex) when (ex.InnerException is not null)
                {
                    System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                }
            }, db, testIdHash);
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
                ?? new Exception(BuildFailureMessage(result!, TestMethod.GetParameters()));
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

    [RequiresUnreferencedCode("Resolves parameter strategies by Type, which may be trimmed.")]
    internal static string BuildFailureMessage(TestRunResult result, ParameterInfo[] parameters)
    {
        ConjectureData replay = ConjectureData.ForRecord(result.Counterexample!);
        object[] values = ParameterStrategyResolver.Resolve(parameters, replay);
        IEnumerable<(string name, object value)> pairs = parameters.Zip(values, (p, v) => (p.Name!, (object)v!));
        return CounterexampleFormatter.Format(pairs, result.Seed!.Value, result.ExampleCount, result.ShrinkCount);
    }
}
