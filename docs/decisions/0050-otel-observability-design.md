# 0050. OTel Observability: ActivitySource and Meter for Distributed Tracing and Metrics

**Date:** 2026-04-16
**Status:** Accepted

## Context

ADR-0037 established `ILogger`/`[LoggerMessage]` as Conjecture's structured-logging layer and explicitly deferred `ActivitySource`/`Meter` as "designed for distributed tracing and metrics in services; not appropriate for single-process test runs."

Since then, two developments changed the calculus:

1. **Aspire adoption** — teams running Conjecture inside Aspire-orchestrated solutions want Conjecture test spans to appear alongside their service traces in the Aspire dashboard, without any bespoke adapter code.
2. **Aggregate metrics demand** — users want Prometheus/OTel-Collector–exportable metrics (e.g. shrink counts, assumption rejection rates) without parsing log output.

`ILogger` is the right vehicle for human-readable, per-run diagnostics. `ActivitySource`/`Meter` is the right vehicle for machine-consumable, cross-process observability. Both are needed; they are complementary, not alternatives.

## Decision

**Static singletons on `ConjectureObservability`**

Expose a single internal (later public) class `ConjectureObservability` in `Conjecture.Core` that owns:

- `static readonly ActivitySource ActivitySource` — name `"Conjecture"`, version from assembly
- `static readonly Meter Meter` — name `"Conjecture"`, version from assembly

Both are always instantiated; the OTel SDK's listener model means zero overhead is incurred when no `ActivityListener` or `MeterListener` is registered.

**Phase-level spans only**

A root `Activity` (`conjecture.test`) is started for each test run. Three child activities are started and stopped around each phase:

| Activity name | Phase |
|---|---|
| `conjecture.generation` | Generation loop |
| `conjecture.shrinking` | Shrink loop |
| `conjecture.targeting` | Targeting / hill-climbing |

No per-example or per-draw spans are emitted — these are tight inner loops where span overhead would meaningfully degrade performance (consistent with ADR-0037's "Hot Path Protection" principle).

**Standard test attributes on the root span**

| Attribute key | Source |
|---|---|
| `test.name` | Test method name |
| `test.class.name` | Test class name |
| `test.framework` | e.g. `"xunit"`, `"nunit"`, `"mstest"` |
| `conjecture.seed` | Reproducibility seed |
| `conjecture.max_examples` | Setting value |

**Metric naming and schema**

All metrics use the `conjecture.*` prefix. The canonical list, types, units, and attribute keys are documented in a versioned JSON schema at `docs/telemetry-schema.json`. The schema URL is embedded as a tag on the `Meter` (`conjecture.schema.url`).

Database metrics are in scope:

| Metric | Type | Unit |
|---|---|---|
| `conjecture.database.replays_total` | Counter | `{replays}` |
| `conjecture.database.saves_total` | Counter | `{saves}` |

**No built-in exporter; no library-side sampling**

Conjecture ships no OTLP exporter and configures no sampler. Export and sampling are the host application's responsibility. Aspire wiring is deferred to a separate issue.

## Consequences

- `System.Diagnostics.DiagnosticSource` (already in the BCL for .NET 8+) is the only new dependency — no NuGet package required for `ActivitySource`/`Meter`.
- `ConjectureObservability` is `internal` initially; it will be made `public` once the API is stable.
- `TestRunner` gains three `Activity`-bracketed sections per run; the added overhead is a handful of dictionary lookups gated on `ActivitySource.HasListeners()`.
- `ExampleDatabase` gains two counter increments per replay/save path.
- `docs/telemetry-schema.json` becomes the authoritative reference for third-party tooling integrations.

## Alternatives Considered

- **Per-example spans**: Rejected — inner loops run thousands of iterations; span creation cost would be significant and the data volume would overwhelm any collector.
- **EventSource / ETW**: Rejected — ETW is Windows-only; `ActivitySource` is the modern cross-platform standard and integrates directly with OTel.
- **Third-party OTel NuGet in Core**: Rejected — `System.Diagnostics.DiagnosticSource` is BCL-level; pulling `OpenTelemetry` into Core would impose a heavy transitive dependency on every Conjecture user.
- **Opt-in ActivitySource (created on demand)**: Rejected — always-on singletons are idiomatic for .NET libraries; the listener model already provides zero-overhead no-op behaviour when no listener is attached.
