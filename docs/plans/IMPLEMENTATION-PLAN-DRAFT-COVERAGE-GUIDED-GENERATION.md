# Draft: Coverage-Guided Generation

## Motivation

Property-based testing and fuzzing attack the same problem — finding inputs that break invariants — from different angles. PBT uses structured generation with shrinking; fuzzing uses coverage feedback to guide mutation. Conjecture's byte-buffer engine is structurally similar to coverage-guided fuzzers (AFL, libFuzzer): both operate on a byte stream that drives input construction. Bridging these approaches means Conjecture can observe which code paths each example exercises and bias generation toward unexplored branches, systematically improving code coverage without users manually tuning strategies.

## .NET Advantage

.NET provides multiple instrumentation channels for coverage feedback:
- `System.Diagnostics.DiagnosticSource` for lightweight event emission
- `System.Runtime.CompilerServices.RuntimeHelpers` for JIT hooks
- Coverlet (the standard .NET coverage tool) can emit per-test coverage data
- .NET 10's improved JIT inlining means coverage instrumentation has lower overhead
- The `Microsoft.CodeCoverage` package provides programmatic access to coverage results

The key insight: Conjecture already generates from a byte buffer and tracks which buffer regions map to which strategy draws. Coverage feedback can influence *which buffer mutations the targeted testing hill climber tries next* — reusing the existing targeting infrastructure.

## Key Ideas

### Architecture
```
┌─────────────────────────────────────────────┐
│ Property Runner                             │
│                                             │
│  Strategy Engine ──► Example Generation     │
│       ▲                    │                │
│       │                    ▼                │
│  Coverage Feedback    Execute Property      │
│       ▲                    │                │
│       │                    ▼                │
│  Coverage Collector ◄── Branch Coverage     │
└─────────────────────────────────────────────┘
```

### Integration with Targeted Testing
```csharp
[Property(CoverageGuided = true)]
public bool ParseHandlesAllInputs(string input)
{
    // Conjecture observes which branches are hit
    // and biases generation toward inputs that reach new branches
    var result = MyParser.TryParse(input, out var value);
    return result ? value != null : true;
}
```

- Coverage data feeds into `Target.Maximize()` automatically — the "score" is the number of unique branches hit
- Hill climber mutates the byte buffer to maximize branch coverage
- No user-visible API change — just a settings flag

### Coverage Collection Strategies
1. **Lightweight: Branch counter instrumentation**
   - Instrument the system under test at JIT time with branch counters
   - Map each counter to a bit in a coverage bitmap (AFL-style)
   - Compare bitmaps across examples to detect new coverage

2. **Medium: Coverlet integration**
   - Use Coverlet's `Coverage` API to collect per-example coverage
   - More accurate but higher overhead
   - Suitable for slower, IO-bound properties

3. **Heavyweight: Full line/branch coverage**
   - Collect detailed coverage reports
   - Use for exploration/debugging, not production test runs
   - Export as coverage reports compatible with CI tools

### Hybrid Mode
```csharp
new ConjectureSettings
{
    CoverageGuided = true,
    TargetingProportion = 0.7,  // 70% of budget guided by coverage
    MaxExamples = 10_000,       // more budget needed for coverage exploration
}
```

- First N examples use standard random generation (establish baseline coverage)
- Remaining budget uses coverage-guided mutation (explore new branches)
- Shrinking is unchanged — operates on the failing buffer regardless of how it was generated

## Design Decisions to Make

1. Which coverage collection mechanism? Lightweight bitmap (fast, approximate) vs Coverlet (accurate, slower)?
2. How to instrument only the system under test, not the test framework and Conjecture itself?
3. Should coverage guidance replace or augment targeted testing? (They share the hill climber)
4. Performance budget: how much overhead is acceptable per example for coverage collection?
5. How to handle coverage of external dependencies (database drivers, HTTP clients)?
6. Should coverage data persist across test runs? (Build a corpus like fuzzers do)

## Scope Estimate

Large. Requires coverage instrumentation integration, hill climber modifications, and careful performance tuning. ~4-5 cycles. Research-level in places.

## Dependencies

- Coverlet or `Microsoft.CodeCoverage` for coverage collection
- Existing `HillClimber` and targeted testing infrastructure
- `ConjectureData` byte buffer for mutation
- .NET 10 JIT (lower instrumentation overhead)

## Open Questions

- Is the coverage-bitmap approach (AFL-style) feasible in managed .NET without native instrumentation?
- How does coverage-guided generation interact with `Assume.That()` rejections? (Rejected examples may still provide coverage signal)
- Should we build a persistent corpus (like fuzzing corpora) that accumulates interesting inputs across runs?
- How to present coverage improvements to the user? (Before/after coverage percentages?)
- Is there prior art in managed-language coverage-guided PBT? (Hypothesis doesn't do this; some Java tools experiment with it)
- Should this integrate with the OTel observability draft for coverage metrics?
