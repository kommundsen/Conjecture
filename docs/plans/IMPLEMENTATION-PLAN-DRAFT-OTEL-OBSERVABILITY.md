# Draft: OpenTelemetry Test Observability

## Motivation

Conjecture already has structured logging via `ILogger` and `[LoggerMessage]` (Phase 6). The next step is emitting OpenTelemetry traces and metrics — providing rich, queryable telemetry for property test runs in CI/CD pipelines. Teams can visualize generation throughput, shrink duration, failure rates, and targeting progress in dashboards they already use (Grafana, Jaeger, Azure Monitor).

## .NET Advantage

.NET 10 adds telemetry schema URL support for `ActivitySource` and `Meter`, aligning with OpenTelemetry specifications. The `System.Diagnostics` APIs are in-box and zero-dependency — no OpenTelemetry SDK required to emit traces (collectors pick them up via the standard `DiagnosticSource` protocol). Conjecture can emit rich telemetry without adding any package dependencies to `Conjecture.Core`.

## Key Ideas

### Traces (via `ActivitySource`)
```
PropertyTest "addition is commutative"          [2.3s]
├── Generation                                   [1.8s]
│   ├── Example 1                                [12ms]  status=pass
│   ├── Example 2                                [11ms]  status=pass
│   ├── ...
│   └── Example 47                               [15ms]  status=fail  seed=0xABCD
├── Shrinking                                    [0.4s]
│   ├── ZeroBlockPass                            [50ms]  reductions=3
│   ├── DeleteBlockPass                          [80ms]  reductions=1
│   ├── LexicographicMinimizePass                [120ms] reductions=5
│   └── ...
├── Targeting                                    [0.1s]
│   └── "list-length" best=42.0
└── Result                                       status=fail  shrunk_seed=0x1234
```

### Metrics (via `Meter`)
- `conjecture.property.examples_total` (Counter) — total examples generated
- `conjecture.property.failures_total` (Counter) — total failures found
- `conjecture.property.duration_seconds` (Histogram) — per-property duration
- `conjecture.shrink.passes_total` (Counter, tagged by pass name) — shrink pass invocations
- `conjecture.shrink.reductions_total` (Counter) — successful shrink reductions
- `conjecture.generation.rejections_total` (Counter) — `Assume.That()` rejections
- `conjecture.targeting.best_score` (Gauge, tagged by label) — current best targeting score

### Zero-Dependency Design
- Use `System.Diagnostics.ActivitySource` and `System.Diagnostics.Metrics.Meter` (both in-box)
- No OpenTelemetry SDK dependency in `Conjecture.Core`
- Telemetry is inert unless a listener/collector is registered (zero overhead when not observed)
- Users wire up collection in their test host or CI pipeline

### CI/CD Integration
```yaml
# GitHub Actions example
- name: Run property tests with tracing
  env:
    OTEL_EXPORTER_OTLP_ENDPOINT: "http://otel-collector:4317"
  run: dotnet test --filter "Category=Property"
```

### Configuration
```csharp
new ConjectureSettings
{
    EnableTracing = true,    // emit Activity spans
    EnableMetrics = true,    // emit Meter measurements
}
```

## Design Decisions to Make

1. Granularity: emit a span per property, per example, or per shrink pass? (Per-example may be too noisy for 1000+ examples)
2. Should tracing be opt-in via settings or always-on with zero overhead when unobserved?
3. Telemetry schema URL: define a Conjecture-specific schema or use a generic test schema?
4. How to correlate traces with test framework output? (xUnit test case ID as span attribute?)
5. Should metrics use the same `ILogger` wiring or require separate configuration?
6. Naming conventions: `conjecture.*` namespace or align with an existing testing telemetry convention?

## Scope Estimate

Small-Medium. `ActivitySource` and `Meter` integration is straightforward. ~1-2 cycles.

## Dependencies

- `System.Diagnostics.DiagnosticSource` (in-box, .NET 10)
- `System.Diagnostics.Metrics` (in-box, .NET 10)
- Existing `TestRunner`, `Shrinker`, and `HillClimber` for instrumentation points
- Existing `ILogger` integration (Phase 6) — complements, does not replace

## Open Questions

- What telemetry do teams actually want from property tests in CI? (Survey needed)
- Should we provide a built-in console exporter for local development, or rely on third-party collectors?
- How to handle trace volume in CI? (1000 examples × 10 shrink passes = 10K+ spans per property)
- Should the Aspire integration (separate draft) automatically wire up OTel collection?
- Is there value in emitting traces for the example database (replay hits/misses)?
