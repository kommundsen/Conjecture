# Conjecture.LinqPad

LINQPad integration for [Conjecture](https://github.com/kommundsen/Conjecture). Renders strategy samples, distribution histograms, and shrink traces as rich HTML in LINQPad's `Dump()` output instead of plain text.

## Install

In LINQPad: <kbd>F4</kbd> → **Add NuGet…** → `Conjecture.LinqPad` (it transitively pulls `Conjecture.Core` and `Conjecture.Interactive`). Then add namespaces `Conjecture.Core`, `Conjecture.Interactive`, and `Conjecture.LinqPad`.

## Usage

```csharp
// Dump a few samples directly
new StrategyCustomMemberProvider<int>(Strategy.Integers<int>(0, 100)).Dump();

// Visualise a shrink trace as an HTML table
Strategy.Integers<int>(0, 1_000_000)
    .ShrinkTraceHtml(seed: 42, x => x < 1_000)
    .Dump("Shrink trace");
```

`ShrinkTraceHtml` returns a LINQPad `Util.RawHtml` value, so `.Dump()` renders the trace as a styled HTML table rather than a flat list.

## Types

| Type | Role |
|---|---|
| `StrategyCustomMemberProvider<T>` | Adapts a `Strategy<T>` to LINQPad's custom-member-provider protocol so `.Dump()` shows samples instead of the strategy's internal state. |
| `StrategyLinqPadExtensions.ShrinkTraceHtml` | Runs a shrink trace and returns an HTML object suitable for `.Dump()`. |

## Links

- [GitHub](https://github.com/kommundsen/Conjecture)
- [Documentation](https://ommundsen.dev/Conjecture/)
- [License](https://github.com/kommundsen/Conjecture/blob/main/LICENSE-MIT.txt)