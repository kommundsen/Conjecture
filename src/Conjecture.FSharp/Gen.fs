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
