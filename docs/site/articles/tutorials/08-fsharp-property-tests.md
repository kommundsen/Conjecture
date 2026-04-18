# Tutorial 8: Property Tests in F#

This tutorial walks through writing your first property-based test in F# using the `Conjecture.FSharp` package and the Expecto integration. You'll install the packages, compose a generator with the `gen { }` computation expression, run a property test, and interpret a shrunk counterexample.

## Prerequisites

- .NET 10 SDK
- A new or existing F# test project

## Step 1: Install the packages

Add `Conjecture.FSharp` and `Conjecture.FSharp.Expecto` to your test project:

```bash
dotnet add package Conjecture.FSharp
dotnet add package Conjecture.FSharp.Expecto
dotnet add package Expecto
```

`Conjecture.FSharp` provides the `Gen<'a>` type and `gen { }` computation expression. `Conjecture.FSharp.Expecto` wires those into Expecto as `testCase`-style properties.

## Step 2: Write a generator

Properties need values. In F#, you build generators in two ways:

- Call module functions directly: `Gen.int (0, 100)`, `Gen.list (Gen.bool)`
- Compose with the `gen { }` expression:

```fsharp
open Conjecture

let pointGen : Gen<int * int> =
    gen {
        let! x = Gen.int (-100, 100)
        let! y = Gen.int (-100, 100)
        return (x, y)
    }
```

`let!` draws a value from a generator — exactly like `let!` in `async { }`, but for random inputs.

## Step 3: Run a property

With Expecto, wrap a property in `property "name" (fun input -> ...)`. The function returns `bool` (pass/fail) or `unit` (any exception is a failure):

```fsharp
module Tests

open Expecto
open Conjecture.FSharp.Expecto

[<Tests>]
let tests =
    testList "list reverse" [
        property "reverse is self-inverse" (fun (xs: int list) ->
            List.rev (List.rev xs) = xs)
    ]

[<EntryPoint>]
let main argv =
    runTestsInAssemblyWithCLIArgs [] argv
```

`property` uses `Gen.auto<'a>` to pick a generator based on the input type — here, `int list`.

Run the test:

```bash
dotnet test
```

## Step 4: Read a shrunk counterexample

Change the property to something that is *almost* true but breaks on a specific case:

```fsharp
property "sum is non-negative" (fun (xs: int list) ->
    List.sum xs >= 0)
```

Run again. Expecto reports a failure with output similar to:

```text
Falsifying example found after 14 examples (shrunk 7 times)
Counterexample: [-1]
Reproduce with: [Property(Seed = 0xA4F3...)]
```

Conjecture ran 14 randomized examples, found a failure, then *shrank* it 7 times down to `[-1]` — the smallest list that falsifies the property. Values are formatted with F#'s `%A`, so records and unions print in idiomatic F# syntax, not their C# shape.

## Next steps

- [Generate records and unions with `Gen.auto`](../how-to/use-fsharp-gen-auto.md)
- [Reference: the `Gen` module](../reference/fsharp-gen.md)
- [Why F# has its own package](../explanation/fsharp-wrapper.md)
