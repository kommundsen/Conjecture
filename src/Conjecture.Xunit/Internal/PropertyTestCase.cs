using Xunit.Abstractions;
using Xunit.Sdk;

namespace Conjecture.Xunit.Internal;

internal sealed class PropertyTestCase : XunitTestCase
{
    internal int MaxExamples { get; private set; }
    internal ulong? Seed { get; private set; }

    [Obsolete("For deserialization only", error: false)]
    public PropertyTestCase() { }

    internal PropertyTestCase(
        IMessageSink diagnosticMessageSink,
        TestMethodDisplay defaultMethodDisplay,
        TestMethodDisplayOptions defaultMethodDisplayOptions,
        ITestMethod testMethod,
        int maxExamples,
        ulong? seed)
        : base(diagnosticMessageSink, defaultMethodDisplay, defaultMethodDisplayOptions, testMethod)
    {
        MaxExamples = maxExamples;
        Seed = seed;
    }

    public override void Serialize(IXunitSerializationInfo info)
    {
        base.Serialize(info);
        info.AddValue("MaxExamples", MaxExamples);
        info.AddValue("Seed", Seed.HasValue ? Seed.Value.ToString() : null);
    }

    public override void Deserialize(IXunitSerializationInfo info)
    {
        base.Deserialize(info);
        MaxExamples = info.GetValue<int>("MaxExamples");
        var seedStr = info.GetValue<string?>("Seed");
        Seed = seedStr is not null ? ulong.Parse(seedStr) : null;
    }

    public override Task<RunSummary> RunAsync(
        IMessageSink diagnosticMessageSink,
        IMessageBus messageBus,
        object[] constructorArguments,
        ExceptionAggregator aggregator,
        CancellationTokenSource cancellationTokenSource)
        => new PropertyTestCaseRunner(
            this, DisplayName, SkipReason,
            constructorArguments, messageBus, aggregator, cancellationTokenSource)
        .RunAsync();
}
