# Conjecture.Interactive

[Polyglot Notebooks](https://github.com/dotnet/interactive) extension for [Conjecture.NET](https://github.com/kommundsen/Conjecture). Explore strategy output interactively with sample tables, histograms, and shrink traces.

## Install

In a `.dib` notebook cell:

```
#r "nuget: Conjecture.Interactive"
```

The kernel extension loads automatically and registers HTML formatters for `Strategy<T>`.

## Usage

```csharp
using Conjecture.Core;
using Conjecture.Interactive;

// Quick-look at sample values
Generate.Integers<int>(0, 100).Preview()

// Distribution histogram (SVG)
Generate.Doubles(0, 1).Histogram()

// Step-by-step shrink trace
Generate.Integers<int>().ShrinkTrace(seed: 42, x => x < 1000)
```

## API

| Method | Returns | Description |
|---|---|---|
| `.Preview(count, seed)` | `string` (HTML) | Sample values in a single-row table |
| `.SampleTable(count, seed)` | `string` (HTML) | Indexed two-column sample table |
| `.Histogram(sampleSize, bucketCount, seed)` | `string` (SVG) | Distribution histogram |
| `.Histogram(selector, sampleSize, bucketCount, seed)` | `string` (SVG) | Histogram with projection function |
| `.ShrinkTrace(seed, failingProperty)` | `ShrinkTraceResult<T>` | Step-by-step shrink trace with HTML |

## Links

- [GitHub](https://github.com/kommundsen/Conjecture)
- [License](https://github.com/kommundsen/Conjecture/blob/main/LICENSE-MIT.txt)
