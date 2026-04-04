# Targeted Testing

Random generation is excellent at finding bugs in the bulk of the input space, but it can miss bugs that only appear at extremes — very long lists, deeply nested structures, values near integer boundaries. Targeted testing bridges the gap: after an initial random phase, the engine uses your feedback to *steer* generation toward the regions you care about.

## When to Use Targeted Testing

Use `Target.Maximize` or `Target.Minimize` when:

- You have a property where bugs are **concentrated at extremes** (large inputs, edge values).
- You want to stress-test performance characteristics (e.g., find inputs that cause O(n²) behaviour).
- Pure random generation would need thousands of examples to reach the interesting region.

For properties where bugs are uniformly distributed, plain `[Property]` tests with random generation are simpler and equally effective.

## Basic Usage: `Target.Maximize` and `Target.Minimize`

Call `Target.Maximize` (or `Target.Minimize`) anywhere in a `[Property]` test body:

```csharp
[Property]
public void Sorting_preserves_length(List<int> xs)
{
    List<int> sorted = xs.OrderBy(x => x).ToList();

    // Tell the engine to seek longer lists
    Target.Maximize(xs.Count, "list_length");

    Assert.Equal(xs.Count, sorted.Count);
}
```

`Target.Minimize` is sugar for negating the score — use it when smaller is harder:

```csharp
[Property]
public void Compression_never_expands_empty_input(byte[] data)
{
    Target.Minimize(data.Length, "input_size");

    byte[] compressed = Compress(data);
    Assert.True(compressed.Length <= data.Length || data.Length == 0);
}
```

The `label` parameter (default `"default"`) identifies the metric. Multiple labels are optimized independently in round-robin order.

## Multiple Labels

A single property can track multiple metrics:

```csharp
[Property]
public void Graph_traversal_visits_all_nodes(int[,] adjacency)
{
    Target.Maximize(NodeCount(adjacency), "nodes");
    Target.Maximize(EdgeCount(adjacency), "edges");

    IEnumerable<int> visited = BreadthFirst(adjacency, start: 0);
    Assert.Equal(NodeCount(adjacency), visited.Count());
}
```

The targeting budget is divided evenly across all labels observed during the generation phase.

## Usage Inside `Generate.Compose`

If you build strategies from `Generate.Compose`, you can record observations directly on the generator context via `ctx.Target`:

```csharp
Strategy<List<int>> longListStrategy = Generate.Compose(ctx =>
{
    List<int> xs = ctx.Generate(Generate.Lists(Generate.Integers<int>(), 0, 1000));
    ctx.Target(xs.Count, "size");
    return xs;
});
```

`ctx.Target` is equivalent to `Target.Maximize` — both write into the same observation store for the current test case.

## Settings

Control targeting behaviour via `ConjectureSettings` or the `[ConjectureSettings]` attribute:

| Setting | Default | Meaning |
|---|---|---|
| `Targeting` | `true` | Enable or disable the targeting phase entirely |
| `TargetingProportion` | `0.5` | Fraction of `MaxExamples` reserved for targeting. Must be in `[0.0, 1.0)`. |

```csharp
[Property]
[ConjectureSettings(Targeting = true, TargetingProportion = 0.7)]
public void Heavy_targeting(List<int> xs)
{
    Target.Maximize(xs.Count);
    // 70% of MaxExamples spent on hill climbing
}
```

Setting `TargetingProportion = 0.0` disables targeting without removing the `Target.Maximize` calls (useful for comparing baseline vs. targeted runs).

## How It Works

Targeting runs in two phases:

1. **Generation phase** — the engine generates `(1 − TargetingProportion) × MaxExamples` examples randomly. For each example, it records the current score per label alongside the IR nodes that produced the example. The best-scoring IR buffer per label is retained.

2. **Hill-climbing phase** — for each label, the engine takes the best IR buffer and mutates it: it tries incrementing, decrementing, and binary-searching each integer-like IR node toward better scores. Random perturbations help escape local maxima. A mutation is kept only if it improves the score.

The two phases together use at most `MaxExamples` test cases. If a property fails during hill climbing, shrinking proceeds exactly as for a random counterexample — targeting never interferes with failure reporting or shrink quality.

> **Note:** Observations are purely advisory. They steer *generation* but have no effect on shrinking. Shrinking operates only on interestingness (whether the test throws).

## Best Practices

**Choose metrics that correlate with bug likelihood.** "Number of unique keys in a map", "depth of a parse tree", and "total size of nested collections" are good candidates — they capture structural complexity that tends to expose bugs.

**Use a single label per logical dimension.** If you're maximizing both list size and element magnitude, use two labels. Mixing them into one score loses information.

**Don't discard failing examples.** `Target.Maximize` is called on every example during the generation phase, including ones that fail. The engine handles this correctly — it aborts targeting and shrinks the counterexample immediately.

**Combine with seeds for debugging.** When a targeted run finds a bug, the failure output includes the seed. Pin it with `[ConjectureSettings(Seed = ...)]` to reproduce the exact targeting path.

## Failure Output

When targeting is enabled and a run completes without failure, the final scores appear in the test output:

```
Passed 100 examples (50 generation + 50 targeting).
Target scores:
  list_length = 87.0000
```

When targeting is enabled and a failure is found, the failure message includes both the counterexample and the scores recorded up to that point.
