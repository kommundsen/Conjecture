// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

module Conjecture.FSharp.Expecto.Tests.ReproExportIntegrationTests

open System
open System.IO
open Expecto
open Conjecture
open Conjecture.FSharp.Expecto

let private runTest (t: Test) : int =
    runTestsWithCLIArgs [] [||] t

let tests =
    testList "Conjecture.FSharp.Expecto.repro-export" [

        testCase "propertyWithRepro writes a .cs file when ExportOnFailure is true and property fails" <| fun () ->
            let tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName())
            Directory.CreateDirectory(tempDir) |> ignore
            try
                let repro = { ExportOnFailure = true; OutputPath = tempDir }
                let t = propertyWithRepro "fails" (fun (x: int) -> x < -1_000_000_000) repro
                let _ = try runTest t with _ -> 1
                let files = Directory.GetFiles(tempDir, "*.cs")
                Expect.isNonEmpty files "expected at least one .cs repro file"
            finally
                try Directory.Delete(tempDir, true) with _ -> ()

        testCase "propertyWithRepro writes no file when ExportOnFailure is false" <| fun () ->
            let tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName())
            Directory.CreateDirectory(tempDir) |> ignore
            try
                let repro = { ExportOnFailure = false; OutputPath = tempDir }
                let t = propertyWithRepro "fails" (fun (x: int) -> x < -1_000_000_000) repro
                let _ = try runTest t with _ -> 1
                let files = Directory.GetFiles(tempDir, "*.cs")
                Expect.isEmpty files "expected no repro file when export disabled"
            finally
                try Directory.Delete(tempDir, true) with _ -> ()
    ]
