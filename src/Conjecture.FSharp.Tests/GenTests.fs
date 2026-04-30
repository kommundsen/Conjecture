// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

module Conjecture.FSharp.Tests.GenTests

open Xunit
open Conjecture
open Conjecture.Core

[<Fact>]
let ``Gen.constant creates a generator that always produces the same value`` () =
    let value = 42
    let gen = Gen.constant value
    let sample = StrategySamplingExtensions.Sample(gen |> Gen.unwrap)
    Assert.Equal(value, sample)

[<Fact>]
let ``Gen.map applies a function to generated values`` () =
    let gen = Gen.constant 42 |> Gen.map ((*) 2)
    let sample = StrategySamplingExtensions.Sample(gen |> Gen.unwrap)
    Assert.Equal(84, sample)

[<Fact>]
let ``Gen.int generates values within the specified range`` () =
    let gen = Gen.int (1, 10)
    let samples = StrategySamplingExtensions.Stream(gen |> Gen.unwrap, 10)
    for sample in samples do
        Assert.True(sample >= 1 && sample <= 10)

[<Fact>]
let ``Gen.filter produces only values that satisfy the predicate`` () =
    let gen = Gen.int (1, 10) |> Gen.filter (fun x -> x > 5)
    let samples = StrategySamplingExtensions.Stream(gen |> Gen.unwrap, 10)
    for sample in samples do
        Assert.True(sample > 5 && sample <= 10)

[<Fact>]
let ``Gen.oneOf produces values from one of the given generators`` () =
    let gen = Gen.oneOf [Gen.constant 1; Gen.constant 2]
    let samples = StrategySamplingExtensions.Stream(gen |> Gen.unwrap, 10)
    for sample in samples do
        Assert.True(sample = 1 || sample = 2)

[<Fact>]
let ``open Conjecture brings Gen into scope`` () =
    // This test verifies that after 'open Conjecture', we can use Gen without module qualification
    let gen = Gen.constant 42
    let sample = StrategySamplingExtensions.Sample(gen |> Gen.unwrap)
    Assert.Equal(42, sample)

[<Fact>]
let ``Gen.bind chains generators together`` () =
    let gen = Gen.constant 5 |> Gen.bind (fun x -> Gen.constant (x * 3))
    let sample = StrategySamplingExtensions.Sample(gen |> Gen.unwrap)
    Assert.Equal(15, sample)

[<Fact>]
let ``Gen.float generates values within the specified range`` () =
    let gen = Gen.float (0.0, 1.0)
    let samples = StrategySamplingExtensions.Stream(gen |> Gen.unwrap, 10)
    for sample in samples do
        Assert.True(sample >= 0.0 && sample <= 1.0)

[<Fact>]
let ``Gen.string generates strings with length in the specified range`` () =
    let gen = Gen.string (3, 5)
    let samples = StrategySamplingExtensions.Stream(gen |> Gen.unwrap, 10)
    for sample in samples do
        let len = String.length sample
        Assert.True(len >= 3 && len <= 5)

[<Fact>]
let ``Gen.bool generates only true or false`` () =
    let gen = Gen.bool
    let samples = StrategySamplingExtensions.Stream(gen |> Gen.unwrap, 10)
    for sample in samples do
        Assert.True(sample = true || sample = false)
