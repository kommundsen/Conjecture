# 0059. Conjecture.Interactions + Conjecture.Http — transport-agnostic interaction foundation

**Date:** 2026-04-23
**Status:** Accepted

## Context

v0.20 (AspNetCore) and v0.22 (Aspire) each defined their own request/interaction type and state-machine vocabulary. That forced users into a paradigm jump between a stateless single-host ASP.NET Core workflow and a stateful `AspireStateMachine` workflow, even though the property-testing semantics are identical: drive some transport target, assert invariants.

Two orthogonal axes were conflated:

- **Host**: single `IHost` vs. `DistributedApplication` vs. pre-Aspire multi-start-project — a name→target map.
- **Model**: single request vs. sequence (state machine) — orthogonal to host.

Downstream milestones (v0.24 AspNetCore, v0.25 Aspire, v0.26 Messaging, v0.27 gRPC) all need shared primitives before they can be built consistently.

## Decision

Ship a two-layer foundation:

### Layer 1 — `Conjecture.Interactions` (transport-agnostic)

- `IInteraction` — empty marker interface. Typed payloads (HTTP request, gRPC call, message) implement it.
- `IInteractionTarget` — single dispatch hook: `Task<object?> ExecuteAsync(IInteraction interaction, CancellationToken ct)`. **CancellationToken is a hard contract**: all implementations must honour it. Attribute adapters source it natively from the test runner; Interactive notebooks source it from the kernel; LinqPad from `QueryCancelToken`.
- `InteractionStateMachine<TState> : IStateMachine<TState, IInteraction>` — reuses `CommandSequenceShrinkPass` from `Conjecture.Core`. No new shrinking machinery.
- `CompositeInteractionTarget` — wraps `IEnumerable<(string name, IInteractionTarget)>`. Dispatches by resource name. Used by multi-host setups (pre-Aspire and Aspire alike share the same shape).
- `Property.ForAll(target, strategy, assertion, settings, ct)` imperative overloads in `Conjecture.Core` — the root primitive every adapter wraps. Makes Interactive notebooks and LinqPad first-class citizens without requiring an adapter.

### Layer 2 — `Conjecture.Http` (HTTP concretion)

- `HttpInteraction(string ResourceName, HttpMethod Method, string Path, HttpContent? Body, IReadOnlyDictionary<string, string> Headers)` — immutable record.
- `IHttpTarget : IInteractionTarget` — extends the contract with `HttpClient ResolveClient(string resourceName)`.
- `HttpInvariantExtensions` — `.AssertNot5xx()`, `.Assert4xx()`, `.AssertProblemDetailsShape()` assertion helpers.
- `Generate.Http` builder — fluent API returning `Strategy<HttpInteraction>`. Methods: `.Get/.Post/.Put/.Delete/.Patch/.WithHeaders/.WithResource/.WithBodyStrategy`.
- `HostHttpTarget : IHttpTarget` — wraps `IHost` + `HttpClient` factory; supports async disposal. Consumed by v0.24 AspNetCore and v0.25 Aspire (and any pre-Aspire multi-host solution).

### Deferred per-adapter fixture wiring

`ConjectureFixtureBase` seam plugging into each adapter's native lifecycle defers to the host milestones (v0.24 / v0.25). No fixture scaffolding ships in this milestone.

### Layer 1 stays minimal

No SignalR, WebSocket, gRPC, or message-queue `IInteraction` subtypes ship here. The abstractions are intentionally thin until at least one non-HTTP transport is actively built, at which point `IInteractionTarget` and `IInteraction` can be reviewed for necessary extension points.

## Consequences

**Easier:**
- Swapping host type (single `IHost` → Aspire) is a one-line change to the target reference.
- Swapping model (stateless → state-machine) is independent of host choice.
- Interactive notebooks and LinqPad require no adapter infrastructure.
- All downstream host packages share shrinking logic via `CommandSequenceShrinkPass`.

**Harder:**
- `IInteraction` is a marker — runtime dispatch is by convention (`ResourceName`), not by type. Incorrect resource names produce runtime errors rather than compile errors.
- `CompositeInteractionTarget` requires every interaction to carry a `ResourceName`; single-target scenarios pay a small naming tax.

**Risks:**
- Layer 1 may need extension points when a second transport ships. The minimal-until-needed policy means `IInteractionTarget` could require a breaking change if its contract turns out to be insufficient.

## Alternatives Considered

**Single unified package** — merging Interactions and Http into one package. Rejected: forces consumers who only need the transport-agnostic primitives (e.g. Messaging, gRPC) to take an HTTP dependency.

**Generic `IInteractionTarget<TResult>`** — typed result avoids `object?`. Rejected: state-machine shrinking requires a uniform command/result shape; introducing a type parameter complicates `InteractionStateMachine` and `CompositeInteractionTarget` without a concrete benefit at this stage.

**Keep per-adapter state machines** — continue with `AspireStateMachine` and a hypothetical `AspNetCoreStateMachine`. Rejected: duplicates the `CommandSequenceShrinkPass` wiring and locks users into a paradigm based on host type rather than test model.
