# Draft: Idiomatic F# Support

## Motivation

F# has a strong tradition of property-based testing and functional programming patterns. Conjecture's C# API (LINQ combinators, fluent method chains) is callable from F# but feels foreign. An idiomatic F# wrapper would let F# developers use Conjecture's engine (byte-buffer shrinking, targeted testing, stateful testing) with the syntax and conventions they expect: computation expressions, curried functions, pipe operators, and module-based APIs.

ADR-0013 already decided to ship this as a separate `Conjecture.FSharp` NuGet package with a one-way dependency on `Conjecture.Core`.

## .NET Advantage

F# 10 brings improvements that directly benefit a Conjecture wrapper:
- **`and!` in task expressions** — concurrent draws with `gen { let! a = genA and! b = genB }`
- **`ValueOption` optional parameters** (`[<Struct>]`) — zero-allocation optional config in generator functions
- **Better trimming by default** — F# 10 auto-generates IL linker substitutions, keeping the wrapper trim-safe
- **Parallel compilation** — faster builds for test projects using the wrapper
- **Attribute target enforcement** — ensures `[<Property>]` is only applied to valid targets (methods, not values)
- **Typed CE bindings without parentheses** — cleaner `gen { let! x: int = ... }` syntax

## Key Ideas

### `gen { }` Computation Expression
```fsharp
let personGen =
    gen {
        let! name = Gen.string (1, 50)
        let! age = Gen.int (0, 150)
        return { Name = name; Age = age }
    }
```
- Builder type: `GenBuilder` wrapping `IGeneratorContext`
- Supports `let!`, `return`, `return!`, `yield`, `yield!`, `and!`
- `and!` enables concurrent draws (semantically equivalent to sequential in PBT, but mirrors F# 10 idiom)

### `Gen` Module
```fsharp
module Gen =
    val int: min:int * max:int -> Gen<int>
    val float: min:float * max:float -> Gen<float>
    val string: minLen:int * maxLen:int -> Gen<string>
    val bool: Gen<bool>
    val list: Gen<'a> -> Gen<'a list>
    val option: Gen<'a> -> Gen<'a option>
    val oneOf: Gen<'a> list -> Gen<'a>
    val constant: 'a -> Gen<'a>
    val map: ('a -> 'b) -> Gen<'a> -> Gen<'b>
    val filter: ('a -> bool) -> Gen<'a> -> Gen<'a>
    val bind: ('a -> Gen<'b>) -> Gen<'a> -> Gen<'b>
```
- Curried, pipe-friendly (`gen |> Gen.filter isPositive |> Gen.map string`)
- Wraps `Strategy<T>` internally
- Uses F# naming conventions (lowercase module functions, tuple parameters)

### `[<Property>]` Attribute for F# Test Frameworks
```fsharp
[<Property>]
let ``addition is commutative`` (a: int) (b: int) =
    a + b = b + a

[<Property(MaxExamples = 500)>]
let ``list reverse is involutory`` (xs: int list) =
    List.rev (List.rev xs) = xs
```
- Works with xUnit and NUnit via existing adapters
- Supports curried multi-parameter properties (F# convention)
- Auto-resolves `Gen<'a>` for standard F# types: `int`, `string`, `float`, `list`, `option`, `Result`, tuples, records, discriminated unions

### Discriminated Union Strategy Auto-Generation
```fsharp
type Shape =
    | Circle of radius: float
    | Rectangle of width: float * height: float
    | Triangle of a: float * b: float * c: float

// Auto-generated:
Gen.auto<Shape>
```
- F# DUs are a natural fit for `OneOf` composition
- Each case's fields resolved recursively
- Weighting configurable: `Gen.auto<Shape> |> Gen.withWeight (Circle, 0.5)`

### Record Auto-Generation
```fsharp
type Person = { Name: string; Age: int }

Gen.auto<Person>
// Equivalent to: gen { let! name = Gen.string ... let! age = Gen.int ... return { Name = name; Age = age } }
```

### Stateful Testing with DU Commands
```fsharp
type StackCmd =
    | Push of int
    | Pop
    | Peek

let stackMachine = {
    InitialState = Stack.empty
    Execute = fun state cmd ->
        match cmd with
        | Push x -> Stack.push x state
        | Pop -> Stack.pop state |> snd
        | Peek -> state
    Precondition = fun state cmd ->
        match cmd with
        | Pop | Peek -> not (Stack.isEmpty state)
        | _ -> true
}
```
- DU cases as commands (idiomatic F# pattern)
- Adapts to `IStateMachine<TState, TCommand>` internally

### Targeted Testing
```fsharp
[<Property>]
let ``binary search finds element`` (xs: int list) (target: int) =
    let sorted = List.sort xs
    Target.maximize (float (List.length sorted)) "list-length"
    binarySearch sorted target = List.contains target sorted
```

## Design Decisions to Make

1. **`Gen<'a>` type**: Thin wrapper around `Strategy<T>`, or type alias? Wrapper enables F#-specific methods; alias avoids duplication.
2. **Record/DU auto-generation**: Use F# reflection (simple, not trim-safe) or a source generator / Myriad plugin (trim-safe, more complex)?
3. **Expecto integration**: Expecto is popular in F# — ship adapter or rely on xUnit adapter?
4. **Naming**: `Conjecture.FSharp` (per ADR-0013) with a module alias like `open Conjecture.Gen`?
5. **Property syntax**: Support both `bool`-returning and `unit`-returning (with assertions) properties?
6. **F# type coverage**: Which F# types get built-in generators? (`list`, `option`, `Result`, `Map`, `Set`, `seq`, anonymous records, tuples)

## Scope Estimate

Medium-Large. Core `Gen` module + CE builder is ~2 cycles. DU/record auto-generation and framework adapters add ~2 more. Total ~4 cycles.

## Dependencies

- `Conjecture.Core` (one-way dependency per ADR-0013)
- F# 10 compiler (ships with .NET 10 SDK)
- `FSharp.Core` (in-box)
- Existing `SharedParameterStrategyResolver` for framework integration

## Open Questions

- Should `Gen<'a>` expose Conjecture's `IGeneratorContext` directly or only through the computation expression?
- Trim safety: F# reflection on DUs/records is convenient but not trim-safe. Is a source generator approach viable for F#?
- How to handle F#'s structural equality in shrinking? (F# records/DUs have `Equals` by default — useful for dedup during shrinking)
- What level of Expecto support is expected by the F# community?
- Should the wrapper provide F#-idiomatic error messages (e.g., using `%A` formatting for counterexamples)?
