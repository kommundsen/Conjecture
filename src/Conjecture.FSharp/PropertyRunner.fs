// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

namespace Conjecture

open System.Collections.Generic
open System.Threading.Tasks
open Conjecture.Core
open Conjecture.Core.Internal

/// Result of running a property test via <see cref="PropertyRunner"/>.
[<RequireQualifiedAccess>]
type PropertyResult =
    | Passed
    | Failed of message: string

module PropertyRunner =
    let private defaultSettings = ConjectureSettings()

    let private buildMessage (gen: Gen<'a>) (nodes: IReadOnlyList<IRNode>) (seed: uint64) (exampleCount: int) (shrinkCount: int) : string =
        let replay = ConjectureData.ForRecord(nodes)
        let value = (Gen.unwrap gen).Generate(replay)
        let formatted = FSharpFormatter.format (value :> obj)
        sprintf "Falsifying example found after %d examples (shrunk %d times)\nCounterexample: %s\nReproduce with: [Property(Seed = 0x%X)]" exampleCount shrinkCount formatted seed

    /// Runs a bool-returning property. <c>false</c> is treated as a test failure.
    let runBool (gen: Gen<'a>) (test: 'a -> bool) : Task<PropertyResult> =
        task {
            let strategy = Gen.unwrap gen
            let! result =
                TestRunner.Run(defaultSettings, fun (data: ConjectureData) ->
                    let value = strategy.Generate(data)
                    if not (test value) then
                        raise (System.Exception("Property returned false"))
                )
            if result.Passed then
                return PropertyResult.Passed
            else
                match result.Counterexample with
                | null -> return PropertyResult.Failed "Property failed (counterexample unavailable)"
                | nodes ->
                    let msg = buildMessage gen nodes result.Seed.Value result.ExampleCount result.ShrinkCount
                    return PropertyResult.Failed msg
        }

    /// Runs a unit-returning property. Any exception thrown is treated as a test failure.
    let runUnit (gen: Gen<'a>) (test: 'a -> unit) : Task<PropertyResult> =
        task {
            let strategy = Gen.unwrap gen
            let! result =
                TestRunner.Run(defaultSettings, fun (data: ConjectureData) ->
                    let value = strategy.Generate(data)
                    test value
                )
            if result.Passed then
                return PropertyResult.Passed
            else
                match result.Counterexample with
                | null -> return PropertyResult.Failed "Property failed (counterexample unavailable)"
                | nodes ->
                    let msg = buildMessage gen nodes result.Seed.Value result.ExampleCount result.ShrinkCount
                    return PropertyResult.Failed msg
        }
