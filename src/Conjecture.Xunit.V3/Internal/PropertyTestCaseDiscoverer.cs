// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

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
        PropertyAttribute attr = (PropertyAttribute)factAttribute;

        int maxExamples = attr.MaxExamples > 0 ? attr.MaxExamples : 100;
        ulong? seed = attr.Seed != 0UL ? attr.Seed : null;
        bool useDatabase = attr.UseDatabase;
        int maxStrategyRejections = attr.MaxStrategyRejections > 0 ? attr.MaxStrategyRejections : 5;
        int deadlineMs = attr.DeadlineMs;

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
            deadlineMs)];

        return ValueTask.FromResult(testCases);
    }
}