// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

module Conjecture.FSharp.Tests.FSharpPropertyTests

open Xunit
open Conjecture
open Conjecture.Core

type Point = { X: int; Y: int }

[<Fact>]
let ``PropertyRunner.runBool failing property reports Failed with F# record format`` () =
    task {
        let gen = Gen.auto<Point> ()
        let! result = PropertyRunner.runBool gen (fun p -> p.X > 100 && p.Y > 100)
        match result with
        | PropertyResult.Failed message ->
            Assert.Contains("{ X =", message)
            Assert.Contains("Y =", message)
        | PropertyResult.Passed ->
            Assert.Fail("Expected property to fail but it passed")
    }

[<Fact>]
let ``PropertyRunner.runBool failing property does not use C# record format`` () =
    task {
        let gen = Gen.auto<Point> ()
        let! result = PropertyRunner.runBool gen (fun p -> p.X > 100 && p.Y > 100)
        match result with
        | PropertyResult.Failed message ->
            Assert.DoesNotContain("Point {", message)
        | PropertyResult.Passed ->
            Assert.Fail("Expected property to fail but it passed")
    }

[<Fact>]
let ``PropertyRunner.runUnit failing property reports Failed with counterexample`` () =
    task {
        let gen = Gen.auto<Point> ()
        let! result = PropertyRunner.runUnit gen (fun _ ->
            raise (System.Exception("always fails")))
        match result with
        | PropertyResult.Failed message ->
            Assert.NotNull(message)
            Assert.True(message.Length > 0)
        | PropertyResult.Passed ->
            Assert.Fail("Expected property to fail but it passed")
    }

[<Fact>]
let ``PropertyRunner.runBool passing property reports Passed`` () =
    task {
        let gen = Gen.auto<Point> ()
        let! result = PropertyRunner.runBool gen (fun _ -> true)
        Assert.Equal(PropertyResult.Passed, result)
    }

[<Fact>]
let ``PropertyRunner.runUnit passing property reports Passed`` () =
    task {
        let gen = Gen.auto<Point> ()
        let! result = PropertyRunner.runUnit gen (fun _ -> ())
        Assert.Equal(PropertyResult.Passed, result)
    }
