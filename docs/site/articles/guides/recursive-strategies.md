# Recursive Strategies

Some data structures are inherently recursive — expression trees, JSON documents, abstract syntax trees, nested lists. Generating these with flat combinators means writing your own depth-control logic on top of the engine. `Generate.Recursive<T>` handles this for you: it generates bounded-depth recursive structures that shrink toward base cases automatically.

## When to Use Recursive Strategies

Use `Generate.Recursive<T>` when:

- Your type is **self-referential** (a node that contains child nodes of the same type).
- You need to test a parser, evaluator, transformer, or serializer on **tree-shaped inputs**.
- You want the shrinker to **reduce depth** on failure, not just simplify leaf values.

For flat types (integers, lists, dictionaries), the standard strategies are simpler and sufficient.

## API

```csharp
Strategy<T> Generate.Recursive<T>(
    Strategy<T> baseCase,
    Func<Strategy<T>, Strategy<T>> recursive,
    int maxDepth = 5)
```

| Parameter | Meaning |
|---|---|
| `baseCase` | Strategy for leaf nodes (depth 0). Used when the target depth is exhausted. |
| `recursive` | Function that receives a `self` strategy and returns a strategy for non-leaf nodes. `self` recurses at depth − 1. |
| `maxDepth` | Maximum recursion depth. Generated values have depth in `[0, maxDepth]`. Must be ≥ 0. |

The engine draws a target depth from `[0, maxDepth]` for each example. The shrinker reduces this target depth toward 0, so failing cases simplify toward shallow trees.

## Example: Expression Trees

Define an expression ADT and generate trees of bounded depth:

```csharp
public abstract class Expr { }
public sealed class Literal(int value) : Expr { public int Value { get; } = value; }
public sealed class Add(Expr left, Expr right) : Expr { public Expr Left { get; } = left; public Expr Right { get; } = right; }
public sealed class Mul(Expr left, Expr right) : Expr { public Expr Left { get; } = left; public Expr Right { get; } = right; }

private static int Eval(Expr expr) => expr switch
{
    Literal lit => lit.Value,
    Add add => Eval(add.Left) + Eval(add.Right),
    Mul mul => Eval(mul.Left) * Eval(mul.Right),
    _ => throw new InvalidOperationException()
};

Strategy<Expr> baseCase = Generate.Integers<int>(0, 100)
    .Select(n => (Expr)new Literal(n));

Strategy<Expr> exprStrategy = Generate.Recursive<Expr>(
    baseCase,
    self => Generate.OneOf(
        baseCase,
        Generate.Tuples(self, self).Select(t => (Expr)new Add(t.Item1, t.Item2)),
        Generate.Tuples(self, self).Select(t => (Expr)new Mul(t.Item1, t.Item2))),
    maxDepth: 5);

[Property]
public void Eval_nonNegativeLiterals_resultIsNonNegative([From<ExprProvider>] Expr expr)
{
    Assert.True(Eval(expr) >= 0);
}
```

When a failure is found (e.g., because `Mul` was implemented as subtraction), the shrinker reduces the tree depth and simplifies literal values. The result is typically a depth-1 or depth-0 tree — the minimal example that exposes the bug.

## Depth Control

`maxDepth` caps the *target* depth drawn from the IR stream. At `maxDepth = 0`, only `baseCase` is ever used. At `maxDepth = 3`, the engine can generate trees with up to 3 levels of nesting.

```csharp
// Always a leaf
Strategy<Expr> leaves = Generate.Recursive<Expr>(baseCase, self => baseCase, maxDepth: 0);

// Up to 3 levels deep
Strategy<Expr> shallow = Generate.Recursive<Expr>(baseCase, self => ..., maxDepth: 3);
```

The actual depth of any generated value is at most `maxDepth` — the engine substitutes `baseCase` whenever remaining depth reaches 0, regardless of what `recursive` produces.

## Example: JSON-Like Values

More complex ADTs work the same way. Here is a JSON-like value type:

```csharp
public abstract class JsonValue { }
public sealed class JNull : JsonValue { }
public sealed class JBool(bool value) : JsonValue { public bool Value { get; } = value; }
public sealed class JNumber(double value) : JsonValue { public double Value { get; } = value; }
public sealed class JString(string value) : JsonValue { public string Value { get; } = value; }
public sealed class JArray(IReadOnlyList<JsonValue> items) : JsonValue { public IReadOnlyList<JsonValue> Items { get; } = items; }
public sealed class JObject(IReadOnlyDictionary<string, JsonValue> props) : JsonValue { public IReadOnlyDictionary<string, JsonValue> Properties { get; } = props; }

private static readonly Strategy<JsonValue> ScalarStrategy = Generate.OneOf(
    Generate.Just<JsonValue>(new JNull()),
    Generate.Booleans().Select(b => (JsonValue)new JBool(b)),
    Generate.Doubles(-1000, 1000).Select(d => (JsonValue)new JNumber(d)),
    Generate.Strings(0, 10).Select(s => (JsonValue)new JString(s)));

private static Strategy<JsonValue> JsonStrategy(int maxDepth) =>
    Generate.Recursive<JsonValue>(
        ScalarStrategy,
        self => Generate.OneOf(
            ScalarStrategy,
            Generate.Lists(self, 0, 5).Select(items => (JsonValue)new JArray(items)),
            Generate.Dictionaries(Generate.Strings(1, 8), self, 0, 3)
                .Select(d => (JsonValue)new JObject(d))),
        maxDepth);
```

This generates the full range of JSON shapes — nulls, booleans, numbers, strings, arrays (with nested values), and objects (with nested values) — at any depth up to `maxDepth`.

## Shrinking

Shrinking of recursive strategies works through the existing integer reduction pass:

1. The depth target drawn at the start of each example is an IR integer. The shrinker reduces it toward 0, which forces shallower structures.
2. Leaf values (integers, strings, etc.) are shrunk by their own passes.
3. Container sizes (list length, dictionary size) are shrunk by the collection reduction pass.

The result: a failing tree shrinks simultaneously in depth, breadth, and value magnitude. A complex JSON document that triggers a bug typically shrinks to an empty array `[]` or empty object `{}` — whichever is the simplest structure that still fails.

## Composing with Other Combinators

Recursive strategies are ordinary `Strategy<T>` values and compose freely:

```csharp
// Filter: only keep expressions that evaluate to a positive result
Strategy<Expr> positiveExpr = exprStrategy.Where(e => Eval(e) > 0);

// Project: extract the depth of the generated tree
Strategy<int> depthStrategy = exprStrategy.Select(ExprDepth);

// Pair with another strategy
Strategy<(Expr, string)> labelledExpr = Generate.Tuples(exprStrategy, Generate.Strings(1, 10));
```

`Generate.Compose` also works — call `ctx.Generate(recursiveStrategy)` inside the factory and the depth tracking integrates transparently.

## Stack Safety

`Generate.Recursive<T>` uses C# call-stack recursion proportional to `maxDepth`. Values up to `maxDepth = 20` are routinely safe. For very large depths (hundreds or thousands), the generator itself may stack overflow before your evaluator does — keep `maxDepth` in the range that makes sense for your domain.
