# Why F# has its own Conjecture package

Conjecture's core is written in C#. F# can call it directly — `Strategy<T>` is just a class — but doing so produces code that doesn't look or feel like F#. `Conjecture.FSharp` is a thin wrapper that bridges the gap.

## The problem with calling C# from F# directly

C# APIs reflect C# idioms: `Func<T, TResult>`, fluent extension methods, mutable `List<T>`, null-returning overloads, PascalCase members. F# code that leans on them reads awkwardly:

```fsharp
// C# API, called from F#
let s : Strategy<int> =
    StrategyExtensions.Select(
        Generate.Integers(0, 100),
        System.Func<_, _>(fun x -> x * 2))
```

Compare with the wrapper:

```fsharp
let s : Gen<int> =
    Gen.int (0, 100) |> Gen.map (fun x -> x * 2)
```

Both do the same thing. The second one looks like F#.

## What the wrapper actually does

`Gen<'a>` is a `[<Struct>]` DU wrapping `Strategy<'a>`:

```fsharp
[<Struct>]
type Gen<'a> = internal Gen of Strategy: Strategy<'a>
```

The `Gen` module is a hand-curated F# surface over `Generate.*` and `StrategyExtensions.*`, with:

- Tuples for ranges (`int * int`) instead of positional overloads
- F# collection types as outputs (`'a list`, `'a option`, `Set<'a>`)
- Lowercase `camelCase` module functions
- `|>`-friendly parameter order: the generator comes last

The `GenBuilder` computation expression wraps `bind`/`map`/`MergeSources` so you can write:

```fsharp
gen {
    let! x = Gen.int (0, 100)
    let! y = Gen.int (0, 100)
    return x + y
}
```

Nothing magical — just sugar for `Gen.bind` calls. The same technique powers `async { }`, `task { }`, and many F# DSLs.

## Counterexample formatting

C# records print as `Point { X = 5, Y = 3 }`. F# records print as `{ X = 5; Y = 3 }`. F# unions print as `Circle 5.0`, not `Shape+Circle`. These differences aren't decorative — they're the syntax you'd type to reconstruct the value.

`FSharpFormatter` detects F# records, unions, and tuples via `FSharpType` and routes them through `sprintf "%A"`. Everything else falls back to `ToString()`. `PropertyRunner` uses this formatter when building failure messages, so counterexamples come back in copy-pasteable F# syntax.

## Why not just use Hedgehog or FsCheck?

Both exist and are excellent. `Conjecture.FSharp` exists because:

- **Single engine.** Conjecture's shrinker, replay database, and targeted-testing infrastructure all live in `Conjecture.Core`. The F# wrapper inherits every improvement to the core without a parallel F# port.
- **Mixed-language projects.** A solution with C# production code and F# tests (or vice versa) can share a single strategy catalog and example database.
- **Trim and AOT alignment.** As the core moves toward trim-safety, the wrapper tracks those annotations (see [the reference](../reference/fsharp-gen.md#trim-safety)).

The wrapper doesn't compete with Hedgehog — it exposes the Conjecture engine to F# developers who are already in a Conjecture-adjacent codebase.

## The Expecto integration

`Conjecture.FSharp.Expecto` adds one function:

```fsharp
val property : string -> ('a -> 'r) -> Test
```

It's an Expecto-native alternative to the xUnit/NUnit/MSTest adapters. The function inspects `'r` at runtime, picks `PropertyRunner.runBool` or `PropertyRunner.runUnit`, runs the test synchronously via `Async.RunSynchronously`, and converts a failure into `failwith` so Expecto reports it. The name `property` was chosen to match the Expecto convention (`testCase`, `testList`, `testProperty`-style naming).

## Trade-offs

- **Reflection via `FSharp.Reflection`.** `Gen.auto` is convenient but not trim-safe. Use it when trimming isn't on; write generators by hand when it is.
- **No built-in stateful DSL yet.** Commands-as-DU is the idiomatic pattern, but there's no equivalent of C#'s stateful-testing helpers in the F# module. This is tracked as future work.
- **No `[<Property>]` attribute for xUnit-on-F#.** You can still reference `Conjecture.Xunit`'s `[<Property>]` from F# — it's the same attribute — but F#-specific attributes (Expecto-style) are the recommended path.

## See also

- [Reference: the `Gen` module](../reference/fsharp-gen.md)
- [Tutorial: F# property tests](../tutorials/08-fsharp-property-tests.md)
- [How-to: `Gen.auto` for records and unions](../how-to/use-fsharp-gen-auto.md)
