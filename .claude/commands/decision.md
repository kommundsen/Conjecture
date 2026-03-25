Record an Architecture Decision Record (ADR) in `docs/decisions/`.

## Input

$ARGUMENTS — short title of the decision (e.g., "Use iterative shrinking instead of coroutines")

## Steps

1. List existing ADRs in `docs/decisions/` to determine the next sequence number.
2. Create a new file: `docs/decisions/NNNN-<slugified-title>.md` where NNNN is zero-padded.
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

4. Ask clarifying questions if the context or rationale is unclear before writing.
5. If this decision supersedes a prior ADR, update the old one's status.
