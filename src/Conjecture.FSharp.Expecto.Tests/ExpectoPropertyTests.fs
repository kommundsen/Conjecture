// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

module Conjecture.FSharp.Expecto.Tests.ExpectoPropertyTests

open Expecto
open Conjecture.FSharp.Expecto

/// Run a single Test synchronously via Expecto's CLI runner and return the exit code.
let private runTest (t: Test) : int =
    runTestsWithCLIArgs [] [||] t

let tests =
    testList "Conjecture.FSharp.Expecto.property" [

        testCase "always-true bool property passes" <| fun () ->
            let t = property "always true" (fun (x: int) -> true)
            let exitCode = runTest t
            Expect.equal exitCode 0 "always-true property should report 0 failures"

        testCase "always-false bool property fails" <| fun () ->
            let t = property "always false" (fun (x: int) -> false)
            // Either exit code is non-zero or the runner throws — both indicate failure.
            let failed =
                try
                    let exitCode = runTest t
                    exitCode <> 0
                with _ ->
                    true
            Expect.isTrue failed "always-false property should report at least one failure"

        testCase "unit assertion-style property passes" <| fun () ->
            // Uses the unit-returning overload of `property`.
            let t : Test = property "assertion style" (fun (xs: int list) -> ignore (List.rev (List.rev xs)))
            let exitCode = runTest t
            Expect.equal exitCode 0 "unit-returning property that never throws should pass"

        testCase "properties compose into testList without error" <| fun () ->
            let suite =
                testList "composed" [
                    property "p1" (fun (x: int) -> true)
                    property "p2" (fun (s: string) -> true)
                ]
            // Composition must not raise; a non-null Test value is sufficient.
            Expect.isNotNull (box suite) "testList composition should produce a non-null Test value"
    ]
