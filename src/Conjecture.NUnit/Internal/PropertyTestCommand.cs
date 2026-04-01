using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Conjecture.Core;
using Conjecture.Core.Internal;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;
using NUnit.Framework.Internal.Commands;

namespace Conjecture.NUnit.Internal;

internal sealed class PropertyTestCommand : DelegatingTestCommand
{
    private readonly int maxExamples;
    private readonly ulong? seed;
    private readonly bool useDatabase;
    private readonly int maxStrategyRejections;
    private readonly int deadlineMs;

    internal PropertyTestCommand(
        TestCommand innerCommand,
        int maxExamples,
        ulong? seed,
        bool useDatabase,
        int maxStrategyRejections,
        int deadlineMs)
        : base(innerCommand)
    {
        this.maxExamples = maxExamples;
        this.seed = seed;
        this.useDatabase = useDatabase;
        this.maxStrategyRejections = maxStrategyRejections;
        this.deadlineMs = deadlineMs;
    }

    [RequiresDynamicCode("Property test execution uses MakeGenericMethod for typed strategy dispatch.")]
    [RequiresUnreferencedCode("Property test execution accesses parameter type metadata that may be trimmed.")]
    public override TestResult Execute(TestExecutionContext context)
    {
        MethodInfo methodInfo = context.CurrentTest.Method!.MethodInfo;
        object? testInstance = context.TestObject;
        ParameterInfo[] methodParams = methodInfo.GetParameters();

        ExampleAttribute[] exampleAttrs = methodInfo
            .GetCustomAttributes(typeof(ExampleAttribute), inherit: false)
            .Cast<ExampleAttribute>()
            .ToArray();

        foreach (ExampleAttribute ea in exampleAttrs)
        {
            PropertyTestBuilder.ValidateExampleArgs(ea, methodParams);
        }

        ConjectureSettings settings = new()
        {
            MaxExamples = maxExamples,
            Seed = seed,
            UseDatabase = useDatabase,
            MaxStrategyRejections = maxStrategyRejections,
            Deadline = deadlineMs > 0 ? TimeSpan.FromMilliseconds(deadlineMs) : null,
        };

        string dbPath = Path.Combine(settings.DatabasePath, "conjecture.db");
        string testIdHash = PropertyTestBuilder.ComputeTestId(methodInfo);
        bool isAsync = IsAsyncReturnType(methodInfo.ReturnType);

        try
        {
            int explicitCount = 0;
            Exception? explicitFailure = null;

            foreach (ExampleAttribute ea in exampleAttrs)
            {
                try
                {
                    object? returnVal = methodInfo.Invoke(testInstance, ea.Arguments);
                    if (returnVal is Task t) { RunTask(t); }
                    else if (returnVal is ValueTask vt) { RunTask(vt.AsTask()); }
                    explicitCount++;
                }
                catch (TargetInvocationException ex) when (ex.InnerException is not null)
                {
                    explicitFailure = new Exception(
                        PropertyTestBuilder.BuildExampleFailureMessage(ea, methodParams, ex.InnerException),
                        ex.InnerException);
                    break;
                }
            }

            if (explicitFailure is not null)
            {
                context.CurrentResult.SetResult(ResultState.Failure, explicitFailure.Message);
                return context.CurrentResult;
            }

            using ExampleDatabase db = new(dbPath);
            Task<TestRunResult> runTask = isAsync
                ? TestRunner.RunAsync(settings, async data =>
                {
                    object[] args = SharedParameterStrategyResolver.Resolve(methodParams, data);
                    await InvokeAsync(methodInfo, testInstance, args);
                }, db, testIdHash)
                : TestRunner.Run(settings, data =>
                {
                    object[] args = SharedParameterStrategyResolver.Resolve(methodParams, data);
                    InvokeSync(methodInfo, testInstance, args);
                }, db, testIdHash);

            // NUnit constraint: DelegatingTestCommand.Execute() has no async override.
            // GetAwaiter().GetResult() is unavoidable here and safe on NUnit's thread-pool threads.
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
                    PropertyTestBuilder.BuildFailureMessage(result, methodParams));
            }
        }
        catch (Exception ex)
        {
            context.CurrentResult.RecordException(ex);
        }

        return context.CurrentResult;
    }

    private static bool IsAsyncReturnType(Type returnType)
    {
        return returnType == typeof(Task)
            || returnType == typeof(ValueTask)
            || (returnType.IsGenericType && (
                returnType.GetGenericTypeDefinition() == typeof(Task<>)
                || returnType.GetGenericTypeDefinition() == typeof(ValueTask<>)));
    }

    // NUnit constraint: Execute() is sync-only; no async alternative exists.
    private static void RunTask(Task task)
    {
        task.GetAwaiter().GetResult();
    }

    private static void InvokeSync(MethodInfo method, object? instance, object[] args)
    {
        try
        {
            method.Invoke(instance, args);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
        }
    }

    private static async Task InvokeAsync(MethodInfo method, object? instance, object[] args)
    {
        object? returnVal;
        try
        {
            returnVal = method.Invoke(instance, args);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            return;
        }

        if (returnVal is Task task) { await task; }
        else if (returnVal is ValueTask vt) { await vt; }
    }
}
