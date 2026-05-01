// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using Conjecture.Core;
using Conjecture.Core.Internal;

using Microsoft.Testing.Extensions.TrxReport.Abstractions;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.Requests;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.TestHost;

namespace Conjecture.TestingPlatform.Internal;

internal sealed class PropertyTestFramework : ITestFramework, IDataProducer
{
    private readonly IEnumerable<Assembly> assemblies;
    private readonly Func<Type, bool>? typePredicate;
    private readonly ICommandLineOptions? commandLineOptions;
    private readonly PropertyTestFrameworkCapabilities? capabilities;

    internal PropertyTestFramework(IServiceProvider serviceProvider)
        : this(serviceProvider, AppDomain.CurrentDomain.GetAssemblies(), null,
               serviceProvider.GetCommandLineOptions())
    {
    }

    internal PropertyTestFramework(
        IServiceProvider serviceProvider,
        IEnumerable<Assembly> assemblies,
        Func<Type, bool>? typePredicate = null,
        ICommandLineOptions? commandLineOptions = null,
        PropertyTestFrameworkCapabilities? capabilities = null)
    {
        _ = serviceProvider;
        this.assemblies = assemblies;
        this.typePredicate = typePredicate;
        this.commandLineOptions = commandLineOptions;
        this.capabilities = capabilities;
    }

    public string Uid => "Conjecture.TestingPlatform";
    public string Version => typeof(PropertyTestFramework).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "1.0.0";
    public string DisplayName => "Conjecture Property Testing";
    public string Description => "Property-based testing for .NET via Microsoft.Testing.Platform";

    public Type[] DataTypesProduced => [typeof(TestNodeUpdateMessage)];

    public Task<bool> IsEnabledAsync()
    {
        return Task.FromResult(true);
    }

