# Use `Gen.auto` for F# records and unions

`Gen.auto<'a> ()` derives a generator for any F# record, discriminated union, or primitive type using reflection. Use it when you don't need fine control over field values.

## Records

```fsharp
open Conjecture

type Customer = { Id: int; Name: string; IsActive: bool }

let customerGen : Gen<Customer> = Gen.auto<Customer> ()
```

Every record field gets a default generator: `int` → `-1000..1000`, `string` → length `0..20`, `bool` → booleans, `float`/`float32` → `-1000.0..1000.0`.

## Discriminated unions

```fsharp
type Shape =
    | Circle of radius: float
    | Rectangle of width: float * height: float
    | Point

let shapeGen : Gen<Shape> = Gen.auto<Shape> ()
```

`Gen.auto` picks a case uniformly, then recurses into its fields. Cases with no fields (like `Point`) are treated as base cases and prevent infinite recursion.

## Nested and recursive types

`Gen.auto` recurses up to a fixed depth (3 levels). Mutually recursive types terminate at a zero-field case:

```fsharp
type Tree =
    | Leaf
    | Node of int * Tree * Tree

let treeGen : Gen<Tree> = Gen.auto<Tree> ()
```

At the depth limit, `Gen.auto` picks a zero-field case if one exists; otherwise it throws `NotSupportedException`. If your DU has no base case, write the generator by hand with `Gen.oneOf` and an explicit recursion depth.

## Concurrent draws with `and!`

The `gen { }` computation expression supports `and!` to express independent draws:

```fsharp
let pair =
    gen {
        let! x = Gen.int (0, 10)
        and! y = Gen.int (100, 200)
        return (x, y)
    }
```

Compared to a chain of `let!`, `and!` signals that the two draws are independent — there's no data dependency between them. The compiler routes this through `GenBuilder.MergeSources` and the result is the same as `Gen.tuple2`.

## Stateful tests via DU commands

Conjecture.FSharp does not yet have a built-in stateful-testing DSL. The idiomatic F# pattern is to represent commands as a DU and generate a list:

```fsharp
type StackCmd<'a> =
    | Push of 'a
    | Pop
    | Peek

let cmdGen : Gen<StackCmd<int>> =
    Gen.oneOf [
        Gen.int (0, 100) |> Gen.map Push
        Gen.constant Pop
        Gen.constant Peek
    ]

let programGen : Gen<StackCmd<int> list> = Gen.list cmdGen
```

Run each generated program against a real implementation and a reference model, asserting they agree. When Conjecture finds a failing sequence, shrinking reduces both the command count and the values inside each command.

## Use with Expecto

```fsharp
open Conjecture.FSharp.Expecto

[<Tests>]
let tests =
    testList "customers" [
        property "round-trip serialization preserves customer" (fun (c: Customer) ->
            let serialized = Json.serialize c
            let deserialized : Customer = Json.deserialize serialized
            c = deserialized)
    ]
```

`property` internally calls `Gen.auto<'a> ()`. For custom generators, drop to `PropertyRunner.runBool`/`runUnit` directly:

```fsharp
testCase "custom generator" <| fun () ->
    let gen =
        gen {
            let! id = Gen.int (1, 1_000_000)
            let! name = Gen.string (1, 50)
            return { Id = id; Name = name; IsActive = true }
        }
    let result = PropertyRunner.runBool gen (fun c -> c.Id > 0) |> Async.AwaitTask |> Async.RunSynchronously
    match result with
    | PropertyResult.Passed -> ()
    | PropertyResult.Failed msg -> failwith msg
```

## See also

- [Reference: the `Gen` module](../reference/fsharp-gen.md)
- [Why F# has its own package](../explanation/fsharp-wrapper.md)
