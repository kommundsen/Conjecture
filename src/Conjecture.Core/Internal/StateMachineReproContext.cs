// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Collections.Generic;

namespace Conjecture.Core.Internal;

internal record StateMachineReproContext(
    string TestClassName,
    string MethodName,
    string SutTypeName,
    IReadOnlyList<(string Label, object? State, Type StateType)> Commands,
    string? ViolatedInvariant,
    ulong Seed,
    int ExampleCount,
    int ShrinkCount,
    TestFramework Framework,
    DateTimeOffset GeneratedAt);