    public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context)
    {
        return Task.FromResult(new CreateTestSessionResult { IsSuccess = true });
    }

    public Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context)
    {
        return Task.FromResult(new CloseTestSessionResult { IsSuccess = true });
    }

    [RequiresUnreferencedCode("Discovers property methods via reflection; not trim-safe.")]
    [RequiresDynamicCode("Resolves parameter strategies via MakeGenericMethod; not NativeAOT-safe.")]
    public async Task ExecuteRequestAsync(ExecuteRequestContext context)
    {
        try
        {
            if (context.Request is DiscoverTestExecutionRequest discoverRequest)
            {
                await DiscoverAsync(context.MessageBus, discoverRequest.Session.SessionUid);
            }
            else if (context.Request is RunTestExecutionRequest runRequest)
            {
                await RunAsync(context.MessageBus, runRequest.Session.SessionUid);
            }
        }
        finally
        {
            context.Complete();
        }
    }

    [RequiresUnreferencedCode("Discovers property methods via reflection; not trim-safe.")]
    internal async Task DiscoverAsync(IMessageBus bus, SessionUid sessionUid)
    {
        foreach (MethodInfo method in EnumeratePropertyMethods())
        {
            string uid = TestCaseHelper.ComputeTestId(method);
            string displayName = $"{method.DeclaringType!.Name}.{method.Name}";
            TestNode node = new()
            {
                Uid = new(uid),
                DisplayName = displayName,
            };
            node.Properties.Add(DiscoveredTestNodeStateProperty.CachedInstance);
            await bus.PublishAsync(this, new TestNodeUpdateMessage(sessionUid, node));
        }
    }

    [RequiresUnreferencedCode("Discovers and runs property methods via reflection; not trim-safe.")]
    [RequiresDynamicCode("Resolves parameter strategies via MakeGenericMethod; not NativeAOT-safe.")]
    internal async Task RunAsync(IMessageBus bus, SessionUid sessionUid)
    {
        foreach (MethodInfo method in EnumeratePropertyMethods())
        {
            await RunMethodAsync(bus, sessionUid, method);
        }
    }

    [RequiresUnreferencedCode("Runs property method via reflection; not trim-safe.")]
    [RequiresDynamicCode("Resolves parameter strategies via MakeGenericMethod; not NativeAOT-safe.")]
    private async Task RunMethodAsync(IMessageBus bus, SessionUid sessionUid, MethodInfo method)
    {
        PropertyAttribute attr = method.GetCustomAttribute<PropertyAttribute>()!;
        string uid = TestCaseHelper.ComputeTestId(method);
        string displayName = $"{method.DeclaringType!.Name}.{method.Name}";
        TestNodeUid parentNodeUid = new(uid);
        ParameterInfo[] parameters = method.GetParameters();
        bool sampleFailed = false;
        object? instance = method.IsStatic ? null : Activator.CreateInstance(method.DeclaringType!);

        SampleAttribute[] samples = method.GetCustomAttributes<SampleAttribute>().ToArray();
        for (int i = 0; i < samples.Length; i++)
        {
            SampleAttribute sample = samples[i];
            string childUid = uid + ":sample:" + i;
            string childDisplayName = $"{displayName}[Sample {i}]";
            TestNode childNode = new()
            {
                Uid = new(childUid),
                DisplayName = childDisplayName,
            };

            try
            {
                TestCaseHelper.ValidateSampleArgs(sample, parameters);
                if (TestCaseHelper.IsAsyncReturnType(method.ReturnType))
                {
                    await TestCaseHelper.InvokeAsync(method, instance, sample.Arguments!);
                }
                else
                {
                    TestCaseHelper.InvokeSync(method, instance, sample.Arguments!);
                }

                childNode.Properties.Add(PassedTestNodeStateProperty.CachedInstance);
                await bus.PublishAsync(this, new TestNodeUpdateMessage(sessionUid, childNode, parentNodeUid));
            }
            catch (Exception ex)
            {
                string msg = TestCaseHelper.BuildSampleFailureMessage(sample, parameters, ex);
                childNode.Properties.Add(new FailedTestNodeStateProperty(ex, msg));
                if (capabilities?.TrxEnabled == true)
                {
                    childNode.Properties.Add(new TrxExceptionProperty(msg, ex.StackTrace));
                }

                await bus.PublishAsync(this, new TestNodeUpdateMessage(sessionUid, childNode, parentNodeUid));

                TestNode parentNode = new()
                {
                    Uid = parentNodeUid,
                    DisplayName = displayName,
                };
                parentNode.Properties.Add(new FailedTestNodeStateProperty(ex, msg));
                if (capabilities?.TrxEnabled == true)
                {
                    parentNode.Properties.Add(new TrxExceptionProperty(msg, ex.StackTrace));
                }

                await bus.PublishAsync(this, new TestNodeUpdateMessage(sessionUid, parentNode));
                sampleFailed = true;
                break;
            }
        }

        if (sampleFailed)
        {
            return;
        }

        if (parameters.Length == 0)
        {
            TestNode node = new() { Uid = parentNodeUid, DisplayName = displayName };
            try
            {
                if (TestCaseHelper.IsAsyncReturnType(method.ReturnType))
                {
                    await TestCaseHelper.InvokeAsync(method, instance, []);
                }
                else
                {
                    TestCaseHelper.InvokeSync(method, instance, []);
                }

                node.Properties.Add(PassedTestNodeStateProperty.CachedInstance);
            }
            catch (Exception ex)
            {
                node.Properties.Add(new FailedTestNodeStateProperty(ex, ex.Message));
                if (capabilities?.TrxEnabled == true)
                {
                    node.Properties.Add(new TrxExceptionProperty(ex.Message, ex.StackTrace));
                }
            }

            await bus.PublishAsync(this, new TestNodeUpdateMessage(sessionUid, node));
            return;
        }

        MtpLogger mtpLogger = new(this, bus, parentNodeUid, sessionUid);
        ConjectureSettings settings = ConjectureSettings.From(attr, mtpLogger);

        if (commandLineOptions is not null)
        {
            // MTP guarantees args is non-empty when TryGetOptionArgumentList returns true.
            if (commandLineOptions.TryGetOptionArgumentList(ConjectureCommandLineOptions.SeedOption, out string[]? seedArgs)
                && ulong.TryParse(seedArgs[0], out ulong seed))
            {
                settings = settings with { Seed = seed };
            }

            // MTP guarantees args is non-empty when TryGetOptionArgumentList returns true.
            if (commandLineOptions.TryGetOptionArgumentList(ConjectureCommandLineOptions.MaxExamplesOption, out string[]? maxArgs)
                && int.TryParse(maxArgs[0], out int max))
            {
                settings = settings with { MaxExamples = max };
            }
        }

        using ExampleDatabase db = new(Path.Combine(settings.DatabasePath, "conjecture.db"), settings.Logger);

        Task Test(ConjectureData data)
        {
            object[] args = SharedParameterStrategyResolver.Resolve(parameters, data);
            if (TestCaseHelper.IsAsyncReturnType(method.ReturnType))
            {
                return TestCaseHelper.InvokeAsync(method, instance, args);
            }

            TestCaseHelper.InvokeSync(method, instance, args);
            return Task.CompletedTask;
        }

        TestRunResult result = await TestRunner.RunAsync(settings, Test, db, uid);

        TestNode resultNode = new()
        {
            Uid = parentNodeUid,
            DisplayName = displayName,
        };

        if (result.Passed)
        {
            resultNode.Properties.Add(PassedTestNodeStateProperty.CachedInstance);
        }
        else
        {
            if (settings.ExportReproductionOnFailure && result.Counterexample is not null)
            {
                try
                {
                    ConjectureData replay = ConjectureData.ForRecord(result.Counterexample);
                    object[] values = SharedParameterStrategyResolver.Resolve(parameters, replay);
                    IEnumerable<(string Name, object? Value, Type Type)> reproParams = parameters.Zip(values,
                        static (p, v) => (p.Name!, (object?)v, p.ParameterType));
                    ReproContext context = new(
                        method.DeclaringType?.Name ?? "UnknownClass",
                        method.Name,
                        TestCaseHelper.IsAsyncReturnType(method.ReturnType),
                        reproParams,
                        result.Seed!.Value,
                        result.ExampleCount,
                        result.ShrinkCount,
                        Conjecture.Core.Internal.TestFramework.Xunit,
                        DateTimeOffset.UtcNow);
                    ReproFileBuilder.WriteToFile(context, settings.ReproductionOutputPath);
                }
                catch (Exception)
                {
                    // Repro export must not propagate; failure reporting happens below.
                }
            }

            string failureMessage;
            try
            {
                failureMessage = TestCaseHelper.BuildFailureMessage(result, parameters);
            }
            catch (Exception)
            {
                failureMessage = result.FailureStackTrace is not null
                    ? $"Property failed.{Environment.NewLine}{result.FailureStackTrace}"
                    : "Property failed.";
            }

            resultNode.Properties.Add(new FailedTestNodeStateProperty(failureMessage));
            if (capabilities?.TrxEnabled == true)
            {
                resultNode.Properties.Add(new TrxExceptionProperty(failureMessage, null));
            }
        }

        await bus.PublishAsync(this, new TestNodeUpdateMessage(sessionUid, resultNode));
    }

    [RequiresUnreferencedCode("Iterates assembly types via reflection; not trim-safe.")]
    private IEnumerable<MethodInfo> EnumeratePropertyMethods()
    {
        foreach (Assembly assembly in assemblies)
        {
            foreach (Type type in assembly.GetTypes())
            {
                if (typePredicate is not null && !typePredicate(type))
                {
                    continue;
                }

                foreach (MethodInfo method in type.GetMethods(
                    BindingFlags.Public | BindingFlags.NonPublic |
                    BindingFlags.Static | BindingFlags.Instance))
                {
                    if (method.GetCustomAttribute<PropertyAttribute>() is not null)
                    {
                        yield return method;
                    }
                }
            }
        }
    }
}