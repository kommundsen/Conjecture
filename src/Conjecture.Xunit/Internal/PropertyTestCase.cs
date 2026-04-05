// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Xunit.Abstractions;
using Xunit.Sdk;

namespace Conjecture.Xunit.Internal;

internal sealed class PropertyTestCase : XunitTestCase
{
    internal int MaxExamples { get; private set; }
    internal ulong? Seed { get; private set; }
    internal bool UseDatabase { get; private set; }
    internal int MaxStrategyRejections { get; private set; }
    internal int DeadlineMs { get; private set; }
    internal bool Targeting { get; private set; } = true;
    internal double TargetingProportion { get; private set; } = 0.5;

    [Obsolete("For deserialization only", error: false)]
    public PropertyTestCase() { }

    internal PropertyTestCase(
        IMessageSink diagnosticMessageSink,
        TestMethodDisplay defaultMethodDisplay,
        TestMethodDisplayOptions defaultMethodDisplayOptions,
        ITestMethod testMethod,
        int maxExamples,
        ulong? seed,
        bool useDatabase,
        int maxStrategyRejections,
        int deadlineMs,
        bool targeting,
        double targetingProportion)
        : base(diagnosticMessageSink, defaultMethodDisplay, defaultMethodDisplayOptions, testMethod)
    {
        MaxExamples = maxExamples;
        Seed = seed;
        UseDatabase = useDatabase;
        MaxStrategyRejections = maxStrategyRejections;
        DeadlineMs = deadlineMs;
        Targeting = targeting;
        TargetingProportion = targetingProportion;
    }

    public override void Serialize(IXunitSerializationInfo info)
    {
        base.Serialize(info);
        info.AddValue("MaxExamples", MaxExamples);
        info.AddValue("Seed", Seed.HasValue ? Seed.Value.ToString() : null);
        info.AddValue("UseDatabase", UseDatabase);
        info.AddValue("MaxStrategyRejections", MaxStrategyRejections);
        info.AddValue("DeadlineMs", DeadlineMs);
        info.AddValue("Targeting", Targeting.ToString());
        info.AddValue("TargetingProportion", TargetingProportion.ToString("R"));
    }

    public override void Deserialize(IXunitSerializationInfo info)
    {
        base.Deserialize(info);
        MaxExamples = info.GetValue<int>("MaxExamples");
        string? seedStr = info.GetValue<string?>("Seed");
        Seed = seedStr is not null ? ulong.Parse(seedStr) : null;
        UseDatabase = info.GetValue<bool>("UseDatabase");
        MaxStrategyRejections = info.GetValue<int>("MaxStrategyRejections");
        DeadlineMs = info.GetValue<int>("DeadlineMs");
        string? targetingStr = info.GetValue<string?>("Targeting");
        Targeting = targetingStr is null ? true : bool.Parse(targetingStr);
        string? proportionStr = info.GetValue<string?>("TargetingProportion");
        if (proportionStr is not null
            && double.TryParse(proportionStr, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out double parsed)
            && parsed >= 0.0 && parsed < 1.0)
        {
            TargetingProportion = parsed;
        }
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