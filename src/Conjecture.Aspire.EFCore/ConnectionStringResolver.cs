// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Threading;
using System.Threading.Tasks;

namespace Conjecture.Aspire.EFCore;

/// <summary>
/// Resolves a connection string for a named Aspire resource.
/// </summary>
/// <param name="resourceName">The name of the Aspire resource.</param>
/// <param name="ct">Cancellation token.</param>
/// <returns>The connection string, or <see langword="null"/> if the resource is unknown.</returns>
public delegate Task<string?> ConnectionStringResolver(string resourceName, CancellationToken ct);