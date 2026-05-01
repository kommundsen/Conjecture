// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

module Conjecture.FSharp.Tests.ReproExportIntegrationTests

open System
open System.IO
open Xunit
open Conjecture
open Conjecture.Core

[<Fact>]
let ``runBoolWithRepro failing property writes a .cs repro file when ExportOnFailure is true`` () =
    task {
        let tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName())
        Directory.CreateDirectory(tempDir) |> ignore
        try
            let repro = { ExportOnFailure = true; OutputPath = tempDir }
            let genWrapped = Gen.int (0, 100)
            let! result = PropertyRunner.runBoolWithRepro genWrapped (fun n -> n < 5) repro
            match result with
            | PropertyResult.Failed _ ->
                let files = Directory.GetFiles(tempDir, "*.cs")
                Assert.NotEmpty(files)
            | PropertyResult.Passed ->
                Assert.Fail("Expected property to fail")
        finally
            try Directory.Delete(tempDir, true) with _ -> ()
    }

[<Fact>]
let ``runBoolWithRepro failing property writes no file when ExportOnFailure is false`` () =
    task {
        let tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName())
        Directory.CreateDirectory(tempDir) |> ignore
        try
            let repro = { ExportOnFailure = false; OutputPath = tempDir }
            let genWrapped = Gen.int (0, 100)
            let! result = PropertyRunner.runBoolWithRepro genWrapped (fun n -> n < 5) repro
            match result with
            | PropertyResult.Failed _ ->
                let files = Directory.GetFiles(tempDir, "*.cs")
                Assert.Empty(files)
            | PropertyResult.Passed ->
                Assert.Fail("Expected property to fail")
        finally
            try Directory.Delete(tempDir, true) with _ -> ()
    }

[<Fact>]
let ``runUnitWithRepro failing property writes a .cs repro file when ExportOnFailure is true`` () =
    task {
        let tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName())
        Directory.CreateDirectory(tempDir) |> ignore
        try
            let repro = { ExportOnFailure = true; OutputPath = tempDir }
            let genWrapped = Gen.int (0, 100)
            let! result =
                PropertyRunner.runUnitWithRepro genWrapped (fun n ->
                    if n >= 5 then raise (Exception("fail"))) repro
            match result with
            | PropertyResult.Failed _ ->
                let files = Directory.GetFiles(tempDir, "*.cs")
                Assert.NotEmpty(files)
            | PropertyResult.Passed ->
                Assert.Fail("Expected property to fail")
        finally
            try Directory.Delete(tempDir, true) with _ -> ()
    }

[<Fact>]
let ``ReproExport disabled defaults are sensible`` () =
    Assert.False(ReproExport.disabled.ExportOnFailure)
    Assert.Equal(".conjecture/repros/", ReproExport.disabled.OutputPath)
