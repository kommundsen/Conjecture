# Understanding property-based testing

## The limits of example-based tests

A unit test checks a specific scenario:

```csharp
[Fact]
public void Sort_empty_list_returns_empty()
{
    Assert.Equal(new List<int>(), MySort.Sort(new List<int>()));
}

[Fact]
public void Sort_single_element_returns_same()
{
    Assert.Equal(new List<int> { 1 }, MySort.Sort(new List<int> { 1 }));
}
```

Each test is a claim about *one particular input*. No matter how many examples you write, you're choosing from the infinite space of possible inputs — and the ones you don't think of are the ones that contain bugs.

## Properties describe invariants

A property describes something that must be true for *all* inputs, not just selected examples:

```csharp
[Property]
public bool Sort_preserves_length(List<int> items)
{
    return MySort.Sort(items).Count == items.Count;
}

[Property]
public bool Sort_output_is_ordered(List<int> items)
{
    List<int> sorted = MySort.Sort(items);
    for (int i = 1; i < sorted.Count; i++)
    {
        if (sorted[i] < sorted[i - 1]) return false;
    }
    return true;
}
```

Conjecture generates hundreds of lists and checks both properties for each one. The inputs you'd never think to try — null-containing lists, duplicate values, lists with only negative numbers — are generated automatically.

## Why generated inputs find bugs that examples miss

When you write an example, you write what you *believe* should work. The edge cases that contain bugs are, by definition, the ones you haven't considered. Random generation has no assumptions to violate — it explores the input space systematically, including regions your intuition overlooked.

This is especially powerful for:
- **Boundary conditions** — values just above or below valid ranges
- **Interaction effects** — combinations of values that individually look fine but interact badly
- **Empty inputs and degenerate cases** — empty strings, zero-length lists, null values
- **Large inputs** — performance-dependent bugs that only appear at scale

## Shrinking makes failures actionable

The counterexample to a property might be a 500-element list when a 2-element list would expose the same bug. Without minimization, property-based testing would produce unreadable failures.

Conjecture automatically **shrinks** every counterexample: after finding a failing input, it searches for a simpler version of the same failure. The result is the minimal input that still triggers the bug. A 500-element list becomes `[2, 1]`. A 200-character string becomes `"ab"`. A complex object becomes one with all fields at their simplest values.

This happens without any code from you — see [Understanding shrinking](shrinking.md) for how it works.

## The Conjecture approach

Conjecture is a direct port of the [Hypothesis](https://hypothesis.works/) engine for Python. Several design choices distinguish it from earlier property testing frameworks:

**Strategies, not type classes.** Earlier frameworks like QuickCheck and FsCheck derive generators from type information (`Arbitrary<T>` type classes). Conjecture uses explicit `Strategy<T>` values — you compose them with LINQ, pass them to parameters, and build them from simpler parts. The tradeoff: more explicit, but also more flexible and easier to understand.

**Byte-stream-backed generation.** Every generated value is backed by a raw byte buffer. The same buffer, replayed through the same strategy, always produces the same value. This is what makes shrinking universal — see [Understanding shrinking](shrinking.md).

**Example database.** When a test fails, Conjecture saves the byte buffer to a SQLite database. Future runs replay stored failures before generating new ones. Once a bug is found, it's checked on every run until the test passes. See [Understanding the example database](example-database.md).

**No shrink functions.** In QuickCheck and its descendants, every generator comes with a `shrink` function that produces smaller values. In Conjecture, shrinking operates on the underlying byte buffer — any value that can be generated can be shrunk, without any per-type code.

## When to use property-based testing

Property tests are most valuable for:
- **Pure functions** — sorting, parsing, encoding, serialization, mathematical operations
- **Invariant preservation** — "the count doesn't change", "the output is sorted", "the result round-trips"
- **Oracle testing** — comparing a new implementation against a reference implementation
- **Stateful systems** — sequences of operations on queues, caches, domain objects (see [How to test stateful systems](../how-to/test-stateful-systems.md))

They are less suited for:
- **Side effects that are expensive to reverse** — database operations, network calls
- **Tests that require exact outputs** — snapshot tests, output formatting
- **Very narrow properties** — "this specific method returns 42" is a fact, not a property

## Further reading

- [Quick Start](../quick-start.md) — write your first property test
- [Tutorial 1](../tutorials/01-your-first-property-test.md) — step-by-step introduction
- [Understanding shrinking](shrinking.md) — how Conjecture finds minimal counterexamples
- [Porting Guide](../porting-guide.md) — coming from Python Hypothesis
