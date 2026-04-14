# How to explore strategies interactively

Visualise strategy output with `Conjecture.Interactive` in any C# application or file-based app.

## Install

```bash
dotnet add package Conjecture.Interactive
```

Or in a [file-based app](https://learn.microsoft.com/en-us/dotnet/core/sdk/file-based-apps):

```csharp
#:package Conjecture.Interactive
```

## Preview sample values

Call `.Preview()` on any strategy to see a comma-separated list of samples:

```csharp
Console.WriteLine(Generate.Integers<int>(0, 100).Preview());
```

Pass `count` (default 20, max 100) and `seed` for reproducibility:

```csharp
Console.WriteLine(Generate.Strings(5, 20).Preview(count: 10, seed: 42));
```

## Show an indexed sample table

`.SampleTable()` renders a two-column index/value text table:

```csharp
Console.WriteLine(Generate.Doubles(-1, 1).SampleTable(count: 5));
```

## Plot a distribution histogram

`.Histogram()` returns a text bar chart. Works on any `IConvertible` strategy:

```csharp
Console.WriteLine(Generate.Integers<int>(0, 1000).Histogram());
```

Use the `selector` overload for non-numeric strategies:

```csharp
Console.WriteLine(Generate.Strings(0, 50).Histogram(s => s.Length));
```

Tweak `sampleSize` (default 1000) and `bucketCount` (default 20) for resolution.

## Trace a shrink sequence

`.ShrinkTrace()` shows step-by-step minimisation toward a counterexample:

```csharp
ShrinkTraceResult<int> trace = Generate.Integers<int>().ShrinkTrace(seed: 42, x => x < 1000);
Console.WriteLine(trace.Text);
```

The result is a `ShrinkTraceResult<T>` with `.Steps` (list of `ShrinkStep<T>`) and `.Text` (rendered table).

> [!NOTE]
> The property must fail on the value generated from the given seed, otherwise `ArgumentException` is thrown.
