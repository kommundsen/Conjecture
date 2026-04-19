# How Conjecture.Regex works

`Conjecture.Regex` generates strings that match (or do not match) a regular expression. This page
explains the key decisions behind the implementation: why the package uses its own parser, how
generation produces shrinkable output, how lookaround is resolved, and when a filter fallback is used.

## Why a custom parser instead of reflection

.NET's `System.Text.RegularExpressions.Regex` compiles patterns into an internal `RegexTree`
object. Reflecting into `RegexTree` to recover the parse tree at runtime would work — but it
is an internal, unversioned API that has broken between major .NET releases. A strategy package
that reached into BCL internals would silently misgenerate or crash after a runtime upgrade.

Roslyn exposes a `RegexParser` as part of `Microsoft.CodeAnalysis.CSharp.Features`, but pulling
in that dependency would add the full Roslyn analyzer chain to every test project that uses
`Conjecture.Regex` — an unacceptable overhead for what should be a self-contained package.

`Conjecture.Regex` therefore ships its own recursive-descent parser. It produces a stable internal
AST from the pattern string, driven by the same spec as .NET's engine. Because the AST is fully
owned by this package, it remains stable across .NET runtime versions and does not couple to any
analyzer toolchain.

> [!NOTE]
> The AST is an internal implementation detail — it is not part of the public API surface and
> may change between minor versions of `Conjecture.Regex`.

## Why IR-native shrinking works

Conjecture shrinks counterexamples by manipulating the **choice-sequence IR** — the sequence of
byte-level decisions that were made while generating a value — rather than by manipulating the
generated value directly.

For regex generation, this means:

- A quantifier repetition count (`{m,n}`) is drawn as an integer from the IR. The existing
  `IntegerReduction` shrink pass reduces it toward `m` automatically — no regex-specific shrinker
  is needed.
- A character drawn from a class (`[a-f]`) is drawn as an index into the sorted codepoint list.
  The `LexMinimize` pass reduces the index toward zero, which corresponds to shrinking toward the
  smallest codepoint in the class.
- An alternation choice (`a|b|c`) is drawn as a branch index. The pass reduces it toward the
  first branch.

The practical result: a regex counterexample minimises to the shortest string using the simplest
characters in the leftmost alternation branch at the minimum repetition count — exactly the
minimal form a developer wants to read when diagnosing a failure.

This is the reason `Gen.String().Filter(r.IsMatch)` is an inferior alternative: the filter
accepts or rejects strings but cannot steer the byte buffer toward the pattern, so the shrinker
cannot reduce the matching string at all. Shrinking with a filter-only approach produces output
that is only as minimal as the original random sample happened to be.

## How lookaround is resolved by intersection

Lookahead and lookbehind assertions (`(?=…)`, `(?!…)`, `(?<=…)`, `(?<!…)`) constrain a position
in the string without consuming characters. During generation, Conjecture resolves them by
**intersecting their character requirements with the character class of the adjacent node**.

For example, the pattern `\w(?=[a-f])` contains a `\w` node followed by a lookahead requiring
`[a-f]`. At generation time, the engine:

1. Computes the lookahead's required character set: `{a, b, c, d, e, f}`.
2. Intersects it with `\w`'s character class: `{a-z, A-Z, 0-9, _}`.
3. Draws the character from the intersection: `{a, b, c, d, e, f}`.

The lookahead is satisfied by construction — no post-hoc filtering is needed. The drawn character
still participates in the IR and shrinks normally toward `a`.

Negative lookahead (`(?!…)`) works analogously: the excluded set is subtracted from the adjacent
character class before drawing.

## When the filter fallback is used

Not all lookaround patterns can be resolved by intersection:

- A lookbehind that references a capturing group whose width varies across alternation branches
  cannot be statically resolved to a fixed character class.
- A lookahead that references a backreference (`(?=\1)`) depends on a value drawn earlier in the
  IR — the constraint cannot be evaluated until the backreference value is known.
- Variable-width lookbehind (`(?<=a+)`) involves a position scan that is not reducible to a
  single character-class intersection.

In these cases, `Conjecture.Regex` generates strings from the pattern without the lookaround
constraint, then applies a post-hoc `Filter` using the compiled `System.Text.RegularExpressions.Regex`.
The filter budget is shared with any other `Assume` calls in the same property.

> [!WARNING]
> If a pattern combines complex lookaround with a narrow character class, the filter may exhaust
> its budget before finding a satisfying string. The test will then fail with an
> `UnsatisfiedAssumptionException`. Simplify the lookaround or widen the adjacent character class
> to resolve this.

## Unicode categories and the ASCII default

Unicode category escapes (`\p{L}`, `\p{Lu}`, `\d`, `\w`, `\s`, etc.) match codepoint sets that
can span tens of thousands of entries across the BMP. Sampling the full range by default would
produce unreadable counterexamples and slow down shrinking — `\p{L}` would just as often generate
a Tibetan or Arabic letter as a Latin one, making failure reports hard to interpret.

By default, `Conjecture.Regex` samples only the ASCII subset of each category:

| Escape | Default (ASCII) | Full (`UnicodeCoverage.Full`) |
|---|---|---|
| `\p{L}` | `a-z`, `A-Z` | All Unicode letters |
| `\d` | `0-9` | All Unicode decimal digits |
| `\w` | `a-z`, `A-Z`, `0-9`, `_` | All Unicode word characters |
| `\s` | space, tab, `\r`, `\n` | All Unicode whitespace |

Use `RegexGenOptions { UnicodeCategories = UnicodeCoverage.Full }` to opt in to the full range
when the property genuinely depends on non-ASCII behaviour — for example, testing a Unicode
normalisation pipeline or a multilingual text classifier.

## See also

- [How to test a regex validator](../how-to/test-regex-validator.md)
- [Reference: Regex strategies](../reference/regex-strategies.md)
- [Understanding shrinking](shrinking.md)
- [ADR-0054: Regex Strategy Design](../../decisions/0054-regex-strategy-design.md)
