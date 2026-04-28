# Conjecture.FSharp.Expecto

[Expecto](https://github.com/haf/expecto) integration for [`Conjecture.FSharp`](https://www.nuget.org/packages/Conjecture.FSharp). Adds a `property` test builder that derives a generator via `Gen.auto<'a> ()` and runs the assertion under Conjecture's shrinking engine.

## Install

```
dotnet add package Conjecture.Core
dotnet add package Conjecture.FSharp
dotnet add package Conjecture.FSharp.Expecto
```

## Usage

```fsharp
open Expecto
open Conjecture.FSharp.Expecto

let tests =
    testList "list invariants" [
        property "reversal is involutive" (fun (xs: int list) ->
            List.rev (List.rev xs) = xs)

        property "concat preserves length" (fun (a: int list, b: int list) ->
            List.length (a @ b) = List.length a + List.length b)
    ]

[<EntryPoint>]
let main argv =
    runTestsWithCLIArgs [] argv tests
```

The `property` function accepts any `'a -> bool` (false → failure) or `'a -> unit` (exception → failure). The argument type drives `Gen.auto<'a> ()`, so any record / DU built from primitives, options, results, lists, sets, sequences, and tuples is covered out of the box.

## API

| Function | Returns | Notes |
|---|---|---|
| `property name (test : 'a -> bool)` | `Test` | Reflection-derived `Gen<'a>`; `false` ⇒ failure. |
| `property name (test : 'a -> unit)` | `Test` | Same generator; any exception ⇒ failure. |

## Links

- [GitHub](https://github.com/kommundsen/Conjecture)
- [Documentation](https://ommundsen.dev/Conjecture/)
- [License](https://github.com/kommundsen/Conjecture/blob/main/LICENSE-MIT.txt)
