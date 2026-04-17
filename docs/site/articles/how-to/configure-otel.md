# How to configure OpenTelemetry observability

Conjecture emits trace spans and metrics via `System.Diagnostics.ActivitySource` and `System.Diagnostics.Metrics.Meter`. No OTel SDK is required to receive them — any compatible listener or collector works.

> [!NOTE]
> Telemetry is inert until a listener is registered. There is zero overhead when no listener is attached.

## Using the BCL listener API (zero dependencies)

Register listeners directly in your test project — no NuGet packages required.

### Traces

```csharp
using System.Diagnostics;

ActivityListener listener = new()
{
    ShouldListenTo = source => source.Name == "Conjecture.Core",
    Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
    ActivityStarted = activity => Console.WriteLine($"[START] {activity.OperationName}"),
    ActivityStopped = activity =>
        Console.WriteLine($"[STOP]  {activity.OperationName} status={activity.GetTagItem("test.status")}"),
};
ActivitySource.AddActivityListener(listener);
```

### Metrics

```csharp
using System.Diagnostics.Metrics;

MeterListener meterListener = new();
meterListener.InstrumentPublished = (instrument, listener) =>
{
    if (instrument.Meter.Name == "Conjecture.Core")
    {
        listener.EnableMeasurementEvents(instrument);
    }
};
meterListener.SetMeasurementEventCallback<long>((instrument, value, _, _) =>
    Console.WriteLine($"{instrument.Name}: {value}"));
meterListener.SetMeasurementEventCallback<double>((instrument, value, _, _) =>
    Console.WriteLine($"{instrument.Name}: {value:F3}"));
meterListener.Start();
```

Dispose both listeners when done — typically in a `[CollectionDefinition]` fixture or `IDisposable` teardown.

> [!TIP]
> For tests that assert on specific metric values, use `[Collection("Sequential")]` and `parallelizeTestCollections: false` in `xunit.runner.json` to prevent cross-test listener bleed. See [configure-logging](configure-logging.md) for the pattern.

## Using .NET Aspire

Aspire's `IDistributedApplicationBuilder` auto-discovers `ActivitySource` and `Meter` registrations. In your `AppHost`:

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddSource("Conjecture.Core"))
    .WithMetrics(metrics => metrics.AddMeter("Conjecture.Core"));
```

Conjecture spans then appear in the Aspire dashboard alongside your service traces.

## Using the OpenTelemetry SDK

Install the OTel .NET SDK in your test project:

```bash
dotnet add package OpenTelemetry.Extensions.Hosting
dotnet add package OpenTelemetry.Exporter.OpenTelemetryProtocol
```

Then configure in your test setup:

```csharp
using OpenTelemetry;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;

TracerProvider tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddSource("Conjecture.Core")
    .AddOtlpExporter()
    .Build();

MeterProvider meterProvider = Sdk.CreateMeterProviderBuilder()
    .AddMeter("Conjecture.Core")
    .AddOtlpExporter()
    .Build();
```

## Exporting to an OTLP collector in CI

Set the standard OTel environment variables before running tests:

```bash
OTEL_EXPORTER_OTLP_ENDPOINT=http://otel-collector:4317 dotnet test
```

GitHub Actions example:

```yaml
- name: Run property tests
  env:
    OTEL_EXPORTER_OTLP_ENDPOINT: ${{ vars.OTEL_COLLECTOR_ENDPOINT }}
  run: dotnet test --filter "Category=Property"
```

## Populating test identity tags

The `test.name` and `test.class.name` tags on the root `PropertyTest` span are populated automatically by the xUnit, NUnit, MSTest, and MTP adapters. When running `TestRunner` directly, set them via `ConjectureSettings`:

```csharp
ConjectureSettings settings = new()
{
    TestName = "My_property_description",
    TestClassName = "MyTestClass",
};
```

See the [telemetry reference](../reference/telemetry.md) for the full span and metric catalog.
