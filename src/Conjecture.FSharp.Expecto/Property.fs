// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

module Conjecture.FSharp.Expecto

open Expecto
open Conjecture

let propertyWithRepro (name: string) (test: 'a -> 'r) (repro: ReproExport) : Test =
    testCase name (fun () ->
        let handleResult result =
            match result with
            | PropertyResult.Passed -> ()
            | PropertyResult.Failed msg -> failwith msg
        let returnType = typeof<'r>
        if returnType = typeof<bool> then
            let boolTest = fun a -> test a |> box |> unbox<bool>
            PropertyRunner.runBoolWithRepro (Gen.auto<'a> ()) boolTest repro |> Async.AwaitTask |> Async.RunSynchronously |> handleResult
        elif returnType = typeof<unit> then
            let unitTest = test >> ignore
            PropertyRunner.runUnitWithRepro (Gen.auto<'a> ()) unitTest repro |> Async.AwaitTask |> Async.RunSynchronously |> handleResult
        else
            failwithf "property: unsupported return type '%s'. Use 'a -> bool or 'a -> unit." typeof<'r>.Name)

let property (name: string) (test: 'a -> 'r) : Test =
    propertyWithRepro name test ReproExport.disabled
