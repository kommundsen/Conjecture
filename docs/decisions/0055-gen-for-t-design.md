# 0055. Gen.For<T>() Design — Unified [Arbitrary] Trigger, Registry Lookup, Override DSL

**Date:** 2026-04-20
**Status:** Accepted

## Context

ADR-0029 defined the `[Arbitrary]` source generator that emits `{TypeName}Arbitrary : IStrategyProvider<T>` implementations. ADR-0028 established `IStrategyProvider<T>` and `[From<TProvider>]` as the extension point for parameter-level strategy injection.

A gap remains: there is no ergonomic way to obtain a strategy for a type *by type alone* — without knowing the generated provider name, without annotating every parameter with `[From<TArbitrary>]`, and without hand-wiring lookups. Concretely, users want:

```csharp
Strategy<Order> s = Gen.For<Order>();
```

where `Order` is decorated with `[Arbitrary]`. The design must:

1. Work for any type with an emitted `IStrategyProvider<T>` — not only types the user directly annotated, but also types resolved from external packages whose providers are already compiled.
2. Remain trim-safe and NativeAOT-compatible (ADR-0014): no runtime reflection over assemblies, no `Activator.CreateInstance` in hot paths.
3. Allow attribute-driven constraints (`[GenRange]`, DataAnnotations) to be baked in at generation time.
4. Allow per-call-site overrides without mutating the shared generated strategy.

## Decision

### Unified trigger: `[Arbitrary]` only

`[Arbitrary]` remains the sole opt-in marker (ADR-0029). No new attribute is introduced. `Gen.For<T>()` resolves any `T` whose `{TypeName}Arbitrary` is registered in the runtime registry. The attribute is the authority; the registry is the derived artifact.

### Registry mechanism: `GenForRegistry.g.cs`

A new incremental source generator, **`GenForGenerator`**, runs alongside the existing `ArbitraryGenerator`. For every `[Arbitrary]`-decorated type it finds in the current compilation, it emits a single `GenForRegistry.g.cs` file containing a module initializer:

```csharp
internal static class GenForRegistry
{
    [ModuleInitializer]
    internal static void Register()
    {
        ProviderRegistry.Register<Order>(static () => new OrderArbitrary());
        ProviderRegistry.Register<Address>(static () => new AddressArbitrary());
        // …one entry per [Arbitrary] type in this compilation
    }
}
```

`ProviderRegistry` is a new `internal static` class in `Conjecture.Generators` backed by a `ConcurrentDictionary<Type, Func<object>>`. The module initializer runs once at assembly load, before any test executes.

`Generate.For<T>()` (public API on the `Generate` static class) performs a single dictionary lookup:

```csharp
public static Strategy<T> For<T>() =>
    ProviderRegistry.Resolve<T>()?.Create()
        ?? throw new InvalidOperationException(
            $"No IStrategyProvider<{typeof(T).Name}> registered. " +
            $"Decorate {typeof(T).Name} with [Arbitrary] or register a provider manually.");
```

The lookup is O(1), allocation-free after warm-up, and trim-safe because `typeof(T)` is a compile-time constant in generic-specialised code.

### Cross-assembly providers

Types from external assemblies that are already compiled with `[Arbitrary]` emit their own `GenForRegistry` module initializers in their respective assemblies. Those initializers run when the assembly is loaded, populating the shared `ProviderRegistry` automatically. No additional configuration is required on the consumer side.

### Constraint attributes baked at generation time

`GenForGenerator` inspects each constructor parameter and property for recognised constraint attributes and folds them into the emitted strategy expression:

| Attribute | Effect on emitted strategy |
|---|---|
| `[GenRange(min, max)]` | `Gen.Integers(min, max)` / `Gen.Floats(min, max)` |
| `[Range(min, max)]` (DataAnnotations) | Same as `[GenRange]` |
| `[StringLength(max)]` | `Gen.Strings(maxLength: max)` |
| `[MinLength(n)]` / `[MaxLength(n)]` | `Gen.Strings(minLength: n)` / `Gen.Strings(maxLength: n)` for strings; `Gen.Lists(…).WithLength(n, n)` for collections |
| `[Required]` | Forces `string` to use `Gen.Strings(minLength: 1)` (no empty strings) |
| No attribute | Existing type-map defaults (ADR-0029) |

FluentValidation rules are not recognised at generation time and are deferred to a future issue.

