// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Collections.Generic;

namespace Conjecture.EFCore;

/// <summary>A point-in-time snapshot of entity counts and primary keys captured from a <see cref="IDbTarget"/>.</summary>
public sealed record EntitySnapshot(
    IReadOnlyDictionary<Type, int> Counts,
    IReadOnlyDictionary<Type, IReadOnlySet<object>> Keys);