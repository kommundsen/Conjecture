# Understanding targeted testing

## The limits of pure random generation

Random generation explores the input space uniformly. For most properties, uniform exploration is exactly what you want — bugs distributed across the input space will be found proportionally to their frequency.

But some bugs only appear at extremes. A sorting algorithm might fail only on lists with more than 10,000 elements. A compression library might fail only when a specific structural pattern appears deep in a large input. An O(n²) algorithm might look fine at 100 elements but time out at 10,000.

Pure random generation will eventually find these bugs, but "eventually" might mean millions of examples. Targeted testing accelerates this by giving Conjecture a signal to steer toward the interesting region.

## The two-phase approach

Targeted testing in Conjecture runs in two phases within the same test run:

**Generation phase** — the engine generates `(1 − TargetingProportion) × MaxExamples` random examples. For each example, it records the score you report alongside the byte buffer that produced it. The highest-scoring buffer per label is retained.

**Hill-climbing phase** — for each label, the engine takes the best buffer from the generation phase and mutates it. It tries incrementing, decrementing, and binary-searching each integer-like byte in the buffer toward better scores. Random perturbations help escape local maxima.

The two phases together use at most `MaxExamples` test cases. The default split is 50/50.

## Scores are hints, not constraints

Scores are purely advisory. A score tells the engine "try to find inputs where this number is larger" — but the engine is free to ignore a mutation that improves the score if it can find a more interesting direction.

Critically, observations have **no effect on shrinking**. If a targeted run finds a failure, the counterexample is shrunk exactly as if it had been found by random generation. The shrunk result is the minimal failing input, not the input with the maximum score.

## When targeting helps

Targeted testing is most effective when:

- The property you care about has a **measurable scalar signal** that correlates with bug likelihood (list length, tree depth, number of distinct keys)
- The bug is **rare under uniform sampling** but common in a specific region
- You want to **stress-test** a system under adversarial load patterns

It adds the most value for:
- Finding performance bugs (time out at large inputs)
- Testing parsers and evaluators on deeply nested inputs
- Verifying algorithms that have known bad worst cases

It adds little value for:
- Properties where any random input is equally likely to fail
- Properties with no obvious scalar signal
- Very fast properties with high `MaxExamples` (uniform exploration is already thorough)

## The hill-climbing tradeoff

Hill climbing is a local search. It can get stuck in local maxima — regions where every mutation decreases the score, but the global maximum is elsewhere. Conjecture addresses this with random restarts and perturbations, but cannot guarantee it always finds the global maximum.

This is acceptable because the goal is not to find the *highest possible* score — it's to find a *high-enough* score that exposes a bug. In practice, hill climbing reliably reaches interesting regions even when it doesn't find the theoretical maximum.

## Multiple labels

When you report multiple scores with different labels:

```csharp
Target.Maximize(NodeCount(graph), "nodes");
Target.Maximize(EdgeCount(graph), "edges");
```

The targeting budget is divided evenly across all labels. Each label runs its own independent hill-climbing phase. This means you get good exploration in each dimension at the cost of fewer examples per dimension.

For tightly coupled dimensions (e.g., a graph where more nodes implies more edges), a single composite score often works better than two independent labels.

## See also

- [How to use targeted testing](../how-to/use-targeted-testing.md) — practical usage
- [Reference: Settings](../reference/settings.md) — `Targeting`, `TargetingProportion`
- [How to test stateful systems](../how-to/test-stateful-systems.md) — related technique for sequence-dependent bugs
