# Targeted Testing API Reference

Targeted testing steers generation toward extremes (large inputs, edge values) after an initial random phase. Use it when bugs are concentrated at boundaries that pure random generation rarely reaches.

## `Target.Maximize` / `Target.Minimize`

```csharp
Target.Maximize(double score, string label = "default");
Target.Minimize(double score, string label = "default");
```

Call from inside a `[Property]` test body. The engine uses the score to hill-climb toward higher (or lower) values during the targeting phase.

```csharp
[Property]
public void Sorting_preserves_length(List<int> xs)
{
    Target.Maximize(xs.Count, "list_length");
    Assert.Equal(xs.Count, xs.OrderBy(x => x).ToList().Count);
}
```

`Target.Minimize(x)` is sugar for `Target.Maximize(-x)`.

## Inside `Generate.Compose`

```csharp
Generate.Compose(ctx =>
{
    var xs = ctx.Generate(Generate.Lists(Generate.Integers<int>(), 0, 1000));
    ctx.Target(xs.Count, "size"); // equivalent to Target.Maximize
    return xs;
});
```

## Multiple Labels

Multiple labels are optimized independently in round-robin order:

```csharp
Target.Maximize(NodeCount(adjacency), "nodes");
Target.Maximize(EdgeCount(adjacency), "edges");
```

## Settings

| Setting | Default | Meaning |
|---|---|---|
| `Targeting` | `true` | Enable or disable the targeting phase |
| `TargetingProportion` | `0.5` | Fraction of `MaxExamples` used for hill-climbing. Must be in `[0.0, 1.0)`. |

```csharp
[ConjectureSettings(Targeting = true, TargetingProportion = 0.7)]
```

## How It Works

1. **Generation phase** — `(1 − TargetingProportion) × MaxExamples` random examples. Best IR buffer per label is retained.
2. **Hill-climbing phase** — mutates the best IR buffer (increment, decrement, binary search each IR node) toward better scores.

Targeting never interferes with failure reporting or shrink quality. Observations are advisory only.
