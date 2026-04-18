// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

namespace Conjecture

open Microsoft.FSharp.Reflection

module FSharpFormatter =
    let private isFSharpType (t: System.Type) =
        FSharpType.IsRecord(t) || FSharpType.IsUnion(t) || FSharpType.IsTuple(t)

    let format (value: obj) : string =
        if isFSharpType (value.GetType()) then sprintf "%A" value
        else
            match value.ToString() with
            | null -> value.GetType().Name
            | s -> s
