# Recursive Strategies API Reference

`Strategy.Recursive<T>` generates bounded-depth recursive structures (trees, ASTs, JSON-like values) that shrink toward base cases automatically.

## API

```csharp
Strategy<T> Strategy.Recursive<T>(
    Strategy<T> baseCase,
    Func<Strategy<T>, Strategy<T>> recursive,
    int maxDepth = 5)
```

| Parameter | Meaning |
|---|---|
| `baseCase` | Strategy for leaf nodes (depth 0) |
| `recursive` | Receives a `self` strategy (at depth − 1) and returns a strategy for non-leaf nodes |
| `maxDepth` | Maximum recursion depth. Must be ≥ 0. |

The engine draws a target depth from `[0, maxDepth]` per example. The shrinker reduces depth toward 0.

## Example: Expression Trees

```csharp
Strategy<Expr> baseCase = Strategy.Integers<int>(0, 100)
    .Select(n => (Expr)new Literal(n));

Strategy<Expr> exprStrategy = Strategy.Recursive<Expr>(
    baseCase,
    self => Strategy.OneOf(
        baseCase,
        Strategy.Tuples(self, self).Select(t => (Expr)new Add(t.Item1, t.Item2)),
        Strategy.Tuples(self, self).Select(t => (Expr)new Mul(t.Item1, t.Item2))),
    maxDepth: 5);
```

## Shrinking

On failure, the shrinker simultaneously reduces:
- **Depth** — the target depth IR integer is reduced toward 0
- **Breadth** — collection sizes (array/dict lengths) shrink via collection reduction
- **Values** — leaf integers and strings shrink by their own passes

Result: a failing tree typically shrinks to a depth-0 or depth-1 structure.

## Composing with Other Combinators

Recursive strategies are ordinary `Strategy<T>` values:

```csharp
exprStrategy.Where(e => Eval(e) > 0)          // filter
exprStrategy.Select(ExprDepth)                 // project
Strategy.Tuples(exprStrategy, Strategy.Strings()) // combine
```

`ctx.Generate(recursiveStrategy)` inside `Strategy.Compose` integrates depth tracking transparently.

## Stack Safety

Uses C# call-stack recursion proportional to `maxDepth`. Values up to `maxDepth = 20` are routinely safe.