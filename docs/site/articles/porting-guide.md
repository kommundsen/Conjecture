# Porting Guide: Hypothesis to Conjecture

This guide maps Python Hypothesis concepts to their Conjecture equivalents. If you've used Hypothesis before, this is the fastest way to get productive.

## Core Concepts

| Hypothesis (Python) | Conjecture (.NET) | Notes |
|---|---|---|
| `@given(...)` | `[Property]` attribute | Attribute-driven; strategy resolved from parameter types |
| `@example(1, "foo")` | `[Example(1, "foo")]` | Explicit cases run before generated ones |
| `@settings(max_examples=500)` | `[Property(MaxExamples = 500)]` | Attribute properties or `ConjectureSettings` record |
| `assume(x > 0)` | `Assume.That(x > 0)` | Throws `UnsatisfiedAssumptionException` to skip |
| `note(message)` | *Not yet implemented* | |

## Strategies

| Hypothesis | Conjecture | Notes |
|---|---|---|
| `st.integers()` | `Strategy.Integers<T>()` | Generic over `IBinaryInteger<T>` — works for `int`, `long`, `byte`, etc. |
| `st.integers(min_value=0, max_value=100)` | `Strategy.Integers<int>(0, 100)` | |
| `st.floats()` | `Strategy.Doubles()` / `Strategy.Floats()` | Separate methods for `double` and `float` |
| `st.booleans()` | `Strategy.Booleans()` | |
| `st.text()` | `Strategy.Strings()` | `.NET uses `string`, not `text` |
| `st.text(min_size=1, max_size=50)` | `Strategy.Strings(minLength: 1, maxLength: 50)` | `size` → `length` |
| `st.binary(min_size=a, max_size=b)` | `Strategy.Arrays<byte>(Strategy.Integers<byte>(), a, b)` | Generic array factory, any element strategy |
| `st.just(value)` | `Strategy.Just(value)` | |
| `st.sampled_from([a, b, c])` | `Strategy.SampledFrom([a, b, c])` | Accepts `IReadOnlyList<T>` |
| `st.from_type(MyEnum)` | `Strategy.Enums<MyEnum>()` | |
| `st.none_of()` | `Strategy.Nullable(inner)` | Wraps a `Strategy<T>` where `T : struct` |
| `st.tuples(st.integers(), st.text())` | `Strategy.Tuples(Strategy.Integers<int>(), Strategy.Strings())` | Up to 4 elements |
| `st.lists(st.integers())` | `Strategy.Lists(Strategy.Integers<int>())` | Returns `Strategy<List<T>>` |
| `st.lists(st.integers(), min_size=1, max_size=10)` | `Strategy.Lists(Strategy.Integers<int>(), minSize: 1, maxSize: 10)` | |
| `st.frozensets(st.integers())` | `Strategy.Sets(Strategy.Integers<int>())` | Returns `Strategy<IReadOnlySet<T>>` |
| `st.dictionaries(st.text(), st.integers())` | `Strategy.Dictionaries(Strategy.Strings(), Strategy.Integers<int>())` | Returns `Strategy<IReadOnlyDictionary<TKey, TValue>>` |
| `st.one_of(st_a, st_b)` | `Strategy.OneOf(stratA, stratB)` | |
| `st.builds(MyClass, ...)` | `[Arbitrary]` source generator | Compile-time code generation; see [Source Generators](how-to/use-source-generators.md) |

## Combinators

Hypothesis uses method names; Conjecture uses LINQ conventions:

| Hypothesis | Conjecture | LINQ equivalent |
|---|---|---|
| `strategy.map(f)` | `strategy.Select(f)` | `select` clause |
| `strategy.filter(f)` | `strategy.Where(f)` | `where` clause |
| `strategy.flatmap(f)` | `strategy.SelectMany(f)` | `from ... in ...` |
| — | `strategy.Zip(other)` | Pairs two strategies |
| — | `strategy.OrNull()` | Wraps `T` in `T?` |
| — | `strategy.WithLabel("name")` | Labels for counterexample output |

### LINQ Query Syntax

Conjecture's LINQ support enables query syntax for composing strategies:

```csharp
// Python Hypothesis:
// @st.composite
// def person(draw):
//     name = draw(st.text(min_size=1))
//     age = draw(st.integers(min_value=0, max_value=150))
//     return Person(name, age)

// Conjecture — LINQ query:
var personStrategy =
    from name in Strategy.Strings(minLength: 1, maxLength: 50)
    from age in Strategy.Integers<int>(0, 150)
    select new Person(name, age);

// Conjecture — imperative (like @st.composite):
var personStrategy = Strategy.Compose<Person>(ctx =>
{
    var name = ctx.Generate(Strategy.Strings(minLength: 1, maxLength: 50));
    var age = ctx.Generate(Strategy.Integers<int>(0, 150));
    return new Person(name, age);
});
```

## Settings

| Hypothesis `settings(...)` | `ConjectureSettings` / `[Property]` | Default |
|---|---|---|
| `max_examples=100` | `MaxExamples = 100` | 100 |
| `database=...` | `Database = true` | `true` |
| `deadline=timedelta(ms=200)` | `DeadlineMs = 200` | no deadline |
| — | `Seed = 42UL` | random |
| — | `MaxStrategyRejections = 5` | 5 |
| — | `MaxUnsatisfiedRatio = 200` | 200 |
| — | `DatabasePath = ".conjecture/examples/"` | `.conjecture/examples/` |

### Assembly-Level Defaults

Python Hypothesis uses `settings.register_profile()`. In Conjecture, use the assembly-level attribute:

```csharp
// Python:
// settings.register_profile("ci", max_examples=1000)
// settings.load_profile("ci")

// Conjecture:
[assembly: ConjectureSettings(MaxExamples = 1000)]
```

## Parameter Resolution

| Hypothesis | Conjecture | When to use |
|---|---|---|
| `@given(st.integers())` | Automatic from type | Default — just declare parameter type |
| `@given(custom_strategy)` | `[From<MyProvider>]` | Custom `IStrategyProvider<T>` |
| — | `[FromMethod("MethodName")]` | Static factory on test class returns `Strategy<T>` |

## Key Differences

1. **Shrinking is byte-level.** Like Hypothesis, Conjecture uses a byte-stream engine — shrinking operates on the underlying byte buffer, not on the generated values directly. You never write shrink functions.

2. **Strategies are resolved from types.** In Hypothesis you must pass strategies to `@given()`. In Conjecture, `[Property]` auto-resolves strategies for `int`, `string`, `List<T>`, etc. Use `[From<T>]` only when you need a custom strategy.

3. **LINQ instead of `.map`/`.filter`/`.flatmap`.** Conjecture uses C# LINQ conventions, enabling query syntax (`from ... in ... select ...`).

4. **Source generator instead of `st.builds()`.** Mark your type with `[Arbitrary]` and the source generator creates an `IStrategyProvider<T>` at compile time.

5. **No profiles.** Use `[assembly: ConjectureSettings(...)]` for assembly-wide defaults and `[Property(MaxExamples = ...)]` for per-test overrides.
