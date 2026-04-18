# Reference: `Conjecture.FSharp`

The `Conjecture.FSharp` package exposes a `Gen<'a>` type, a `Gen` module, a `gen { }` computation expression, and a `PropertyRunner` for executing properties.

Namespace: `Conjecture`

## `Gen<'a>`

```fsharp
[<Struct>]
type Gen<'a> = internal Gen of Strategy: Strategy<'a>
```

An opaque struct wrapping `Conjecture.Core.Strategy<'a>`. Construct values via the `Gen` module, not the constructor. Use `Gen.unwrap` to access the underlying `Strategy<'a>` when interoperating with the C# API.

## `Gen` module

| Function | Signature | Notes |
|---|---|---|
| `constant` | `'a -> Gen<'a>` | Always produces the given value. |
| `map` | `('a -> 'b) -> Gen<'a> -> Gen<'b>` | Projects generated values. |
| `filter` | `('a -> bool) -> Gen<'a> -> Gen<'a>` | Discards values failing the predicate. Aggressive filters hurt performance. |
| `bind` | `('a -> Gen<'b>) -> Gen<'a> -> Gen<'b>` | Monadic bind; enables dependent draws. |
| `oneOf` | `Gen<'a> list -> Gen<'a>` | Uniformly picks one of the supplied generators. |
| `int` | `int * int -> Gen<int>` | Integer in `[min, max]`. |
| `float` | `float * float -> Gen<float>` | Double in `[min, max]`. |
| `string` | `int * int -> Gen<string>` | String of length `[minLen, maxLen]`. |
| `bool` | `Gen<bool>` | Value, not a function — booleans. |
| `list` | `Gen<'a> -> Gen<'a list>` | F# `list`. |
| `option` | `Gen<'a> -> Gen<'a option>` | 50/50 `Some` vs. `None`. |
| `result` | `Gen<'ok> -> Gen<'err> -> Gen<Result<'ok, 'err>>` | 50/50 `Ok` vs. `Error`. |
| `set` | `Gen<'a> -> Gen<Set<'a>>` | Deduplicated into an F# `Set`. |
| `seq` | `Gen<'a> -> Gen<seq<'a>>` | Finite sequence. |
| `tuple2` | `Gen<'a> -> Gen<'b> -> Gen<'a * 'b>` | Pair. |
| `auto<'a>` | `unit -> Gen<'a>` | Reflection-driven generator. Trim-unsafe — see below. |
| `unwrap` | `Gen<'a> -> Strategy<'a>` | Escape hatch to the C# core API. |

## `GenBuilder` (the `gen { }` expression)

| Operation | Signature | Syntax |
|---|---|---|
| `Bind` | `Gen<'a> * ('a -> Gen<'b>) -> Gen<'b>` | `let! x = genA` |
| `Return` | `'a -> Gen<'a>` | `return value` |
| `ReturnFrom` | `Gen<'a> -> Gen<'a>` | `return! gen` |
| `MergeSources` | `Gen<'a> * Gen<'b> -> Gen<'a * 'b>` | `and!` |

`gen` is auto-opened from the `GenBuilderValue` module, so `gen { ... }` is available wherever `Conjecture` is opened.

## `PropertyRunner` module

| Function | Signature | Notes |
|---|---|---|
| `runBool` | `Gen<'a> -> ('a -> bool) -> Task<PropertyResult>` | Property fails when the predicate returns `false`. |
| `runUnit` | `Gen<'a> -> ('a -> unit) -> Task<PropertyResult>` | Property fails when the function throws. |

```fsharp
type PropertyResult =
    | Passed
    | Failed of message: string
```

Both functions use a default `ConjectureSettings()` and the standard test runner. For custom settings, drop into the C# API via `Gen.unwrap` and `TestRunner.Run` directly.

The failure `message` includes: example count, shrink count, formatted counterexample, and a seed for reproduction.

## `FSharpFormatter` module

| Function | Signature |
|---|---|
| `format` | `obj -> string` |

Formats F# records, unions, and tuples with `sprintf "%A"`. Falls back to `ToString()` for other types. Used internally by `PropertyRunner` to print counterexamples.

## `Conjecture.FSharp.Expecto`

Module (not a namespace): `Conjecture.FSharp.Expecto`.

| Function | Signature |
|---|---|
| `property` | `string -> ('a -> 'r) -> Test` |

Returns an Expecto `Test`. The return type `'r` must be `bool` or `unit` — anything else throws at runtime. Internally calls `Gen.auto<'a> ()`.

## F# type coverage

Types `Gen.auto<'a>` supports out of the box:

| Type | Default generator |
|---|---|
| `int` | `Gen.int (-1000, 1000)` |
| `float` | `Gen.float (-1000.0, 1000.0)` |
| `float32` | same as `float`, cast down |
| `string` | `Gen.string (0, 20)` |
| `bool` | `Gen.bool` |
| F# records | each field recursively |
| F# discriminated unions | uniform over cases, each case recursively |

Not supported by `Gen.auto`:

- Collections (`list`, `option`, `Set`, arrays) — build with `Gen.list`, `Gen.option`, `Gen.set`
- C# classes, structs, interfaces
- Generic type parameters that remain unresolved
- Tuples — use `Gen.tuple2` explicitly

Anything unsupported throws `NotSupportedException` at call time.

## Trim-safety

`Gen.auto` is annotated `[<RequiresUnreferencedCode>]`. It uses `FSharp.Reflection` to walk types at runtime, which trimming and native AOT cannot statically verify. Two consequences:

- Building with `<PublishTrimmed>true</PublishTrimmed>` or `<PublishAot>true</PublishAot>` emits an `IL2026`/`IL3050` warning at each `Gen.auto` call site.
- At runtime under trimming, the reflected fields may have been removed, causing `Gen.auto` to throw.

Mitigations:

- Annotate calling functions with `[<RequiresUnreferencedCode>]` to propagate the warning.
- For trim-safe tests, write generators by hand with the `gen { }` expression.
- `Gen.auto` is intended for test code, which is normally not trimmed.

## See also

- [Tutorial: F# property tests](../tutorials/08-fsharp-property-tests.md)
- [How-to: `Gen.auto` for records and unions](../how-to/use-fsharp-gen-auto.md)
- [Explanation: why F# has its own package](../explanation/fsharp-wrapper.md)
