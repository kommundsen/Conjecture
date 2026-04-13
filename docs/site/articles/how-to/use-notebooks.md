# How to explore strategies in notebooks

Visualise strategy output interactively with `Conjecture.Interactive` in Polyglot Notebooks.

## Install

In a `.dib` notebook cell:

```csharp
#r "nuget: Conjecture.Core"
#r "nuget: Conjecture.Interactive"
using Conjecture.Core;
using Conjecture.Interactive;
```

The kernel extension loads automatically.

## Preview sample values

Call `.Preview()` on any strategy to see a quick HTML table:

```csharp
Generate.Integers<int>(0, 100).Preview()
```

Pass `count` (default 20, max 100) and `seed` for reproducibility:

```csharp
Generate.Strings(5, 20).Preview(count: 10, seed: 42)
```

## Show an indexed sample table

`.SampleTable()` renders a two-column index/value table:

```csharp
Generate.Doubles(-1, 1).SampleTable(count: 5)
```

## Plot a distribution histogram

`.Histogram()` returns an SVG histogram. Works on any `IConvertible` strategy:

```csharp
Generate.Integers<int>(0, 1000).Histogram()
```

Use the `selector` overload for non-numeric strategies:

```csharp
Generate.Strings(0, 50).Histogram(s => s.Length)
```

Tweak `sampleSize` (default 1000) and `bucketCount` (default 20) for resolution.

## Trace a shrink sequence

`.ShrinkTrace()` shows step-by-step minimisation toward a counterexample:

```csharp
Generate.Integers<int>().ShrinkTrace(seed: 42, x => x < 1000)
```

The result is a `ShrinkTraceResult<T>` with `.Steps` (list of `ShrinkStep<T>`) and `.Html` (rendered table).

> [!NOTE]
> The property must fail on the value generated from the given seed, otherwise `ArgumentException` is thrown.

## See also

- [Quick Start notebook](https://github.com/kommundsen/Conjecture/blob/main/docs/notebooks/Conjecture-QuickStart.dib)
