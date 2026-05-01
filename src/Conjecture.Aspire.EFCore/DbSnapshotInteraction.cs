// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Threading.Tasks;

using Conjecture.Interactions;

using Microsoft.EntityFrameworkCore;

using Conjecture.Abstractions.Interactions;

namespace Conjecture.Aspire.EFCore;

/// <summary>
/// An addressed interaction that captures a DB snapshot by running <see cref="Capture"/>
/// against the resolved <see cref="DbContext"/>.
/// </summary>
/// <param name="ResourceName">Logical name of the database resource.</param>
/// <param name="Label">Human-readable label for the snapshot (used in trace output).</param>
/// <param name="Capture">Async function that reads from the context and returns an observable value.</param>
public readonly record struct DbSnapshotInteraction(
    string ResourceName,
    string Label,
    Func<DbContext, Task<object?>> Capture) : IAddressedInteraction, Conjecture.Aspire.ISnapshotLabel;