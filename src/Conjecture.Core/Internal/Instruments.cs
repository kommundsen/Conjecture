// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Diagnostics.Metrics;

namespace Conjecture.Core.Internal;

internal static class Instruments
{
    internal static readonly Counter<long> ExamplesTotal =
        ConjectureObservability.Meter.CreateCounter<long>(
            "conjecture.property.examples_total",
            unit: "examples");

    internal static readonly Counter<long> FailuresTotal =
        ConjectureObservability.Meter.CreateCounter<long>(
            "conjecture.property.failures_total",
            unit: "failures");

    internal static readonly Histogram<double> DurationSeconds =
        ConjectureObservability.Meter.CreateHistogram<double>(
            "conjecture.property.duration_seconds",
            unit: "s");

    internal static readonly Counter<long> RejectionsTotal =
        ConjectureObservability.Meter.CreateCounter<long>(
            "conjecture.generation.rejections_total",
            unit: "rejections");

    internal static readonly Counter<long> ShrinkPassesTotal =
        ConjectureObservability.Meter.CreateCounter<long>(
            "conjecture.shrink.passes_total",
            unit: "passes");

    internal static readonly Counter<long> ShrinkReductionsTotal =
        ConjectureObservability.Meter.CreateCounter<long>(
            "conjecture.shrink.reductions_total",
            unit: "reductions");

    internal static readonly Histogram<double> TargetingBestScore =
        ConjectureObservability.Meter.CreateHistogram<double>(
            "conjecture.targeting.best_score");

    internal static readonly Counter<long> DatabaseReplaysTotal =
        ConjectureObservability.Meter.CreateCounter<long>(
            "conjecture.database.replays_total",
            unit: "replays");

    internal static readonly Counter<long> DatabaseSavesTotal =
        ConjectureObservability.Meter.CreateCounter<long>(
            "conjecture.database.saves_total",
            unit: "saves");
}