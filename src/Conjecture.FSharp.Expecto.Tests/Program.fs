// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

module Conjecture.FSharp.Expecto.Tests.Program

open Expecto

[<EntryPoint>]
let main argv =
    let combined = testList "all" [ ExpectoPropertyTests.tests; ReproExportIntegrationTests.tests ]
    runTestsWithCLIArgs [] argv combined
