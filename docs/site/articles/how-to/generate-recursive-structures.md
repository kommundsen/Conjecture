# How to generate recursive structures

Some data structures are inherently recursive — expression trees, JSON documents, abstract syntax trees, nested lists. `Strategy.Recursive<T>` generates bounded-depth recursive structures that shrink toward base cases automatically.

> [!NOTE]
> For how recursive shrinking works internally, see [Understanding shrinking](../explanation/shrinking.md).

## When to use

Use `Strategy.Recursive<T>` when:

- Your type is **self-referential** (a node that contains child nodes of the same type).
- You need to test a parser, evaluator, transformer, or serializer on **tree-shaped inputs**.
- You want the shrinker to **reduce depth** on failure, not just simplify leaf values.

For flat types (integers, lists, dictionaries), the standard strategies are simpler and sufficient.

## API

```csharp
Strategy<T> Strategy.Recursive<T>(
    Strategy<T> baseCase,
    Func<Strategy<T>, Strategy<T>> recursive,
    int maxDepth = 5)
```

| Parameter | Meaning |
|---|---|
| `baseCase` | Strategy for leaf nodes (depth 0). Used when the target depth is exhausted. |
| `recursive` | Function that receives a `self` strategy and returns a strategy for non-leaf nodes. `self` recurses at depth − 1. |
| `maxDepth` | Maximum recursion depth. Generated values have depth in `[0, maxDepth]`. Must be ≥ 0. |

## Example: expression trees

```csharp
public abstract class Expr { }
public sealed class Literal(int value) : Expr { public int Value { get; } = value; }
public sealed class Add(Expr left, Expr right) : Expr { public Expr Left { get; } = left; public Expr Right { get; } = right; }
public sealed class Mul(Expr left, Expr right) : Expr { public Expr Left { get; } = left; public Expr Right { get; } = right; }

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

When a failure is found, the shrinker reduces tree depth and simplifies leaf values. The result is typically a depth-1 or depth-0 tree.

## Example: JSON-like values

```csharp
public abstract class JsonValue { }
public sealed class JNull : JsonValue { }
public sealed class JBool(bool value) : JsonValue { public bool Value { get; } = value; }
public sealed class JNumber(double value) : JsonValue { public double Value { get; } = value; }
public sealed class JString(string value) : JsonValue { public string Value { get; } = value; }
public sealed class JArray(IReadOnlyList<JsonValue> items) : JsonValue { public IReadOnlyList<JsonValue> Items { get; } = items; }
public sealed class JObject(IReadOnlyDictionary<string, JsonValue> props) : JsonValue
{
    public IReadOnlyDictionary<string, JsonValue> Properties { get; } = props;
}

private static readonly Strategy<JsonValue> ScalarStrategy = Strategy.OneOf<JsonValue>(
    Strategy.Just<JsonValue>(new JNull()),
    Strategy.Booleans().Select(b => (JsonValue)new JBool(b)),
    Strategy.Doubles(-1000, 1000).Select(d => (JsonValue)new JNumber(d)),
    Strategy.Strings(0, 10).Select(s => (JsonValue)new JString(s)));

private static Strategy<JsonValue> JsonStrategy(int maxDepth) =>
    Strategy.Recursive<JsonValue>(
        ScalarStrategy,
        self => Strategy.OneOf<JsonValue>(
            ScalarStrategy,
            Strategy.Lists(self, 0, 5).Select(items => (JsonValue)new JArray(items)),
            Strategy.Dictionaries(Strategy.Strings(1, 8), self, 0, 3)
                .Select(d => (JsonValue)new JObject(d))),
        maxDepth);
```

## Control depth

`maxDepth` caps the target depth drawn per example. At `maxDepth = 0`, only `baseCase` is ever used.

```csharp
// Always a leaf — useful for baseline comparison
Strategy<Expr> leaves = Strategy.Recursive<Expr>(baseCase, self => baseCase, maxDepth: 0);

// Up to 3 levels deep
Strategy<Expr> shallow = Strategy.Recursive<Expr>(baseCase, self => ..., maxDepth: 3);
```

## Compose with other combinators

Recursive strategies are ordinary `Strategy<T>` values:

```csharp
// Filter
Strategy<Expr> positiveExpr = exprStrategy.Where(e => Eval(e) > 0);

// Project
Strategy<int> depthStrategy = exprStrategy.Select(ExprDepth);

// Inside Strategy.Compose
var strategy = Strategy.Compose(ctx =>
{
    Expr expr = ctx.Generate(exprStrategy);
    // ...
});
```

## Stack safety

`Strategy.Recursive<T>` uses C# call-stack recursion proportional to `maxDepth`. Values up to `maxDepth = 20` are routinely safe. For very deep structures, keep `maxDepth` in the range that makes sense for your domain.
