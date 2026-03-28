---
name: decision
description: >
  Record an Architecture Decision Record (ADR) in `docs/decisions/` for the Conjecture project.
  Use this skill whenever the user wants to document a design choice, record why something was built a certain way, capture a tradeoff, or note a decision that constrains future work — even if they don't say "ADR" explicitly.
  Triggers on phrases like "record this decision", "document why we chose", "log the architecture decision", "we decided to use X", or when the implement skill identifies a design choice that should be captured first.
---

Record an Architecture Decision Record (ADR) in `docs/decisions/`.

## Input

Short title of the decision, e.g. `Use iterative shrinking instead of coroutines`.

## Steps

1. List existing ADRs in `docs/decisions/` to determine the next sequence number.
2. Create `docs/decisions/NNNN-<slugified-title>.md` where NNNN is zero-padded.
3. Use this template:

```markdown
# NNNN. <Title>

**Date:** <today>
**Status:** Accepted | Proposed | Superseded by NNNN

## Context

<What is the issue? Why does this decision need to be made? What constraints exist?>

## Decision

<What was decided and why?>

## Consequences

<What are the trade-offs? What becomes easier or harder?>

## Alternatives Considered

<What other options were evaluated?>
```

4. Ask clarifying questions if context or rationale is unclear before writing.
5. If this decision supersedes a prior ADR, update the old one's **Status** to `Superseded by NNNN`.
