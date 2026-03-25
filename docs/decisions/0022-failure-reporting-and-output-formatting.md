# 0022. Failure Reporting and Output Formatting

**Date:** 2026-03-25
**Status:** Accepted

## Context

When Conjecture.NET finds and shrinks a counterexample, the output must be immediately actionable. Developers should be able to copy the reported values directly into a `[Fact]` regression test or reproduce the failure by pasting a seed attribute. C# has no universal pretty-printer for arbitrary types — `ToString()` defaults to the type name with no field values, which is useless for domain objects. The formatter must also be compatible with NativeAOT and trimming (ADR-0014), ruling out unconstrained reflection.

## Decision

Introduce `IStrategyFormatter<T>` and a central `FormatterRegistry`:

```csharp
public interface IStrategyFormatter<T>
{
    string Format(T value);
}

FormatterRegistry.Register<MyType>(new MyTypeFormatter());
```

Built-in strategies register default formatters that produce C#-like literal syntax:

| Type | Output |
|------|--------|
| `int` | `42` |
| `string` | `"hello"` |
| `List<int>` | `[3, -1, 7]` |
| `byte[]` | `new byte[] { 0x01, 0xFF }` |
| `record` | `new Person("Alice", 30)` |
| `(T, U)` | `(3, "x")` |

Standard failure output:

```
Falsifying example found after 47 examples (seed: 0xDEADBEEF):
  x = [3, -1, 0]
  y = "hello"
Shrunk 12 times from original.
Reproduce with: [Property(Seed = 0xDEADBEEF)]
```

Stateful test failures (ADR-0015) print the shrunk command sequence with step numbers and the final state.

The seed is always printed. The example database (ADR-0024) also stores the raw buffer for zero-configuration re-run on the next test run without specifying a seed.

`FormatterRegistry` uses a `Holder<T>` generic static pattern (consistent with ADR-0014) to remain trim-safe — no `Type`-keyed dictionaries, no reflection-based lookup.

## Consequences

- Failure output is copy-pasteable into regression tests without any manual reformatting
- Custom types require an explicit formatter registration, but the `[Arbitrary]` source generator (ADR-0010) can emit a default formatter alongside the strategy
- `FormatterRegistry` must be populated at startup; `[ModuleInitializer]` registrations from source-generated code handle this automatically
- Stateful test output is necessarily more verbose but still structured and shrunk to the minimal failing sequence
- If no formatter is registered for a type, the engine falls back to `ToString()` with a warning — output degrades gracefully rather than failing

## Alternatives Considered

- **Rely on `ToString()`** — the default for most domain types is the fully qualified type name with no field values; not actionable and discourages writing property tests for complex types
- **Reflection-based auto-formatter** — works for simple POCOs in a full-runtime environment but breaks under NativeAOT and `PublishTrimmed=true` (ADR-0014); also produces inconsistent output for types with private fields or custom equality
- **JSON serialisation** — not all types are JSON-serialisable; output uses JSON syntax rather than C# syntax, so values cannot be pasted directly into source code; adds a dependency on `System.Text.Json` or Newtonsoft
