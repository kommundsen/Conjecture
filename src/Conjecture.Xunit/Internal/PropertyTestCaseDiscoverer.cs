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
        var maxExamples = factAttribute.GetNamedArgument<int>("MaxExamples");
        if (maxExamples <= 0) { maxExamples = 100; }

        var rawSeed = factAttribute.GetNamedArgument<ulong>("Seed");
        var seed = rawSeed == 0UL ? (ulong?)null : rawSeed;

        var useDatabase = factAttribute.GetNamedArgument<bool>("UseDatabase");
        var maxStrategyRejections = factAttribute.GetNamedArgument<int>("MaxStrategyRejections");
        if (maxStrategyRejections <= 0) { maxStrategyRejections = 5; }
        var deadlineMs = factAttribute.GetNamedArgument<int>("DeadlineMs");

        yield return new PropertyTestCase(
            diagnosticMessageSink,
            discoveryOptions.MethodDisplayOrDefault(),
            discoveryOptions.MethodDisplayOptionsOrDefault(),
            testMethod,
            maxExamples,
            seed,
            useDatabase,
            maxStrategyRejections,
            deadlineMs);
    }
}
