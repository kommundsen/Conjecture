# Draft: .NET Interactive / Polyglot Notebook Support

## Motivation

.NET Interactive (Polyglot Notebooks) is widely used for data exploration, prototyping, and documentation. Property-based testing benefits from an exploratory workflow: compose a strategy, visualize what it generates, inspect shrink behavior, iterate on constraints. A notebook-friendly API lets developers build intuition about their strategies before committing them to test suites — and serves as living documentation for how data generation works.

## .NET Advantage

.NET Interactive provides rich output formatting via `ITypeFormatter<T>`, inline chart rendering, and `#r "nuget:..."` for zero-config package installation. Conjecture's strategies can render as distribution histograms, shrink trees, or sample tables directly in the notebook output cell. The REPL-style workflow is a natural fit for strategy authoring and debugging.

## Key Ideas

### Quick Start in a Notebook
```csharp
#r "nuget: Conjecture.Core"
using Conjecture;

// See what a strategy generates
Generate.Integers<int>(-100, 100).Preview(count: 20)
// Output: [-47, 0, 83, -12, 1, 99, -100, 0, 3, ...]
```

### Distribution Visualization
```csharp
// Histogram of generated values
Generate.Integers<int>(0, 100).Histogram(sampleSize: 10_000)
// Output: renders inline histogram showing distribution shape

// Distribution of string lengths
Generate.Strings(minLength: 0, maxLength: 50).Histogram(x => x.Length, sampleSize: 5000)
// Output: histogram of string lengths
```

### Strategy Exploration
```csharp
// Sample a complex strategy and display as a table
var personStrategy = Generate.Compose<Person>(ctx => new Person(
    ctx.Generate(Generate.Strings(1, 20)),
    ctx.Generate(Generate.Integers<int>(0, 120))
));

personStrategy.SampleTable(count: 10)
// Output:
// | Name     | Age |
// |----------|-----|
// | "abc"    | 42  |
// | "x"      | 0   |
// | ...      | ... |
```

### Shrink Trace Inspection
```csharp
// See how a value shrinks
Generate.Lists(Generate.Integers<int>(0, 100), minSize: 1, maxSize: 20)
    .ShrinkTrace(
        seed: 42,
        property: xs => xs.Sum() < 50
    )
// Output:
// Original:  [73, 91, 12, 45, 88, 3, 67]  (sum=379)
// Step 1:    [73, 91, 12, 45]              (sum=221)
// Step 2:    [73, 91]                       (sum=164)
// Step 3:    [50, 0]                        (sum=50)
// Step 4:    [50]                           (sum=50)
// Minimal:   [50]                           (sum=50)
```

### Stateful Testing Visualization
```csharp
// Visualize state machine execution
Generate.StateMachine<CounterMachine>(maxSteps: 10)
    .SampleExecution()
// Output:
// State 0: Counter(0)
//   → Increment
// State 1: Counter(1)
//   → Increment
// State 2: Counter(2)
//   → Decrement
// State 3: Counter(1)
```

### Extension Methods for Notebook Context
- `.Preview(count)` — sample N values and display
- `.Histogram(sampleSize)` — render distribution histogram
- `.SampleTable(count)` — tabular display for complex types
- `.ShrinkTrace(seed, property)` — step-by-step shrink visualization
- `.SampleExecution()` — state machine trace display

## Design Decisions to Make

1. Ship as `Conjecture.Interactive` package or include in Core with conditional notebook detection?
2. How to detect notebook context? (`KernelInvocationContext.Current != null` or explicit opt-in)
3. Chart rendering: use Plotly.NET, XPlot, or custom SVG/HTML output?
4. Should `.Preview()` / `.Histogram()` be available outside notebooks (e.g., console output)?
5. How to handle large sample sizes without overwhelming notebook output?
6. F# notebook support: should the F# draft's `Gen` module also have notebook extensions?

## Scope Estimate

Small-Medium. Core display extensions are ~1 cycle. Histogram/chart rendering adds ~1 more.

## Dependencies

- `Microsoft.DotNet.Interactive` (for `ITypeFormatter<T>` and kernel integration)
- `Conjecture.Core` strategy engine
- A charting library (TBD) for histogram rendering
- Existing `Strategy<T>.Sample()` infrastructure (from standalone data generation draft)

## Open Questions

- What's the overlap with the standalone data generation draft? (`.Preview()` and `.Sample()` are related)
- Should notebook output be interactive (clickable shrink steps, expandable traces)?
- How to handle notebook cell re-execution with different seeds? (Determinism vs exploration)
- Is there demand for a "strategy playground" web UI beyond notebooks?
- Should we provide pre-built notebook templates (`.dib` files) as documentation?
