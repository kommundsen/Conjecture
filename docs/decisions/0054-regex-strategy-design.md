# 0054. Regex Strategy Design

**Date:** 2026-04-19
**Status:** Accepted

## Context

Property-based tests frequently need to generate strings that either match or violate a regular expression: validation code, parsers, sanitisers, protocol frame readers, and data-classification rules all specify their input grammar via regex. Without first-class `Gen.Matching` / `Gen.NotMatching` strategies, users resort to `Gen.String().Filter(r.IsMatch)`, which wastes budget and shrinks poorly.

The design question is how to produce a `Strategy<string>` from a regex pattern while preserving Conjecture's IR-native shrinking, NativeAOT/trim compatibility, and stability across .NET runtimes.

Constraints:
- .NET's `System.Text.RegularExpressions.RegexTree` is internal and has broken between runtime versions; reflecting into it ties us to a private API.
- Roslyn's `RegexParser` (from `Microsoft.CodeAnalysis.CSharp.Features`) pulls in a heavy analyzer dependency and is not intended as a public parser.
- Conjecture shrinks by manipulating the choice-sequence IR, not the generated value. A regex strategy that emits opaque strings would be un-shrinkable.
- `RegexOptions` (`IgnoreCase`, `Singleline`, `Multiline`) change the language of the pattern; a generator that ignores them would produce inputs the SUT rejects (or silently mismatches).
- `\p{L}` and other Unicode category escapes have very large codepoint ranges; naively sampling the full range blows out shrink targets and string size without a user opt-in.

## Decision

Ship `Conjecture.Regex` with its own recursive-descent parser producing a stable internal AST, then drive generation from that AST through ordinary choice-sequence IR primitives.

**Parser**
- Own recursive-descent re-parser — no reliance on `RegexTree` reflection or Roslyn.
- Scope: .NET regex flavour plus common PCRE extensions — lookaround (`(?=…)`, `(?!…)`, `(?<=…)`, `(?<!…)`), conditionals (`(?(cond)yes|no)`), named groups, backreferences.
- Explicitly excludes possessive quantifiers (`*+`, `++`, `?+`) — not supported by .NET's engine, no corresponding match semantics to generate against.

**API surface**
- `Gen.Matching(string pattern)` and `Gen.Matching(Regex regex, RegexGenOptions? options = null)`.
- Symmetric `Gen.NotMatching(...)` overloads.
- `RegexGenOptions` carries per-strategy knobs (Unicode coverage, lookaround resolution mode, size bounds).
- `RegexOptions` is tracked intrinsically by the parser: `IgnoreCase` produces mixed-case emitters for literal chars, `Singleline` makes `.` include `\n`, `Multiline` treats `^`/`$` as line anchors.

**Unicode**
- Default: ASCII subset of each Unicode category (e.g. `\p{L}` ⇒ ASCII letters). Keeps generated strings small, shrinkable, and human-readable.
- Opt-in: `RegexGenOptions { UnicodeCategories = UnicodeCoverage.Full }` samples the full Unicode range for category escapes.

**Lookaround**
- Resolved properly via char-requirement intersection at generation time: a lookahead `(?=[a-f])` attached to a `\w` node narrows that node's char class to `[a-f]` before drawing.
- Unresolvable cases (e.g. lookaround referencing backreferences, variable-width lookbehind over alternations) fall back to post-hoc `Filter` against the compiled `Regex`, bounded by the standard filter budget.

**Shrinking**
- IR-native. No new `IShrinkPass` — quantifier counts and character-class indices are plain choice-sequence draws, so existing passes shrink them automatically.
- Quantifiers shrink toward their minimum repetition count; characters shrink toward the minimum codepoint of their resolved class.

**Explicitly deferred**
- ReDoS / adversarial timing generation → v0.15.0 (#366). Warrants its own ADR covering timing semantics, cancellation, and synthesis heuristics.
- `RegexEquivalence` (pattern-level equivalence checking) → future issue.

## Consequences

Positive:
- Stable: no private-API reflection, survives runtime upgrades.
- Shrinks meaningfully — counter-examples minimise to the shortest string in the simplest char class that still matches.
- Trim/AOT safe — no dynamic `RegexTree` walking, no code generation.
- `RegexOptions` handled intrinsically means users can pass a compiled `Regex` from their SUT and get generation matching what the SUT actually accepts.
- ASCII-by-default Unicode keeps failure reports readable; full coverage available when the property genuinely depends on non-ASCII behaviour.

Negative:
- Maintenance cost: we own a regex parser and must track .NET regex syntax additions.
- Feature drift risk: a future .NET release could add syntax our parser doesn't recognise. Mitigated by the `Regex` overload — the SUT's compiled pattern still runs through .NET's engine for validation in tests.
- Some lookaround patterns degrade to filter-based fallback, which can blow filter budget on pathological inputs.

## Alternatives Considered

**Fare (https://github.com/moodmosaic/Fare)** — MIT, Xeger / dk.brics.automaton port. Evaluated and rejected as a dependency:
- No shrinking integration — emits opaque strings, defeating Conjecture's IR-native counter-example minimisation.
- Regular languages only — cannot represent lookaround, backreferences, or conditionals.
- Not `RegexOptions`-aware (no intrinsic `IgnoreCase`/`Multiline`/`Singleline` handling).
- Unmaintained since ~2019; 3rd-hand port (Java → C# → fork), raising long-term risk.
- License compatibility (MIT → MPL-2.0) is clean, so licensing is not a blocker — rejection is on technical grounds.

Fare's source remains a useful reference for char-class sampling and `\d` / `\w` / `\s` semantics, and will be consulted during implementation without being vendored or linked.

**Reflecting into `System.Text.RegularExpressions.RegexTree`** — rejected: internal API, has broken between .NET versions, tight coupling to BCL internals is unacceptable for a foundational testing library.

**Reusing Roslyn's `RegexParser`** — rejected: heavy analyzer dependency chain for what should be a self-contained strategy package; API is not intended for reuse.

**`Gen.String().Filter(r.IsMatch)`** — the status quo. Rejected as the target solution: burns filter budget, produces no meaningful shrinks (the filter just accepts or rejects; it cannot steer the string toward the pattern).
