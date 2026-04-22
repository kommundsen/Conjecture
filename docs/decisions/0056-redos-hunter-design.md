# 0056. ReDoSHunter: Adversarial Regex String Generation

**Date:** 2026-04-22
**Status:** Accepted

## Context

ReDoS (Regular Expression Denial of Service) vulnerabilities arise when a regex with nested quantifiers or ambiguous alternation takes exponential time on certain inputs. Detecting these at test time requires a strategy that actively synthesises strings likely to trigger catastrophic backtracking — not just random strings that happen to match.

Conjecture already has `Generate.Matching` (and `NotMatching`) in `Conjecture.Regex`. The challenge is to extend this with an *adversarial* variant: `Generate.ReDoS`, which targets slow execution via the engine-level timeout and hill-climbs elapsed time as a fitness signal.

Key constraints:
- Must integrate with Conjecture's existing targeting (`ctx.Target`) and IR-native shrinking.
- The `RegexNode` AST is available internally (via `System.Text.RegularExpressions.RegexParser` internals or a hand-rolled walker), enabling guided synthesis.
- False positives from a single slow run are unacceptable; confirmation logic is required.
- Users should not need to configure timeout values or retry counts — the API must stay simple.

## Decision

### Timing

Use a dual-signal approach:
- **`Regex.MatchTimeout`** (engine-level): set to a fixed internal threshold (e.g. 200 ms). A `RegexMatchTimeoutException` is the hard failure predicate — it means the engine gave up.
- **`Stopwatch`**: measure wall time for each `Regex.IsMatch` call and feed `ctx.Target(elapsedMs)` to drive hill-climbing toward longer-running inputs.

This pairs the engine's own safety valve with Conjecture's targeting loop, letting the shrinker find the *minimal* catastrophic input rather than just any slow one.

### Cancellation and Confirmation

A single timeout hit is not sufficient to declare a ReDoS — noisy environments produce spurious timeouts. The strategy will:
1. Retry up to **3 times** on any candidate that triggers a timeout.
2. Require **≥ 2 confirmed timeouts** on the same candidate before treating it as a genuine failure.

These thresholds are internal constants, not exposed as user-configurable parameters. Exposing them would add API surface without meaningful benefit — the values are informed by empirical calibration against known ReDoS patterns.

### Synthesis: AST-Guided Targeting

The strategy walks the compiled `RegexNode` tree looking for **nested-quantifier sub-trees** (e.g. `(a+)+`, `(a|aa)*`). When found:

- Repetition-count draws for those nodes are **biased toward the maximum** — using `Generate.Integers(min, max).Select(x => max - (max - x) / 4)` or equivalent skewing — to maximise the number of partial-match backtracks.
- Other nodes are generated normally via the existing `RegexNodeStrategy` logic.

When **no nested quantifiers are found**, the strategy falls back to `Generate.Matching` with a diagnostic label (`"redos:no-nested-quantifiers"`) indicating why adversarial synthesis was not possible.

### Shrinking

No custom `IShrinkPass` is needed. IR-native shrinking (the existing `DeleteFixedChunkShrinkPass`, `ShrinkTowardsZeroShrinkPass`, etc.) handles minimisation of the byte buffer that produces a slow input. The targeting signal naturally guides the engine toward minimal catastrophic inputs during the search phase; shrinking then reduces them further.

### Reporting

`ReDoSHunterStrategy` is a `Strategy<string>`. It emits candidate strings. The **user's property** owns the timing assertion — for example:

```csharp
ForAll(Generate.Regex.ReDoS(pattern), input =>
{
    var sw = Stopwatch.StartNew();
    Regex.IsMatch(input, pattern);
    Assert.True(sw.ElapsedMilliseconds < 100, $"Slow on: {input}");
});
```

This keeps the strategy composable and avoids baking a specific time threshold into the library.

### API Shape

Two overloads only, mirroring `Generate.Matching`:

```csharp
Generate.Regex.ReDoS(string pattern, RegexGenOptions? options = null)
Generate.Regex.ReDoS(Regex regex, RegexGenOptions? options = null)
```

No additional parameters. The strategy is surfaced via the existing C# 14 `extension(Generate)` block in `Conjecture.Core.RegexGenerateExtensions`.

### `RegexOptions` Handling

| Option | Behaviour |
|---|---|
| `RegexOptions.NonBacktracking` | Fall back to `Generate.Matching` with diagnostic label `"redos:non-backtracking"`. The NFA engine cannot exhibit catastrophic backtracking by design. |
| `RegexOptions.Compiled` | No special handling needed. Compiled regexes use the same backtracking engine; `MatchTimeout` and `Stopwatch` work identically. |
| Others | Pass through unchanged. |

## Consequences

**Easier:**
- ReDoS candidates are minimal (IR shrinking + targeting).
- No new shrink infrastructure to maintain.
- API is simple and mirrors existing `Generate.Matching` shape — no learning curve.
- `NonBacktracking` fall-through is safe and self-documenting via the diagnostic label.

**Harder:**
- AST walking requires access to `RegexNode` internals (or a hand-rolled pattern walker). This is a maintenance risk if the BCL changes internal types.
- The confirmation logic (3 retries, ≥2 hits) adds latency per candidate — acceptable for a security-focused strategy but notable for large test suites.
- No user control over timeout or retry thresholds. If a team's CI is extremely noisy, they cannot tune the confirmation window without forking.

## Alternatives Considered

**Pure random generation (`Generate.Matching` only):** Finds ReDoS inputs by chance only — impractical for complex patterns where adversarial inputs are rare.

**Custom `IShrinkPass`:** Would allow targeted shrinking of string length directly, but the existing IR-native passes already handle byte-level minimisation effectively. Adding a custom pass would increase complexity for marginal gain.

**Exposing timeout/retry as parameters:** Keeps the API simple. Power users can compose `ReDoSHunterStrategy` manually if they need custom thresholds (the type is public).

**Separate `Conjecture.ReDoS` package:** Overkill — the feature is a natural extension of `Conjecture.Regex` and shares `RegexNodeStrategy` internals already present there.
