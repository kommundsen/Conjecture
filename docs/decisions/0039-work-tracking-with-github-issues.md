# 0039. Work Tracking with GitHub Issues

**Date:** 2026-04-06
**Status:** Accepted

## Context

Through Phase 7, all implementation planning was tracked in `docs/plans/` as structured markdown files: eight phase files (IMPLEMENTATION-PLAN-PHASE-{0-7}.md) and twenty exploratory draft files (IMPLEMENTATION-PLAN-DRAFT-*.md). This worked well for a single developer working through a linear sequence of TDD cycles with no external contributors.

With Phase 7 complete and the project approaching public release, two things change:

1. Future work is no longer a linear sequence of phases — it is a backlog of independent, unordered features that will be prioritised and scheduled over time.
2. GitHub Issues provides features (cross-references from commits/PRs, milestone progress tracking, label-based filtering, notifications) that markdown files cannot replicate and that become more valuable as the project gains external visibility.

## Decision

GitHub Issues is the canonical work tracking tool for all development from Phase 8 onward. The structure follows GitHub-native conventions:

| Planning concept | GitHub equivalent |
|---|---|
| Capability area | **Tracking issue** (label: `tracking`) — parent issue with task list linking child enhancements |
| Draft feature / planned feature | **Enhancement issue** (label: `enhancement`) — one issue per draft file |
| Implementation unit (sub-phase) | **Enhancement issue** assigned to a milestone, created when the parent feature is scheduled |
| Individual test/implement step | **Checklist item** inside an implementation issue |
| Release / iteration | **Milestone** (`Backlog`, `v0.7.0`, etc.) |

**Label taxonomy (initial):**

- Type: `tracking`, `enhancement`, `bug`, `needs-adr`
- Area: `area:strategies`, `area:shrinking`, `area:engine`, `area:generators`, `area:analyzers`, `area:fsharp`, `area:release` (grow as needed)
- Effort: `effort:small`, `effort:medium`, `effort:large`, `effort:xl`
- Status: `blocked`, `in-progress`

**Lifecycle:** Draft features start in the `Backlog` milestone as unscheduled enhancements. When a feature is accepted for a release, it moves to the target milestone and is decomposed into implementation issues. Implementation issues are created on-demand — not upfront for the entire backlog.

**What does not change:**

- `docs/plans/` is frozen as historical record for Phases 0–7. No new phase files will be added.
- `docs/decisions/` ADRs are unchanged. Issues link to ADR files via relative markdown links.
- Slash commands (`/test`, `/implement-cycle`, `/decision`, etc.) continue to be used identically during implementation.

## Consequences

**Easier:**
- Discoverability for contributors: label and milestone filters surface relevant work without knowledge of file structure.
- Progress visibility: milestone completion percentages and task list check-off are visible on the repository home page.
- Traceability: commits and PRs can reference issues with `#N`; the full context chain (issue → PR → commit) is navigable from GitHub.
- Backlog triage: effort and area labels make it straightforward to compare and sequence unscheduled features.

**Harder / trade-offs:**
- The rich contextual content in draft files (dependency notes, bash verification commands, constraint lists) must be manually migrated into issue bodies; it is not automatic.
- Issue body edits have no PR-style review workflow; plan changes are less auditable than markdown file diffs.
- `docs/plans/` phase files are no longer the source of truth for future work, which creates a potential for confusion if contributors read both.

## Alternatives Considered

**Continue with markdown files in `docs/plans/`:** Suitable for solo linear development but does not scale to an unordered backlog or external contributions. No cross-referencing, progress tracking, or notifications.

**GitHub Projects board:** Adds a kanban/roadmap view on top of Issues. Deferred — the Issues tab with label filters is sufficient for a solo project at this stage. Can be added later without any structural changes.

**Linear or similar tools:** Introduces a dependency on an external paid service. GitHub Issues keeps all tracking co-located with the code and is free for open source.
