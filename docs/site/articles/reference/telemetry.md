# Telemetry reference

Conjecture emits OpenTelemetry traces and metrics via the BCL `System.Diagnostics` APIs. Both are always available; they produce zero overhead when no listener is registered.

See [How to configure OpenTelemetry observability](../how-to/configure-otel.md) for wiring instructions.

## ActivitySource

**Name:** `"Conjecture.Core"`
**Version:** assembly informational version

Subscribe with `source.Name == "Conjecture.Core"` in your `ActivityListener.ShouldListenTo` predicate.

### Spans

#### `PropertyTest`

Root span — one per property test run.

| Tag | Type | Description |
|---|---|---|
| `conjecture.seed` | `string` | Hex seed used for generation (e.g. `"0x00000000DEADBEEF"`), or `"random"` when no seed was pinned |
| `conjecture.max_examples` | `int` | Value of `ConjectureSettings.MaxExamples` |
| `test.name` | `string` | Test method name. Set by framework adapters; `null` when not configured |
| `test.class.name` | `string` | Test class name. Set by framework adapters; `null` when not configured |
| `test.status` | `string` | `"pass"` or `"fail"` — set immediately before the span is stopped |

#### `PropertyTest.Generation`

Child of `PropertyTest`. Wraps the generation loop.

| Tag | Type | Description |
|---|---|---|
| `examples` | `int` | Number of valid examples generated |
| `failures` | `int` | Number of failures found during generation (`0` or `1`) |
| `rejections` | `int` | Number of examples rejected by `Assume.That()` |

#### `PropertyTest.Shrinking`

Child of `PropertyTest`. Only emitted when a failure was found and shrinking runs.

| Tag | Type | Description |
|---|---|---|
| `reductions` | `int` | Number of successful shrink steps |

#### `PropertyTest.Targeting`

Child of `PropertyTest`. Only emitted when `ConjectureSettings.Targeting = true` and at least one label was observed during generation.

| Tag | Type | Description |
|---|---|---|
| `labels` | `string` | Comma-separated list of targeting label names |
| `best_score` | `double` | Highest score achieved across all labels |

---

## Meter

**Name:** `"Conjecture.Core"`
**Version:** assembly informational version

Subscribe with `instrument.Meter.Name == "Conjecture.Core"` in your `MeterListener.InstrumentPublished` callback.

### Metrics

#### `conjecture.property.examples_total`

**Type:** Counter (`long`)
**Unit:** `{examples}`

Total number of valid examples generated across all property test runs.

#### `conjecture.property.failures_total`

**Type:** Counter (`long`)
**Unit:** `{failures}`

Total number of failures found. One failure equals one failing property run, regardless of how many examples were generated.

#### `conjecture.property.duration_seconds`

**Type:** Histogram (`double`)
**Unit:** `s`

End-to-end duration of each property test run, in seconds. Recorded once per run, after generation (and targeting if applicable) completes.

#### `conjecture.generation.rejections_total`

**Type:** Counter (`long`)
**Unit:** `{rejections}`

Total number of examples rejected by `Assume.That()`. High values relative to `examples_total` indicate an over-constrained strategy.

#### `conjecture.shrink.passes_total`

**Type:** Counter (`long`)
**Unit:** `{passes}`

Number of shrink pass invocations. Tagged by pass name (e.g. `"ZeroBlocks"`, `"DeleteBlocks"`, `"LexMin"`). Only incremented when shrinking runs.

#### `conjecture.shrink.reductions_total`

**Type:** Counter (`long`)
**Unit:** `{reductions}`

Total number of successful shrink reductions across all passes.

#### `conjecture.targeting.best_score`

**Type:** Histogram (`double`)

Final best score per targeting label at the end of each targeting phase. Tagged by label name. Only recorded when targeting runs.

#### `conjecture.database.replays_total`

**Type:** Counter (`long`)
**Unit:** `{replays}`

Number of stored failing examples replayed from the example database at the start of a run.

#### `conjecture.database.saves_total`

**Type:** Counter (`long`)
**Unit:** `{saves}`

Number of shrunk counterexamples saved to the example database.

---

## Schema URL

The authoritative machine-readable schema is at `docs/telemetry-schema.json` in the repository. The schema URL is embedded as a tag on the `Meter` (`conjecture.schema.url`) for tooling integrations.
