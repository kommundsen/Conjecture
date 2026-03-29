using System.Diagnostics;
using System.Reflection;
using Conjecture.Core;
using Conjecture.Core.Internal;
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

    protected override async Task<RunSummary> RunTestAsync()
    {
        var summary = new RunSummary { Total = 1 };
        var test = CreateTest(TestCase, DisplayName);

        if (!MessageBus.QueueMessage(new TestStarting(test)))
        {
            CancellationTokenSource.Cancel();
        }

        var sw = Stopwatch.StartNew();
        Exception? setupFailure = null;
        TestRunResult? result = null;

        try
        {
            var testInstance = Activator.CreateInstance(TestClass);
            var methodInfo = TestMethod;

            result = TestRunner.Run(settings, data =>
            {
                var args = ParameterStrategyResolver.Resolve(methodInfo.GetParameters(), data);
                try
                {
                    methodInfo.Invoke(testInstance, args);
                }
                catch (TargetInvocationException ex) when (ex.InnerException is not null)
                {
                    System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                }
            });
        }
        catch (Exception ex)
        {
            setupFailure = ex;
        }

        sw.Stop();
        var elapsed = (decimal)sw.Elapsed.TotalSeconds;

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
            var failure = setupFailure
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

    internal static string BuildFailureMessage(TestRunResult result, ParameterInfo[] parameters)
    {
        var replay = ConjectureData.ForRecord(result.Counterexample!);
        var values = ParameterStrategyResolver.Resolve(parameters, replay);
        var pairs = parameters.Zip(values, (p, v) => (p.Name!, (object)v));
        return CounterexampleFormatter.Format(pairs, result.Seed!.Value, result.ExampleCount, result.ShrinkCount);
    }
}
