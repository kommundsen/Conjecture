// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

namespace Conjecture

open Conjecture.Core

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
