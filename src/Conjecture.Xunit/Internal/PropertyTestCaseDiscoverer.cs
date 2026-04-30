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
        AttributeInfoPropertyTestAdapter adapter = new(factAttribute);

        int maxExamples = adapter.MaxExamples > 0 ? adapter.MaxExamples : 100;
        ulong? seed = adapter.Seed != 0UL ? adapter.Seed : null;
        bool database = adapter.Database;
        int maxStrategyRejections = adapter.MaxStrategyRejections > 0 ? adapter.MaxStrategyRejections : 5;
        int deadlineMs = adapter.DeadlineMs;
        bool targeting = adapter.Targeting;
        double targetingProportion = adapter.TargetingProportion;
        bool exportReproOnFailure = adapter.ExportReproductionOnFailure;
        string reproOutputPath = adapter.ReproductionOutputPath;

        yield return new PropertyTestCase(
            diagnosticMessageSink,
            discoveryOptions.MethodDisplayOrDefault(),
            discoveryOptions.MethodDisplayOptionsOrDefault(),
            testMethod,
            maxExamples,
            seed,
            database,
            maxStrategyRejections,
            deadlineMs,
            targeting,
            targetingProportion,
            exportReproOnFailure,
            reproOutputPath);
    }
}