// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using Conjecture.Core;
using Conjecture.Core.Internal;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Xunit;
using Xunit.Sdk;
using Xunit.v3;

using Conjecture.Abstractions.Testing;

namespace Conjecture.Xunit.V3.Internal;

internal sealed class PropertyTestCase : XunitTestCase, ISelfExecutingXunitTestCase
{
    internal int MaxExamples { get; private set; }
    internal ulong? Seed { get; private set; }
    internal bool Database { get; private set; }
    internal int MaxStrategyRejections { get; private set; }
    internal int DeadlineMs { get; private set; }
    internal bool Targeting { get; private set; } = true;
    internal double TargetingProportion { get; private set; } = 0.5;

    [Obsolete("For deserialization only", error: false)]
    public PropertyTestCase() { }

    internal PropertyTestCase(
        IXunitTestMethod testMethod,
        string testCaseDisplayName,
        string uniqueID,
        bool @explicit,
        Type[]? skipExceptions,
        string? skipReason,
        Type? skipType,
        string? skipUnless,
        string? skipWhen,
        Dictionary<string, HashSet<string>>? traits,
        object[]? testMethodArguments,
        string? sourceFilePath,
        int? sourceLineNumber,
        int maxExamples,
        ulong? seed,
        bool database,
        int maxStrategyRejections,
        int deadlineMs,
        bool targeting,
        double targetingProportion)
        : base(testMethod, testCaseDisplayName, uniqueID, @explicit, skipExceptions,
               skipReason, skipType, skipUnless, skipWhen, traits,
               testMethodArguments, sourceFilePath, sourceLineNumber, timeout: null)
    {
        MaxExamples = maxExamples;
        Seed = seed;
        Database = database;
        MaxStrategyRejections = maxStrategyRejections;
        DeadlineMs = deadlineMs;
        Targeting = targeting;
        TargetingProportion = targetingProportion;
    }

    protected override void Serialize(IXunitSerializationInfo info)
    {
        base.Serialize(info);
        info.AddValue("MaxExamples", MaxExamples);
        info.AddValue("Seed", Seed.HasValue ? Seed.Value.ToString() : null);
        info.AddValue("Database", Database);
        info.AddValue("MaxStrategyRejections", MaxStrategyRejections);
        info.AddValue("DeadlineMs", DeadlineMs);
        info.AddValue("Targeting", Targeting);
        info.AddValue("TargetingProportion", TargetingProportion);
    }

    protected override void Deserialize(IXunitSerializationInfo info)
    {
        base.Deserialize(info);
        MaxExamples = info.GetValue<int>("MaxExamples");
        string? seedStr = info.GetValue<string?>("Seed");
        Seed = seedStr is not null ? ulong.Parse(seedStr) : null;
        Database = info.GetValue<bool>("Database");
        MaxStrategyRejections = info.GetValue<int>("MaxStrategyRejections");
        DeadlineMs = info.GetValue<int>("DeadlineMs");
        Targeting = info.GetValue<bool>("Targeting");
        TargetingProportion = info.GetValue<double>("TargetingProportion");
    }

