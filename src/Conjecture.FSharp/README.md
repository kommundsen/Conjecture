# Conjecture.FSharp

Idiomatic F# wrappers around [Conjecture](https://github.com/kommundsen/Conjecture) property-based testing. Exposes a `Gen<'a>` strategy type with the usual combinators (`map`, `filter`, `bind`, `oneOf`, `tuple2`, `list`, `set`, `option`, `result`, `auto`) and a `PropertyRunner` that turns an `'a -> bool` (or `'a -> unit`) into a runnable property.

## Install

```
dotnet add package Conjecture.Core
dotnet add package Conjecture.FSharp
```

To run properties under Expecto, use the [`Conjecture.FSharp.Expecto`](https://www.nuget.org/packages/Conjecture.FSharp.Expecto) integration.

## Usage

```fsharp
open Conjecture
open System.Threading.Tasks

let genIntList : Gen<int list> = Gen.list (Gen.int (-100, 100))

let reverseInvolutive (xs: int list) : bool =
    List.rev (List.rev xs) = xs

let runResult : Task<PropertyResult> =
    PropertyRunner.runBool genIntList reverseInvolutive

match runResult.Result with
| PropertyResult.Passed     -> printfn "ok"
| PropertyResult.Failed msg -> failwith msg
```

For records and discriminated unions, `Gen.auto<'a> ()` derives a generator via `FSharp.Reflection`:

```fsharp
type Address = { Street: string; ZipCode: int }
type Customer = { Name: string; Address: Address option }

let genCustomer : Gen<Customer> = Gen.auto<Customer> ()
```

## API

| Function | Returns | Notes |
|---|---|---|
| `Gen.constant v` | `Gen<'a>` | Always yields `v`. |
| `Gen.int (min, max)` / `Gen.float (...)` / `Gen.string (...)` / `Gen.bool` | `Gen<_>` | Primitive generators. |
| `Gen.list g` / `Gen.set g` / `Gen.seq g` / `Gen.option g` / `Gen.result ok err` / `Gen.tuple2 g1 g2` | composed `Gen<_>` | Standard structural generators. |
| `Gen.map f g` / `Gen.filter p g` / `Gen.bind f g` / `Gen.oneOf gs` | `Gen<_>` | Combinators. |
| `Gen.auto<'a> ()` | `Gen<'a>` | Reflection-based generator for records and DUs (uses `FSharp.Reflection`; not trim-safe). |
| `PropertyRunner.runBool g test` | `Task<PropertyResult>` | Runs `test : 'a -> bool`; `false` is failure. |
| `PropertyRunner.runUnit g test` | `Task<PropertyResult>` | Runs `test : 'a -> unit`; any exception is failure. |
| `PropertyResult.Passed` / `PropertyResult.Failed of string` | DU | Outcome including a formatted counterexample. |

## Links

- [GitHub](https://github.com/kommundsen/Conjecture)
- [Documentation](https://ommundsen.dev/Conjecture/)
- [License](https://github.com/kommundsen/Conjecture/blob/main/LICENSE-MIT.txt)
