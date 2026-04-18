# 0052. F# Wrapper Implementation Architecture

**Date:** 2026-04-18
**Status:** Accepted

## Context

ADR 0013 decided to ship a separate `Conjecture.FSharp` package with an idiomatic F# API. This ADR records the concrete implementation decisions: how `Gen<'a>` is represented, how computation expressions surface `IGeneratorContext`, how auto-generation handles F# types, and how the optional Expecto adapter is packaged.

Several constraints shape the design:

- The core engine is trim-safe and NativeAOT-compatible; the F# wrapper may use reflection where justified but must not drag those dependencies into Core.
- F# users expect discriminated unions and records to be generatable without manual registration.
- Counterexample output must use F# `%A` formatting so DU/record values are printed idiomatically.
- `IGeneratorContext` is a C# concept that should not surface in F# user code.
- The Expecto test framework has its own runner lifecycle; integration must be a separate package.

## Decision

**`Gen<'a>` type.** Defined as a `[<Struct>]` single-case discriminated union wrapping `Strategy<'a>`:

```fsharp
[<Struct>] type Gen<'a> = Gen of Strategy<'a>
```

A struct DU (not a type alias) gives the F# type system a distinct `Gen<'a>` that can carry a `Gen` module of curried functions while remaining zero-overhead at runtime. A type alias would prevent defining a module with the same name.

**Dependency boundary.** `Conjecture.FSharp` depends only on `Conjecture.Core`. It does not reference any framework adapter (`Conjecture.Xunit`, `Conjecture.NUnit`, etc.). Test-framework wiring is the responsibility of the adapter packages.

**Namespace and open.** A top-level `Conjecture` module re-exports the most commonly used bindings, so `open Conjecture` is the single import needed for typical usage. Framework-specific bindings (e.g., Expecto `property`) are in their own module under the adapter package.

**`gen { }` computation expression.** A `GenBuilder` CE hides `IGeneratorContext` from user code. `let!` inside the expression calls `Gen.sample context gen` internally; users never see `IGeneratorContext` directly.

**Property styles.** The `Property.check` entry point (and the Expecto `property` combinator) accepts both `'a -> bool` (predicate) and `'a -> unit` (assertion, throws on failure). This matches FsCheck conventions and lets users use assertion libraries directly.

**Counterexample formatting.** Counterexample values are formatted with `sprintf "%A"` rather than `.ToString()`. This produces idiomatic F# output for DUs, records, and lists.

**`Gen.auto<'a>`.** Auto-generation for records and discriminated unions uses `FSharp.Reflection` at runtime and is annotated `[<RequiresUnreferencedCode>]`. This keeps the main `Gen` module trim-safe; users who need auto-generation opt in explicitly and accept the annotation.

**Shrinking.** No F#-specific shrink logic is needed. The Core shrinker operates on the byte stream; F# structural equality is not involved in shrinking, so Core's behaviour is correct as-is.

**Expecto adapter.** `Conjecture.FSharp.Expecto` is a separate NuGet package with a single `property` combinator that wraps `Property.check` and registers the result as an Expecto `Test`. This keeps the main F# package free of the Expecto dependency.

## Consequences

- F# users get a zero-overhead `Gen<'a>` type with no boxing; the struct DU is erased to `Strategy<'a>` at the IL level.
- `Gen.auto` is opt-in and clearly annotated; NativeAOT builds that exclude it remain trimmer-safe.
- Users who open only `Conjecture` get a clean, uncluttered namespace without framework noise.
- Two additional packages to publish and version (`Conjecture.FSharp`, `Conjecture.FSharp.Expecto`).
- `IGeneratorContext` CE binding must be maintained in sync with any future Core changes to the context interface.
- `sprintf "%A"` formatting is slower than `.ToString()` but formatting only occurs on failure, so the cost is acceptable.

## Alternatives Considered

- **Type alias `type Gen<'a> = Strategy<'a>`**: Simpler but prevents a `Gen` module from existing at the same level; the C# class would shadow it.
- **Class-based `Gen<'a>`**: Avoids struct limitations but adds allocation; struct DU is zero-cost and idiomatic for lightweight wrappers.
- **Expose `IGeneratorContext` in F# API**: More flexible but leaks a C#-centric abstraction; the CE approach keeps the surface clean.
- **Compile-time auto-generation via source generator**: Trim-safe but complex; deferred until demand justifies it — reflection is sufficient for v1.
- **Bundle Expecto adapter in `Conjecture.FSharp`**: Reduces package count but forces an Expecto dependency on users who use a different F# test framework.