    [RequiresDynamicCode("xUnit test execution invokes test methods via reflection.")]
    [RequiresUnreferencedCode("xUnit test execution accesses type and method metadata that may be trimmed.")]
    public async ValueTask<RunSummary> Run(
        ExplicitOption explicitOption,
        IMessageBus messageBus,
        object?[] constructorArguments,
        ExceptionAggregator aggregator,
        CancellationTokenSource cancellationTokenSource)
    {
        RunSummary summary = new() { Total = 1 };

        IReadOnlyDictionary<string, IReadOnlyCollection<string>> traits =
            Traits.ToDictionary(kvp => kvp.Key, kvp => (IReadOnlyCollection<string>)kvp.Value.ToList());

        XunitTest test = new(this, TestMethod, null, null, null, null, null, TestCaseDisplayName, 0, traits, null, Array.Empty<object?>());

        string assemblyID = TestMethod.TestClass.TestCollection.TestAssembly.UniqueID;
        string collectionID = TestMethod.TestClass.TestCollection.UniqueID;
        string? classID = TestMethod.TestClass.UniqueID;
        string? methodID = TestMethod.UniqueID;
        string caseID = UniqueID;
        string testID = test.UniqueID;

        if (!messageBus.QueueMessage(new TestStarting
        {
            AssemblyUniqueID = assemblyID,
            TestCollectionUniqueID = collectionID,
            TestClassUniqueID = classID,
            TestMethodUniqueID = methodID,
            TestCaseUniqueID = caseID,
            TestUniqueID = testID,
            TestDisplayName = TestCaseDisplayName,
            Explicit = explicitOption == ExplicitOption.Only,
            StartTime = DateTimeOffset.UtcNow,
            Timeout = 0,
            Traits = traits,
        }))
        {
            cancellationTokenSource.Cancel();
        }

        Stopwatch sw = Stopwatch.StartNew();
        Exception? failure = null;
        TestRunResult? result = null;

        try
        {
            MethodInfo methodInfo = TestMethod.Method;
            object? testInstance;
            ILogger logger = NullLogger.Instance;
            if (constructorArguments is { Length: > 0 })
            {
                TestOutputHelper outputHelper = new();
                outputHelper.Initialize(messageBus, test);
                logger = TestOutputHelperLogger.FromWriteLine(outputHelper.WriteLine);
                testInstance = Activator.CreateInstance(TestMethod.TestClass.Class, ResolveConstructorArguments(constructorArguments, outputHelper));
            }
            else
            {
                testInstance = Activator.CreateInstance(TestMethod.TestClass.Class);
            }

            ConjectureSettings settings = new()
            {
                MaxExamples = MaxExamples,
                Seed = Seed,
                Database = Database,
                MaxStrategyRejections = MaxStrategyRejections,
                Deadline = DeadlineMs > 0 ? TimeSpan.FromMilliseconds(DeadlineMs) : null,
                Targeting = Targeting,
                TargetingProportion = TargetingProportion,
                Logger = logger,
            };

            string dbPath = Path.Combine(settings.DatabasePath, "conjecture.db");
            string testIdHash = TestCaseHelper.ComputeTestId(methodInfo);

            ParameterInfo[] methodParams = methodInfo.GetParameters();

            SampleAttribute[] sampleAttrs = methodInfo
                .GetCustomAttributes(typeof(SampleAttribute), inherit: false)
                .Cast<SampleAttribute>()
                .ToArray();

            foreach (SampleAttribute sa in sampleAttrs)
            {
                TestCaseHelper.ValidateSampleArgs(sa, methodParams);
            }

            int explicitCount = 0;
            Exception? explicitFailure = null;

            foreach (SampleAttribute sa in sampleAttrs)
            {
                try
                {
                    object? returnVal = methodInfo.Invoke(testInstance, sa.Arguments);
                    if (returnVal is Task task) { await task; }
                    else if (returnVal is ValueTask vt) { await vt; }
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
                failure = explicitFailure;
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

                if (!result.Passed)
                {
                    failure = new Exception(TestCaseHelper.BuildFailureMessage(result, methodParams));
                }
            }
        }
        catch (Exception ex)
        {
            failure = ex;
        }

        sw.Stop();
        decimal elapsed = (decimal)sw.Elapsed.TotalSeconds;

        if (failure is null)
        {
            if (!messageBus.QueueMessage(new TestPassed
            {
                AssemblyUniqueID = assemblyID,
                TestCollectionUniqueID = collectionID,
                TestClassUniqueID = classID,
                TestMethodUniqueID = methodID,
                TestCaseUniqueID = caseID,
                TestUniqueID = testID,
                ExecutionTime = elapsed,
                FinishTime = DateTimeOffset.UtcNow,
                Output = string.Empty,
                Warnings = null,
            }))
            {
                cancellationTokenSource.Cancel();
            }
        }
        else
        {
            summary.Failed = 1;
            if (!messageBus.QueueMessage(TestFailed.FromException(
                failure,
                assemblyID,
                collectionID,
                classID,
                methodID,
                caseID,
                testID,
                elapsed,
                output: string.Empty,
                warnings: null)))
            {
                cancellationTokenSource.Cancel();
            }
        }

        if (!messageBus.QueueMessage(new TestFinished
        {
            AssemblyUniqueID = assemblyID,
            TestCollectionUniqueID = collectionID,
            TestClassUniqueID = classID,
            TestMethodUniqueID = methodID,
            TestCaseUniqueID = caseID,
            TestUniqueID = testID,
            ExecutionTime = elapsed,
            FinishTime = DateTimeOffset.UtcNow,
            Output = string.Empty,
            Warnings = null,
            Attachments = new Dictionary<string, TestAttachment>(),
        }))
        {
            cancellationTokenSource.Cancel();
        }

        return summary;
    }

    // xUnit v3 passes Func<ITestOutputHelper> factory delegates in constructorArguments.
    // Substitute the pre-initialized TestOutputHelper we created for this test execution.
    private static object?[] ResolveConstructorArguments(object?[] args, ITestOutputHelper outputHelper)
    {
        object?[] resolved = new object?[args.Length];
        for (int i = 0; i < args.Length; i++)
        {
            resolved[i] = args[i] is Func<ITestOutputHelper> ? outputHelper : args[i];
        }
        return resolved;
    }

}