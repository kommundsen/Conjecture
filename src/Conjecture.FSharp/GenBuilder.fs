// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

namespace Conjecture

type GenBuilder() =
    member _.Bind(gen: Gen<'a>, f: 'a -> Gen<'b>) : Gen<'b> =
        Gen.bind f gen

    member _.Return(value: 'a) : Gen<'a> =
        Gen.constant value

    member _.ReturnFrom(gen: Gen<'a>) : Gen<'a> =
        gen

    member _.MergeSources(gen1: Gen<'a>, gen2: Gen<'b>) : Gen<'a * 'b> =
        gen1 |> Gen.bind (fun a -> gen2 |> Gen.map (fun b -> a, b))

[<AutoOpen>]
module GenBuilderValue =
    let gen = GenBuilder()
