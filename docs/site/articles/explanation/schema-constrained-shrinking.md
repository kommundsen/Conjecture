# Why schema-constrained strategies shrink correctly

Schema-derived strategies produce values that conform to the schema — and their shrunken counterexamples do too. This page explains why that works and where the limits are.

## The concern

When you use a custom `Strategy<T>`, Conjecture shrinks the underlying byte buffer, not the value. A shorter buffer usually produces a simpler value, but there's no guarantee the simpler value still satisfies arbitrary constraints.

For schema-driven strategies, the question becomes: can shrinking produce a value that _violates_ the schema? If shrinking yields `{ "age": -5 }` from a schema that declares `"minimum": 0`, the counterexample is useless — the shrunken input doesn't represent a valid API call.

## How schema strategies avoid this

Schema-driven strategies in Conjecture are _generative_, not _filterable_. Instead of generating an unconstrained value and filtering out those that don't match:

```csharp
// NOT how it works — this breaks shrinking
Generate.Integers<int>().Where(x => x >= 0 && x <= 150)
```

…the strategy draws _directly within the constrained range_:

```csharp
// How it actually works
Generate.Integers(0L, 150L)
```

Because the bounds are baked into the draw call, every buffer — including the shrunken ones — produces a value inside the valid range. The shrinker moves toward smaller buffers, which map to values closer to the minimum of the range, but never outside it.

The same principle applies to every constraint the parser handles: `minLength`/`maxLength` become the bounds passed to `Generate.Strings`; `enum` becomes `Generate.SampledFrom`; `pattern` becomes `Generate.Matching`. No filtering, no post-generation rejection.

## The shrinking guarantee

For every supported JSON Schema keyword, the shrunken counterexample satisfies the same constraints as the original generated value. A failing `{ "age": 3 }` came from a strategy that can only produce ages in `[0, 150]`, so any shrunken version will also have age in `[0, 150]`.

## Where shrinking is approximate

A few constructs involve choices rather than ranges, and shrinking choices is less deterministic:

**`oneOf` / `anyOf`** — Conjecture picks one sub-schema uniformly. Shrinking may choose a different arm. The shrunken value still satisfies _some_ sub-schema, but possibly a different one than the original. For most property tests this is fine, but if you're testing behaviour that's specific to one arm, pin the sub-schema explicitly.

**`allOf` with incompatible sub-schemas** — Conjecture merges properties from all sub-schemas. If two sub-schemas declare the same property with incompatible constraints, the last one wins. The generated value satisfies the merged result, but not necessarily each sub-schema individually.

**Deferred keywords** — Keywords like `additionalProperties`, `if`/`then`/`else`, and `uniqueItems` are not yet enforced during generation. Values will not necessarily satisfy these constraints, and shrinking provides no guarantee about them either. See the [schema strategies reference](../reference/schema-strategies.md#deferred-keywords) for the full list.

## Contrast with post-generation validation

An alternative approach is to generate unconstrained values and validate them after generation, discarding failures with `Assume.That`. This has two problems:

1. **Filter budget exhaustion** — if the schema is selective, Conjecture exhausts its assumption budget and fails the test with `UnsatisfiedAssumptionException` rather than finding a real counterexample.
2. **Broken shrinking** — shrunken values are drawn from the unconstrained domain and may not pass the validator, causing the shrinker to reject valid shrink steps. Counterexamples are often larger than necessary.

Generative constraints avoid both problems.

## Further reading

- [How to generate data from a JSON Schema definition](../how-to/generate-from-json-schema.md)
- [Schema strategies reference](../reference/schema-strategies.md)
- [Understanding shrinking](shrinking.md)
