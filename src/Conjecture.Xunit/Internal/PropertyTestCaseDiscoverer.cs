// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Xunit.Abstractions;
using Xunit.Sdk;

namespace Conjecture.Xunit.Internal;

internal sealed class PropertyTestCaseDiscoverer(IMessageSink diagnosticMessageSink) : IXunitTestCaseDiscoverer
{
    public IEnumerable<IXunitTestCase> Discover(
        ITestFrameworkDiscoveryOptions discoveryOptions,
        ITestMethod testMethod,
        IAttributeInfo factAttribute)
    {
        int maxExamples = factAttribute.GetNamedArgument<int>("MaxExamples");
        if (maxExamples <= 0) { maxExamples = 100; }

        ulong rawSeed = factAttribute.GetNamedArgument<ulong>("Seed");
        ulong? seed = rawSeed == 0UL ? null : rawSeed;

        bool useDatabase = factAttribute.GetNamedArgument<bool>("UseDatabase");
        int maxStrategyRejections = factAttribute.GetNamedArgument<int>("MaxStrategyRejections");
        if (maxStrategyRejections <= 0) { maxStrategyRejections = 5; }
        int deadlineMs = factAttribute.GetNamedArgument<int>("DeadlineMs");
        bool targeting = factAttribute.GetNamedArgument<bool>("Targeting");
        double targetingProportion = factAttribute.GetNamedArgument<double>("TargetingProportion");
        bool exportReproOnFailure = factAttribute.GetNamedArgument<bool>("ExportReproOnFailure");
        string reproOutputPath = factAttribute.GetNamedArgument<string>("ReproOutputPath") ?? ".conjecture/repros/";

        yield return new PropertyTestCase(
            diagnosticMessageSink,
            discoveryOptions.MethodDisplayOrDefault(),
            discoveryOptions.MethodDisplayOptionsOrDefault(),
            testMethod,
            maxExamples,
            seed,
            useDatabase,
            maxStrategyRejections,
            deadlineMs,
            targeting,
            targetingProportion,
            exportReproOnFailure,
            reproOutputPath);
    }
}