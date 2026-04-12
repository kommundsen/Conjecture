// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core;

using Xunit.Sdk;
using Xunit.v3;

namespace Conjecture.Xunit.V3.Internal;

internal sealed class PropertyTestCaseDiscoverer : IXunitTestCaseDiscoverer
{
    public ValueTask<IReadOnlyCollection<IXunitTestCase>> Discover(
        ITestFrameworkDiscoveryOptions discoveryOptions,
        IXunitTestMethod testMethod,
        IFactAttribute factAttribute)
    {
        IPropertyTest propTest = (IPropertyTest)factAttribute;
        PropertyAttribute attr = (PropertyAttribute)factAttribute;

        int maxExamples = propTest.MaxExamples > 0 ? propTest.MaxExamples : 100;
        ulong? seed = propTest.Seed != 0UL ? propTest.Seed : null;
        bool useDatabase = propTest.UseDatabase;
        int maxStrategyRejections = propTest.MaxStrategyRejections > 0 ? propTest.MaxStrategyRejections : 5;
        int deadlineMs = propTest.DeadlineMs;
        bool targeting = propTest.Targeting;
        double targetingProportion = propTest.TargetingProportion;

        string displayName = testMethod.GetDisplayName(attr.DisplayName ?? testMethod.Method.Name, null, null, null);
        string uniqueID = testMethod.UniqueID;

        IReadOnlyCollection<IXunitTestCase> testCases = [new PropertyTestCase(
            testMethod,
            displayName,
            uniqueID,
            attr.Explicit,
            attr.SkipExceptions,
            attr.Skip,
            attr.SkipType,
            attr.SkipUnless,
            attr.SkipWhen,
            null,
            null,
            null,
            null,
            maxExamples,
            seed,
            useDatabase,
            maxStrategyRejections,
            deadlineMs,
            targeting,
            targetingProportion)];

        return ValueTask.FromResult(testCases);
    }
}