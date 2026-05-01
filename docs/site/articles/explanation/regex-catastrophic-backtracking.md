# Why nested quantifiers cause catastrophic backtracking

Catastrophic backtracking is a class of ReDoS (Regular Expression Denial of Service)
vulnerability where a regex engine takes exponential time on certain inputs. Understanding why
it happens explains why `Strategy.ReDoSHunter` generates the strings it does.

## How backtracking engines work

Most regex engines — including .NET's default NFA engine — work by trying a path through the
pattern and backtracking if it fails. For simple patterns this is fast, but for patterns with
ambiguous repetition the engine must explore exponentially many paths before it can declare no
match.

## The nested-quantifier case

Consider the pattern `(a+)+$` and the input `"aaaa!"`.

The outer `+` repeats the group `(a+)` one or more times. The inner `a+` matches one or more
`a`s. Together they can match `"aaaa"` in many different ways:

| Outer iterations | Inner lengths |
|---|---|
| 1 group | `[4]` |
| 2 groups | `[1,3]`, `[2,2]`, `[3,1]` |
| 3 groups | `[1,1,2]`, `[1,2,1]`, `[2,1,1]` |
| 4 groups | `[1,1,1,1]` |

For `n` input characters there are 2^(n-1) ways to partition them. When the engine reaches `!`
and the `$` anchor fails, it must try every partition before concluding the string does not
match — 2^(n-1) attempts in total.

With n=30 (thirty `a`s followed by `!`), that is over 500 million backtrack steps. On a modern
CPU this takes several seconds to minutes; with n=50 the heat death of the universe is a more
realistic estimate.

## The alternation case

Ambiguous alternation has the same effect. For `(a|aa)+$` with input `"aaa!"`:

The engine can match each character as either `a` (single) or the first character of `aa`
(pair). The number of ways grows with the Fibonacci sequence — exponential growth, though slower
than the pure-quantifier case.

## Why property-based testing finds these faster than manual audit

Manual auditing requires the developer to construct the worst-case input by hand. For complex
patterns this is non-trivial — the pathological input depends on the specific ambiguity structure
of the pattern, which is hard to reason about directly.

`Strategy.ReDoSHunter` automates this by:

1. **Walking the AST** to find nested-quantifier sub-trees.
2. **Biasing repetition counts** toward their maximum, generating longer strings with more
   ambiguous paths.
3. **Appending a non-matching suffix** (`\0`) to force the engine to exhaust all backtracking
   paths rather than returning on first match.
4. **Feeding timing signals** back to Conjecture's hill-climbing loop, steering the engine toward
   progressively slower inputs.
5. **Shrinking** via the standard IR-native passes, converging to the shortest string that still
   triggers the slowdown.

The result is a minimal, reproducible pathological input — typically found in tens of seconds,
not hours of manual analysis.

## How to protect against catastrophic backtracking

| Fix | When to use |
|---|---|
| `RegexOptions.NonBacktracking` | Drop-in replacement for patterns where full NFA semantics are not required. The NFA engine cannot exhibit catastrophic backtracking by design. |
| Possessive quantifiers / atomic groups | Prevent the engine from reconsidering already-matched characters. Useful when `NonBacktracking` changes match semantics. |
| Rewrite the pattern | Remove the ambiguity. `(a+)+$` can be rewritten as `a+$` — single quantifier, no ambiguity. |
| Timeout + retry limit | `MatchTimeout` on the `Regex` instance provides a safety net but is not a fix — it just limits the damage. |

## See also

- [How to audit a regex for catastrophic backtracking](../how-to/audit-regex-for-redos.md)
- [Reference: `Strategy.ReDoSHunter`](../reference/regex-strategies.md#strategyredoshunter)
- [How Conjecture.Regex works](regex-engine.md)
