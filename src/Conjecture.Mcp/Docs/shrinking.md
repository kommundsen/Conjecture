# Conjecture Shrinking Explained

## What Is Shrinking?

When Conjecture finds a failing input, it automatically tries to simplify it to the **smallest input** that still causes the failure. This is called shrinking.

Instead of seeing a failure with `x = -847362, list = [5, 2, 99, 3, ...]`, you see `x = 1, list = [0]` — the minimal case that isolates the bug.

## How Shrinking Works

Conjecture uses a multi-tier shrinking algorithm:

**Tier 0 — Structural reduction:**
- Delete zero-valued blocks
- Delete contiguous blocks
- Delete intervals
- Shrink command sequences (for state machines)

**Tier 1 — Lexicographic minimization:**
- Reduce each integer value toward zero

**Tier 2 — Adaptive reduction:**
- Try to replace values with smaller constants at type-specific boundaries

Each tier runs to fixpoint before the next tier begins. The process is fast because shrinking replays the original buffer with modified values rather than generating new random inputs.

## Reading Shrink Output

```
Falsifying example found after 47 examples (shrunk 23 times)
x = -847362
list = [5, 2, 99, 3, 7]
Minimal counterexample:
x = 1
list = [0]
Reproduce with: [Property(Seed = 0xABCD1234)]
```

| Field | Meaning |
|-------|---------|
| `47 examples` | How many random draws before a failure was found |
| `shrunk 23 times` | How many successful simplification steps were applied |
| Original values | The first input that caused failure |
| Minimal counterexample | The simplest input Conjecture could find |
| `Seed = 0x...` | Replay this exact sequence deterministically |

## Shrinking and Custom Strategies

Shrinking works through the IR (Intermediate Representation) layer — it manipulates the underlying integer buffer, not the generated values directly. This means custom strategies built with `Generate.Compose`, `.Select()`, `.Where()`, etc. shrink automatically without any extra work.

**Exception:** `Strategy<T>.Where(pred)` with a tight filter can make shrinking less effective because many shrunk candidates are rejected. Prefer `Assume.That()` inside `Generate.Compose` or structure the strategy to avoid needing heavy filtering.

## Reproducing Failures

```csharp
// Paste the seed from the output:
[Property(Seed = 0xABCD1234)]
public void MyTest(int x, List<int> list) { ... }
```

The example database also persists the seed — the failure will be retried first on every future run automatically.
