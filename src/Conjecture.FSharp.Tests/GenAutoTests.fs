// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

module Conjecture.FSharp.Tests.GenAutoTests

open Xunit
open Conjecture
open Conjecture.Core

type Color = Red | Green | Blue
type Shape = Circle of float | Rectangle of float * float
type Person = { Name: string; Age: int }
type Tree = Leaf | Node of Tree * Tree
type Inf = A of Inf
type MutA = { B: MutB }
and MutB = { A: MutA }

[<Fact>]
let ``Gen.auto produces all Color cases across sufficient samples`` () =
    let gen = Gen.auto<Color> ()
    let samples = StrategySamplingExtensions.Stream(gen |> Gen.unwrap, 200) |> Seq.toList
    Assert.True(samples |> List.exists (fun c -> c = Red))
    Assert.True(samples |> List.exists (fun c -> c = Green))
    Assert.True(samples |> List.exists (fun c -> c = Blue))

[<Fact>]
let ``Gen.auto produces Circle case for Shape`` () =
    let gen = Gen.auto<Shape> ()
    let samples = StrategySamplingExtensions.Stream(gen |> Gen.unwrap, 200) |> Seq.toList
    let hasCircle = samples |> List.exists (fun s -> match s with | Circle _ -> true | _ -> false)
    Assert.True(hasCircle)

[<Fact>]
let ``Gen.auto produces Rectangle case for Shape`` () =
    let gen = Gen.auto<Shape> ()
    let samples = StrategySamplingExtensions.Stream(gen |> Gen.unwrap, 200) |> Seq.toList
    let hasRectangle = samples |> List.exists (fun s -> match s with | Rectangle _ -> true | _ -> false)
    Assert.True(hasRectangle)

[<Fact>]
let ``Gen.auto produces Person records with string names`` () =
    let gen = Gen.auto<Person> ()
    let samples = StrategySamplingExtensions.Stream(gen |> Gen.unwrap, 100) |> Seq.toList
    for person in samples do
        Assert.True(person.Name.GetType() = typeof<string>)

[<Fact>]
let ``Gen.auto produces Person records across multiple samples`` () =
    let gen = Gen.auto<Person> ()
    let samples = StrategySamplingExtensions.Stream(gen |> Gen.unwrap, 100) |> Seq.toList
    Assert.Equal(100, samples.Length)

[<Fact>]
let ``Gen.auto falls through to built-in int generator`` () =
    let gen = Gen.auto<int> ()
    let samples = StrategySamplingExtensions.Stream(gen |> Gen.unwrap, 10) |> Seq.toList
    Assert.Equal(10, samples.Length)

[<Fact>]
let ``Gen.auto falls through to built-in string generator`` () =
    let gen = Gen.auto<string> ()
    let samples = StrategySamplingExtensions.Stream(gen |> Gen.unwrap, 10) |> Seq.toList
    Assert.Equal(10, samples.Length)

[<Fact>]
let ``Gen.auto for deeply nested Tree DU terminates without infinite recursion`` () =
    let gen = Gen.auto<Tree> ()
    let samples = StrategySamplingExtensions.Stream(gen |> Gen.unwrap, 20) |> Seq.toList
    Assert.True(samples.Length > 0)

[<Fact>]
let ``Gen.auto for all-recursive DU with no base cases throws NotSupportedException`` () =
    Assert.Throws<System.NotSupportedException>(fun () ->
        let gen = Gen.auto<Inf> ()
        StrategySamplingExtensions.Sample(gen |> Gen.unwrap) |> ignore)

[<Fact>]
let ``Gen.auto for mutually recursive record types throws NotSupportedException`` () =
    Assert.Throws<System.NotSupportedException>(fun () ->
        let gen = Gen.auto<MutA> ()
        StrategySamplingExtensions.Sample(gen |> Gen.unwrap) |> ignore)
