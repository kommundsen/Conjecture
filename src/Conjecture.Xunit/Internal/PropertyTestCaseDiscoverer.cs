using Xunit.Abstractions;
using Xunit.Sdk;

namespace Conjecture.Xunit.Internal;

internal sealed class PropertyTestCaseDiscoverer : IXunitTestCaseDiscoverer
{
    private readonly IMessageSink diagnosticMessageSink;

    public PropertyTestCaseDiscoverer(IMessageSink diagnosticMessageSink)
        => this.diagnosticMessageSink = diagnosticMessageSink;

    public IEnumerable<IXunitTestCase> Discover(
        ITestFrameworkDiscoveryOptions discoveryOptions,
        ITestMethod testMethod,
        IAttributeInfo factAttribute)
    {
        var maxExamples = factAttribute.GetNamedArgument<int>("MaxExamples");
        if (maxExamples <= 0) maxExamples = 100;

        var rawSeed = factAttribute.GetNamedArgument<ulong>("Seed");
        var seed = rawSeed == 0UL ? (ulong?)null : rawSeed;

        yield return new PropertyTestCase(
            diagnosticMessageSink,
            discoveryOptions.MethodDisplayOrDefault(),
            discoveryOptions.MethodDisplayOptionsOrDefault(),
            testMethod,
            maxExamples,
            seed);
    }
}
