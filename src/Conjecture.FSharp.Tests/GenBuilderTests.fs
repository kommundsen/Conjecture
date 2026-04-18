// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

module Conjecture.FSharp.Tests.GenBuilderTests

open Xunit
open Conjecture
open Conjecture.Core

[<Fact>]
let ``gen builder with return produces the value`` () =
    let result = gen {
        return 42
    }
    let sample = DataGen.SampleOne(result |> Gen.unwrap)
    Assert.Equal(42, sample)

[<Fact>]
let ``gen builder with let! and return chains generators`` () =
    let result = gen {
        let! x = Gen.int (0, 10)
        return x * 2
    }
    let samples = DataGen.Stream(result |> Gen.unwrap, 20)
    for sample in samples do
        Assert.True(sample >= 0 && sample <= 20)
        Assert.True(sample % 2 = 0)

[<Fact>]
let ``gen builder with and! combines generators`` () =
    let result = gen {
        let! x = Gen.int (0, 10)
        and! y = Gen.bool
        return x, y
    }
    let samples = DataGen.Stream(result |> Gen.unwrap, 20)
    for sample in samples do
        let (x, y) = sample
        Assert.True(x >= 0 && x <= 10)
        Assert.True(y = true || y = false)

[<Fact>]
let ``gen builder with multiple let! bindings produces correct range`` () =
    let result = gen {
        let! x = Gen.int (1, 5)
        let! y = Gen.int (1, 5)
        return x + y
    }
    let samples = DataGen.Stream(result |> Gen.unwrap, 30)
    for sample in samples do
        Assert.True(sample >= 2 && sample <= 10)

[<Fact>]
let ``gen builder with nested blocks composes correctly`` () =
    let innerGen = gen {
        let! x = Gen.int (1, 3)
        return x * 2
    }
    let outerGen = gen {
        let! doubled = innerGen
        let! y = Gen.int (1, 3)
        return doubled + y
    }
    let samples = DataGen.Stream(outerGen |> Gen.unwrap, 30)
    for sample in samples do
        Assert.True(sample >= 3 && sample <= 9)
