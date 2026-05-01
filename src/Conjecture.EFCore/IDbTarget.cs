// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Threading;
using System.Threading.Tasks;


using Microsoft.EntityFrameworkCore;

using Conjecture.Abstractions.Interactions;

namespace Conjecture.EFCore;

/// <summary>EF Core-specific interaction target exposing a <see cref="DbContext"/> for the given resource.</summary>
public interface IDbTarget : IInteractionTarget
{
    /// <summary>Gets the resource name this target is registered under.</summary>
    string ResourceName { get; }

    /// <summary>Returns a <see cref="DbContext"/> configured for the given resource name.</summary>
    DbContext ResolveContext(string resourceName);

    /// <summary>Resets the database for the given resource name (e.g. drops and recreates).</summary>
    Task ResetAsync(string resourceName, CancellationToken ct = default);
}