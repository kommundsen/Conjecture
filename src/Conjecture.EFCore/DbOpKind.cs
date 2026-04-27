// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

namespace Conjecture.EFCore;

/// <summary>Classifies the kind of database operation represented by a <see cref="DbInteraction"/>.</summary>
public enum DbOpKind
{
    /// <summary>An entity is being added to the context.</summary>
    Add,

    /// <summary>An entity is being updated in the context.</summary>
    Update,

    /// <summary>An entity is being removed from the context.</summary>
    Remove,

    /// <summary>Changes are being persisted to the database.</summary>
    SaveChanges,

    /// <summary>Data is being queried from the database.</summary>
    Query,
}