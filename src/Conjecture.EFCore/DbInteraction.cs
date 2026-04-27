// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Interactions;

namespace Conjecture.EFCore;

/// <summary>Represents a database operation flowing through the Layer 1 interactions framework.</summary>
/// <param name="ResourceName">Logical name of the database resource being targeted.</param>
/// <param name="Op">The kind of database operation.</param>
/// <param name="Payload">Optional entity or value associated with the operation. May be null for operations like <see cref="DbOpKind.SaveChanges"/>.</param>
public sealed record DbInteraction(
    string ResourceName,
    DbOpKind Op,
    object? Payload) : IAddressedInteraction;