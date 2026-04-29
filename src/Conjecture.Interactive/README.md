# Conjecture.Interactive

Strategy visualization and exploration utilities for [Conjecture](https://github.com/kommundsen/Conjecture). Explore strategy output with sample tables, histograms, and shrink traces — all rendered as plain text for terminals and file-based apps.

## Install

```
dotnet add package Conjecture.Interactive
```

## Usage

```csharp
using Conjecture.Core;
using Conjecture.Interactive;

// Quick-look at sample values
Console.WriteLine(Strategy.Integers<int>(0, 100).Preview());

// Distribution histogram (text bar chart)
Console.WriteLine(Strategy.Doubles(0, 1).Histogram());

// Step-by-step shrink trace
ShrinkTraceResult<int> trace = Strategy.Integers<int>().ShrinkTrace(seed: 42, x => x < 1000);
Console.WriteLine(trace.Text);
```

## API

| Method | Returns | Description |
|---|---|---|
| `.Preview(count, seed)` | `string` | Comma-separated sample values |
| `.SampleTable(count, seed)` | `string` | Indexed two-column text table |
| `.Histogram(sampleSize, bucketCount, seed)` | `string` | Distribution bar chart |
| `.Histogram(selector, sampleSize, bucketCount, seed)` | `string` | Bar chart with projection function |
| `.ShrinkTrace(seed, failingProperty)` | `ShrinkTraceResult<T>` | Step-by-step shrink trace with `.Text` and `.Steps` |

## Links

- [GitHub](https://github.com/kommundsen/Conjecture)
- [License](https://github.com/kommundsen/Conjecture/blob/main/LICENSE-MIT.txt)