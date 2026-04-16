// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

using Conjecture.Core;
using Conjecture.Core.Internal;

namespace Conjecture.Core.Tests;

public sealed class MetricsInstrumentationTests
{
    [Fact]
    public async Task PassingProperty_ExamplesTotal_IsGreaterThanZero()
    {
        ConcurrentDictionary<string, long> measurements = new();
        using MeterListener listener = new();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == "Conjecture.Core")
            {
                l.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, measurement, _, _) =>
        {
            measurements.AddOrUpdate(instrument.Name, static (_, delta) => delta, static (_, existing, delta) => existing + delta, measurement);
        });
        listener.Start();

        ConjectureSettings settings = new() { MaxExamples = 10, Seed = 1UL };
        await TestRunner.Run(settings, data =>
        {
            int x = Generate.Integers<int>(0, 100).Generate(data);
            if (x < 0) { throw new Exception("impossible"); }
        });

        Assert.True(measurements.GetValueOrDefault("conjecture.property.examples_total") > 0);
    }

    [Fact]
    public async Task PassingProperty_FailuresTotal_IsZero()
    {
        ConcurrentDictionary<string, long> measurements = new();
        using MeterListener listener = new();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == "Conjecture.Core")
            {
                l.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, measurement, _, _) =>
        {
            measurements.AddOrUpdate(instrument.Name, static (_, delta) => delta, static (_, existing, delta) => existing + delta, measurement);
        });
        listener.Start();

        ConjectureSettings settings = new() { MaxExamples = 10, Seed = 1UL };
        await TestRunner.Run(settings, data =>
        {
            int x = Generate.Integers<int>(0, 100).Generate(data);
            if (x < 0) { throw new Exception("impossible"); }
        });

        Assert.True(measurements.ContainsKey("conjecture.property.examples_total"), "examples_total instrument must be recorded");
        Assert.Equal(0L, measurements.GetValueOrDefault("conjecture.property.failures_total"));
    }

    [Fact]
    public async Task FailingProperty_FailuresTotal_IsOne()
    {
        ConcurrentDictionary<string, long> measurements = new();
        using MeterListener listener = new();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == "Conjecture.Core")
            {
                l.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, measurement, _, _) =>
        {
            measurements.AddOrUpdate(instrument.Name, static (_, delta) => delta, static (_, existing, delta) => existing + delta, measurement);
        });
        listener.Start();

        ConjectureSettings settings = new() { MaxExamples = 100, Seed = 1UL };
        await TestRunner.Run(settings, data =>
        {
            int x = Generate.Integers<int>(0, 100).Generate(data);
            if (x > 5) { throw new Exception("too large"); }
        });

        Assert.Equal(1L, measurements.GetValueOrDefault("conjecture.property.failures_total"));
    }

    [Fact]
    public async Task FailingProperty_ExamplesTotal_IsGreaterThanZero()
    {
        ConcurrentDictionary<string, long> measurements = new();
        using MeterListener listener = new();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == "Conjecture.Core")
            {
                l.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, measurement, _, _) =>
        {
            measurements.AddOrUpdate(instrument.Name, static (_, delta) => delta, static (_, existing, delta) => existing + delta, measurement);
        });
        listener.Start();

        ConjectureSettings settings = new() { MaxExamples = 100, Seed = 1UL };
        await TestRunner.Run(settings, data =>
        {
            int x = Generate.Integers<int>(0, 100).Generate(data);
            if (x > 5) { throw new Exception("too large"); }
        });

        Assert.True(measurements.GetValueOrDefault("conjecture.property.examples_total") > 0);
    }

    [Fact]
    public async Task FailingProperty_ShrinkReductionsTotal_IsNonNegative()
    {
        ConcurrentDictionary<string, long> measurements = new();
        using MeterListener listener = new();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == "Conjecture.Core")
            {
                l.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, measurement, _, _) =>
        {
            measurements.AddOrUpdate(instrument.Name, static (_, delta) => delta, static (_, existing, delta) => existing + delta, measurement);
        });
        listener.Start();

        ConjectureSettings settings = new() { MaxExamples = 100, Seed = 1UL };
        await TestRunner.Run(settings, data =>
        {
            int x = Generate.Integers<int>(0, 100).Generate(data);
            if (x > 5) { throw new Exception("too large"); }
        });

        Assert.True(measurements.ContainsKey("conjecture.shrink.reductions_total"), "shrink.reductions_total instrument must be recorded");
        Assert.True(measurements.GetValueOrDefault("conjecture.shrink.reductions_total") >= 0);
    }

    [Fact]
    public async Task PropertyWithAssumptions_GenerationRejectionsTotal_IsGreaterThanZero()
    {
        ConcurrentDictionary<string, long> measurements = new();
        using MeterListener listener = new();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == "Conjecture.Core")
            {
                l.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, measurement, _, _) =>
        {
            measurements.AddOrUpdate(instrument.Name, static (_, delta) => delta, static (_, existing, delta) => existing + delta, measurement);
        });
        listener.Start();

        // Use a seed that ensures some odd numbers are generated so Assume.That rejects them
        ConjectureSettings settings = new() { MaxExamples = 5, Seed = 1UL };
        await TestRunner.Run(settings, data =>
        {
            int x = Generate.Integers<int>(0, 10).Generate(data);
            Assume.That(x % 2 == 0); // odd values are rejected
        });

        Assert.True(measurements.GetValueOrDefault("conjecture.generation.rejections_total") > 0);
    }

    [Fact]
    public async Task PassingProperty_DurationSeconds_IsPositive()
    {
        ConcurrentDictionary<string, double> doubleMeasurements = new();
        using MeterListener listener = new();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == "Conjecture.Core")
            {
                l.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<double>((instrument, measurement, _, _) =>
        {
            doubleMeasurements.AddOrUpdate(instrument.Name, static (_, delta) => delta, static (_, existing, delta) => existing + delta, measurement);
        });
        listener.Start();

        ConjectureSettings settings = new() { MaxExamples = 10, Seed = 1UL };
        await TestRunner.Run(settings, data =>
        {
            int x = Generate.Integers<int>(0, 100).Generate(data);
            if (x < 0) { throw new Exception("impossible"); }
        });

        Assert.True(doubleMeasurements.GetValueOrDefault("conjecture.property.duration_seconds") > 0.0);
    }
}