// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

namespace Conjecture

open System
open System.Collections.Generic
open System.Diagnostics.CodeAnalysis
open Conjecture.Core
open Microsoft.FSharp.Reflection

[<Struct>]
type Gen<'a> = internal Gen of Strategy: Strategy<'a>

module Gen =
    let unwrap (gen: Gen<'a>) : Strategy<'a> =
        let (Gen s) = gen
        s

    let constant (value: 'a) : Gen<'a> =
        Gen(Generate.Just(value))

    let map (f: 'a -> 'b) (gen: Gen<'a>) : Gen<'b> =
        let strategy = unwrap gen
        Gen(StrategyExtensions.Select(strategy, System.Func<_, _>(f)))

    let filter (predicate: 'a -> bool) (gen: Gen<'a>) : Gen<'a> =
        let strategy = unwrap gen
        Gen(StrategyExtensions.Where(strategy, System.Func<_, _>(predicate)))

    let bind (f: 'a -> Gen<'b>) (gen: Gen<'a>) : Gen<'b> =
        let strategy = unwrap gen
        let selector (a: 'a) : Strategy<'b> = unwrap (f a)
        Gen(StrategyExtensions.SelectMany(strategy, System.Func<_, _>(selector)))

    let oneOf (gens: Gen<'a> list) : Gen<'a> =
        let strategies = List.toArray (List.map unwrap gens)
        Gen(Generate.OneOf(strategies))

    let int (range: int * int) : Gen<int> =
        let (min, max) = range
        Gen(Generate.Integers<int>(min, max))

    let float (range: float * float) : Gen<float> =
        let (min, max) = range
        Gen(Generate.Doubles(min, max))

    let string (range: int * int) : Gen<string> =
        let (minLen, maxLen) = range
        Gen(Generate.Strings(minLen, maxLen))

    let bool : Gen<bool> =
        Gen(Generate.Booleans())

    let list (gen: Gen<'a>) : Gen<'a list> =
        let inner = unwrap gen
        Gen(StrategyExtensions.Select(Generate.Lists(inner), System.Func<_, _>(fun (lst: System.Collections.Generic.List<'a>) -> lst |> Seq.toList)))

    let option (gen: Gen<'a>) : Gen<'a option> =
        Gen(StrategyExtensions.SelectMany(
            Generate.Booleans(),
            System.Func<_, _>(fun flag ->
                if flag then
                    StrategyExtensions.Select(unwrap gen, System.Func<_, _>(Some))
                else
                    Generate.Just(None))))

    let result (okGen: Gen<'ok>) (errGen: Gen<'err>) : Gen<Result<'ok, 'err>> =
        Gen(StrategyExtensions.SelectMany(
            Generate.Booleans(),
            System.Func<_, _>(fun flag ->
                if flag then
                    StrategyExtensions.Select(unwrap okGen, System.Func<_, _>(Ok))
                else
                    StrategyExtensions.Select(unwrap errGen, System.Func<_, _>(Error)))))

    let set (gen: Gen<'a>) : Gen<Set<'a>> =
        let inner = unwrap gen
        Gen(StrategyExtensions.Select(Generate.Lists(inner), System.Func<_, _>(fun (lst: System.Collections.Generic.List<'a>) -> lst |> Set.ofSeq)))

    let seq (gen: Gen<'a>) : Gen<seq<'a>> =
        let inner = unwrap gen
        Gen(StrategyExtensions.Select(Generate.Lists(inner), System.Func<_, _>(fun (lst: System.Collections.Generic.List<'a>) -> lst :> seq<'a>)))

    let tuple2 (genA: Gen<'a>) (genB: Gen<'b>) : Gen<'a * 'b> =
        Gen(StrategyExtensions.SelectMany(
            unwrap genA,
            System.Func<_, _>(fun a ->
                StrategyExtensions.Select(unwrap genB, System.Func<_, _>(fun b -> (a, b))))))

    let private combineFieldGens (gens: Gen<obj> list) : Gen<obj list> =
        gens
        |> List.fold
            (fun accGen fieldGen ->
                accGen
                |> bind (fun (acc: obj list) ->
                    fieldGen |> map (fun v -> v :: acc)))
            (constant [])

    [<RequiresUnreferencedCode("Uses FSharp.Reflection and is not trim-safe.")>]
    let rec private autoWithDepth (cache: Dictionary<struct (int * Type), Gen<obj>>) (depth: int) (t: Type) : Gen<obj> =
        let key = struct (depth, t)
        match cache.TryGetValue(key) with
        | true, cached -> cached
        | false, _ ->
            let gen =
                if FSharpType.IsRecord(t) then
                    let fields = FSharpType.GetRecordFields(t)
                    if depth <= 0 && fields.Length > 0 then
                        raise (NotSupportedException($"Gen.auto cannot generate type '{t.FullName}': depth limit reached."))
                    let fieldGens =
                        fields
                        |> Array.map (fun f -> autoWithDepth cache (depth - 1) f.PropertyType)
                        |> Array.toList
                    combineFieldGens fieldGens
                    |> map (fun values ->
                        let result = FSharpValue.MakeRecord(t, values |> List.rev |> List.toArray)
                        Unchecked.nonNull result)
                elif FSharpType.IsUnion(t) then
                    let cases = FSharpType.GetUnionCases(t)
                    let baseCases = cases |> Array.filter (fun c -> c.GetFields().Length = 0)
                    if depth <= 0 && baseCases.Length = 0 then
                        raise (NotSupportedException($"Gen.auto cannot generate type '{t.FullName}': depth limit reached with no base cases."))
                    let effectiveCases =
                        if depth <= 0 && baseCases.Length > 0 then baseCases else cases
                    let caseGens =
                        effectiveCases
                        |> Array.map (fun case ->
                            let fieldTypes = case.GetFields()
                            if fieldTypes.Length = 0 then
                                constant (Unchecked.nonNull (FSharpValue.MakeUnion(case, [||])))
                            else
                                let fieldGens =
                                    fieldTypes
                                    |> Array.map (fun f -> autoWithDepth cache (depth - 1) f.PropertyType)
                                    |> Array.toList
                                combineFieldGens fieldGens
                                |> map (fun values ->
                                    Unchecked.nonNull (FSharpValue.MakeUnion(case, values |> List.rev |> List.toArray))))
                        |> Array.toList
                    oneOf caseGens
                elif t = typeof<int> then
                    int (-1000, 1000) |> map (fun x -> x :> obj)
                elif t = typeof<string> then
                    string (0, 20) |> map (fun x -> x :> obj)
                elif t = typeof<bool> then
                    bool |> map (fun x -> x :> obj)
                elif t = typeof<float> then
                    float (-1000.0, 1000.0) |> map (fun x -> x :> obj)
                elif t = typeof<float32> then
                    float (-1000.0, 1000.0) |> map (fun x -> float32 x :> obj)
                else
                    raise (NotSupportedException($"Gen.auto does not support type: {t.FullName}"))
            cache[key] <- gen
            gen

    [<RequiresUnreferencedCode("Uses FSharp.Reflection and is not trim-safe.")>]
    let auto<'a> () : Gen<'a> =
        let cache = Dictionary<struct (int * Type), Gen<obj>>()
        autoWithDepth cache 3 typeof<'a> |> map (fun o -> o :?> 'a)

