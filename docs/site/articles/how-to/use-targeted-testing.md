# How to use targeted testing

Random generation is excellent at finding bugs distributed across the input space. Targeted testing steers generation toward extremes — very long lists, deeply nested structures, values near boundaries — using your feedback to guide the search.

> [!NOTE]
> For background on how the hill-climbing phase works and when to use it, see [Understanding targeted testing](../explanation/targeted-testing.md).

## When to use targeted testing

Use `Target.Maximize` or `Target.Minimize` when:

- Bugs are **concentrated at extremes** (large inputs, edge values).
- You want to stress-test performance characteristics (e.g., find inputs that cause O(n²) behaviour).
- Pure random generation would need thousands of examples to reach the interesting region.

For properties where bugs are uniformly distributed, plain `[Property]` tests are simpler and equally effective.

## Basic usage

Call `Target.Maximize` (or `Target.Minimize`) anywhere in a `[Property]` test body:

```csharp
[Property]
public bool Sorting_preserves_length(List<int> xs)
{
    List<int> sorted = xs.OrderBy(x => x).ToList();

    // Tell the engine to seek longer lists
    Target.Maximize(xs.Count, "list_length");

    return xs.Count == sorted.Count;
}
```

`Target.Minimize` negates the score — use it when smaller is harder to find:

```csharp
[Property]
public bool Compression_never_expands_empty_input(byte[] data)
{
    Target.Minimize(data.Length, "input_size");

    byte[] compressed = Compress(data);
    return compressed.Length <= data.Length || data.Length == 0;
}
```

The `label` parameter (default `"default"`) identifies the metric. Multiple labels are optimized independently.

## Track multiple metrics

A single property can maximize multiple independent dimensions:

```csharp
[Property]
public bool Graph_traversal_visits_all_nodes(int[,] adjacency)
{
    Target.Maximize(NodeCount(adjacency), "nodes");
    Target.Maximize(EdgeCount(adjacency), "edges");

    IEnumerable<int> visited = BreadthFirst(adjacency, start: 0);
    return NodeCount(adjacency) == visited.Count();
}
```

The targeting budget is divided evenly across all labels observed during the generation phase.

## Use targeting inside `Strategy.Compose`

If you build strategies from `Strategy.Compose`, record observations via `ctx.Target`:

```csharp
Strategy<List<int>> longListStrategy = Strategy.Compose(ctx =>
{
    List<int> xs = ctx.Generate(Strategy.Lists(Strategy.Integers<int>(), 0, 1000));
    ctx.Target(xs.Count, "size");
    return xs;
});
```

`ctx.Target` is equivalent to `Target.Maximize` — both write to the same observation store for the current test case.

## Adjust targeting settings

| Setting | Default | Meaning |
|---|---|---|
| `Targeting` | `true` | Enable or disable the targeting phase entirely |
| `TargetingProportion` | `0.5` | Fraction of `MaxExamples` reserved for targeting. Must be in `[0.0, 1.0)` |

```csharp
[Property]
[ConjectureSettings(Targeting = true, TargetingProportion = 0.7)]
public void Heavy_targeting(List<int> xs)
{
    Target.Maximize(xs.Count);
    // 70% of MaxExamples spent on hill climbing
}
```

Setting `TargetingProportion = 0.0` disables targeting without removing the `Target.Maximize` calls — useful for comparing baseline vs. targeted runs.

## Read the output

When a targeted run completes without failure, the final scores appear in the test output:

```text
Passed 100 examples (50 generation + 50 targeting).
Target scores:
  list_length = 87.0000
```

When targeting is enabled and a failure is found, the failure message includes both the counterexample and the scores recorded up to that point.

## Best practices

**Choose metrics that correlate with bug likelihood.** Number of unique keys in a map, depth of a parse tree, total size of nested collections — these capture structural complexity that tends to expose bugs.

**Use one label per logical dimension.** Mixing unrelated scores into one label loses information.

**Combine with seeds for debugging.** When a targeted run finds a bug, pin the seed with `[ConjectureSettings(Seed = ...)]` to reproduce the exact targeting path. See [How to reproduce a failure](reproduce-a-failure.md).