### Override DSL: `ForConfiguration<T>`

Per-call-site overrides are applied via a fluent `cfg => cfg.Override(…)` lambda passed to an overload of `Gen.For<T>()`:

```csharp
Strategy<Order> s = Gen.For<Order>(cfg => cfg
    .Override(o => o.Amount, Gen.Decimals(0m, 1_000m))
    .Override(o => o.CustomerId, Gen.Integers(1, 9999)));
```

`ForConfiguration<T>` wraps the base strategy from `ProviderRegistry` and applies member-level replacements as post-construction transforms. It does not mutate the shared registered strategy. Each override produces a new `Strategy<T>` that draws from the overriding strategy for the specified member and from the base generated strategy for all others.

### Abstract types and hierarchy handling

`Gen.For<T>()` for an abstract type follows ADR-0051: the emitted `{TypeName}Arbitrary` uses `Generate.OneOf` across all concrete subtypes decorated with `[Arbitrary]`. If no decorated concrete subtypes exist, `GenForGenerator` emits **CON311** (Error) and produces no registry entry.

Interfaces are not supported as the type argument to `Gen.For<T>()`. If `T` is an interface with no registered provider, `GenForGenerator` emits **CON310** (Error) at any call site it can detect, and `ProviderRegistry.Resolve<T>()` throws at runtime for call sites it cannot.

### Diagnostics

| ID | Severity | Condition |
|---|---|---|
| CON310 | Error | `Gen.For<T>()` where `T` is an interface with no registered `IStrategyProvider<T>` |
| CON311 | Error | `[Arbitrary]` on an abstract type with no decorated concrete subtypes in the compilation |
| CON312 | Warning | `[GenRange]` and a DataAnnotations constraint both present on the same member — `[GenRange]` wins; DataAnnotations constraint is ignored |

### Relationship to partial constructor generation (ADR-0049)

Partial constructor generation (ADR-0049) is a separate, orthogonal mechanism. A type may use both: `[Arbitrary]` triggers `{TypeName}Arbitrary` registration; a `partial` constructor provides constructor-internal drawing. `Gen.For<T>()` will call the generated `{TypeName}Arbitrary.Create()` which in turn invokes the partial constructor through the `PartialConstructorContext` ambient. No conflict or coupling.

## Consequences

- Users can obtain a strategy for any `[Arbitrary]`-decorated type with a single `Gen.For<T>()` call — no need to name or instantiate the generated provider type directly.
- The module-initializer registry is trim-safe and NativeAOT-compatible: no runtime assembly scanning, no reflection over members.
- Attribute constraints (`[GenRange]`, DataAnnotations) are baked at generation time, so the resulting strategy is optimal and does not require filter-based rejection sampling.
- Per-call-site overrides via `ForConfiguration<T>` do not mutate the shared registry entry, so test isolation is maintained.
- `GenForGenerator` must run after `ArbitraryGenerator` so the `{TypeName}Arbitrary` types it references are already emitted. This is achieved by running both in the same incremental pipeline pass (they are part of the same generator assembly).
- Cross-assembly providers are registered automatically via module initializers, but only when the external assembly is loaded. Lazy loading could silently delay registration; test harnesses should reference providers eagerly.

## Alternatives Considered

- **Separate `[GenerateStrategy]` attribute**: Would require two attributes for the same type, splitting the declaration. `[Arbitrary]` is already the canonical opt-in marker; a second attribute adds ceremony with no benefit.
- **Runtime assembly scanning via `AppDomain.GetAssemblies()`**: Finds all `IStrategyProvider<T>` implementations dynamically. Rejected — breaks NativeAOT (ADR-0014) and adds non-deterministic startup cost.
- **Convention-based naming (`typeof(T).Name + "Arbitrary"`)**: Avoids a registry entirely. Rejected — requires `Activator.CreateInstance` (reflection), breaks NativeAOT, and fails for types in nested namespaces or with conflicting names.
- **Static `Gen.Register<T, TProvider>()`** (manual registration only): Gives users control but removes the zero-configuration story. Module initializers achieve automatic registration without sacrificing manual override capability.
- **Fluent builder returning a new `Strategy<T>` (no registry)**: `Gen.For<Order>().With(o => o.Amount, Gen.Decimals(...))`. Ergonomic but requires the user to chain from something; the registry is still needed to seed the base strategy.
