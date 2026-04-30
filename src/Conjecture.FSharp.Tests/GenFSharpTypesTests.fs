// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

module Conjecture.FSharp.Tests.GenFSharpTypesTests

open Xunit
open Conjecture
open Conjecture.Core

[<Fact>]
let ``Gen.list produces int list values`` () =
    let gen = Gen.list (Gen.int (0, 10))
    let samples = StrategySamplingExtensions.Stream(gen |> Gen.unwrap, 20)
    for sample in samples do
        for element in sample do
            Assert.True(element >= 0 && element <= 10)

[<Fact>]
let ``Gen.option produces Some values`` () =
    let gen = Gen.option Gen.bool
    let samples = StrategySamplingExtensions.Stream(gen |> Gen.unwrap, 100)
    let hasSome = samples |> Seq.exists Option.isSome
    Assert.True(hasSome)

[<Fact>]
let ``Gen.option produces None values`` () =
    let gen = Gen.option Gen.bool
    let samples = StrategySamplingExtensions.Stream(gen |> Gen.unwrap, 100)
    let hasNone = samples |> Seq.exists Option.isNone
    Assert.True(hasNone)

[<Fact>]
let ``Gen.option Some values are true or false`` () =
    let gen = Gen.option Gen.bool
    let samples = StrategySamplingExtensions.Stream(gen |> Gen.unwrap, 50)
    for sample in samples do
        match sample with
        | Some v -> Assert.True(v = true || v = false)
        | None -> ()

[<Fact>]
let ``Gen.result produces Ok cases`` () =
    let gen = Gen.result (Gen.int (0, 10)) (Gen.string (0, 5))
    let samples = StrategySamplingExtensions.Stream(gen |> Gen.unwrap, 100)
    let hasOk = samples |> Seq.exists (fun r -> match r with | Ok _ -> true | Error _ -> false)
    Assert.True(hasOk)

[<Fact>]
let ``Gen.result produces Error cases`` () =
    let gen = Gen.result (Gen.int (0, 10)) (Gen.string (0, 5))
    let samples = StrategySamplingExtensions.Stream(gen |> Gen.unwrap, 100)
    let hasError = samples |> Seq.exists (fun r -> match r with | Ok _ -> false | Error _ -> true)
    Assert.True(hasError)

[<Fact>]
let ``Gen.result Ok values are within range`` () =
    let gen = Gen.result (Gen.int (0, 10)) (Gen.string (0, 5))
    let samples = StrategySamplingExtensions.Stream(gen |> Gen.unwrap, 50)
    for sample in samples do
        match sample with
        | Ok v -> Assert.True(v >= 0 && v <= 10)
        | Error _ -> ()

[<Fact>]
let ``Gen.set produces Set with deduplicated elements`` () =
    let gen = Gen.set (Gen.int (0, 5))
    let samples = StrategySamplingExtensions.Stream(gen |> Gen.unwrap, 20)
    for sample in samples do
        let asList = sample |> Set.toList
        let asDistinct = asList |> List.distinct
        Assert.Equal(asList.Length, asDistinct.Length)

[<Fact>]
let ``Gen.set elements are within the generator range`` () =
    let gen = Gen.set (Gen.int (0, 5))
    let samples = StrategySamplingExtensions.Stream(gen |> Gen.unwrap, 20)
    for sample in samples do
        for element in sample do
            Assert.True(element >= 0 && element <= 5)

[<Fact>]
let ``Gen.tuple2 covers all four bool combinations across enough samples`` () =
    let gen = Gen.tuple2 Gen.bool Gen.bool
    let samples = StrategySamplingExtensions.Stream(gen |> Gen.unwrap, 200) |> Seq.toList
    let hasFF = samples |> List.exists (fun (a, b) -> a = false && b = false)
    let hasFT = samples |> List.exists (fun (a, b) -> a = false && b = true)
    let hasTF = samples |> List.exists (fun (a, b) -> a = true && b = false)
    let hasTT = samples |> List.exists (fun (a, b) -> a = true && b = true)
    Assert.True(hasFF)
    Assert.True(hasFT)
    Assert.True(hasTF)
    Assert.True(hasTT)

[<Fact>]
let ``Gen.tuple2 produces pairs with correct types`` () =
    let gen = Gen.tuple2 Gen.bool Gen.bool
    let sample = StrategySamplingExtensions.Sample(gen |> Gen.unwrap)
    let (a, b) = sample
    Assert.True(a = true || a = false)
    Assert.True(b = true || b = false)

[<Fact>]
let ``Gen.seq produces seq of int values within range`` () =
    let gen = Gen.seq (Gen.int (0, 3))
    let sample = StrategySamplingExtensions.Sample(gen |> Gen.unwrap)
    for element in sample do
        Assert.True(element >= 0 && element <= 3)

[<Fact>]
let ``Gen.seq result type is seq of int`` () =
    let gen : Gen<seq<int>> = Gen.seq (Gen.int (0, 3))
    let sample : seq<int> = StrategySamplingExtensions.Sample(gen |> Gen.unwrap)
    Assert.True(sample |> Seq.forall (fun e -> e >= 0 && e <= 3))
