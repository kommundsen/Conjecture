// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Generic;

namespace Conjecture.Core.Internal;

internal record ReproContext(
    string TestClassName,
    string MethodName,
    bool IsAsync,
    IEnumerable<(string Name, object? Value, Type Type)> Parameters,
    ulong Seed,
    int ExampleCount,
    int ShrinkCount,
    TestFramework Framework,
    DateTimeOffset GeneratedAt);
