// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;

using Conjecture.Aspire;

namespace Conjecture.Aspire.EFCore;

/// <summary>Extension methods on <see cref="IAspireAppFixture"/> for EF Core target registration.</summary>
public static class AspireDbFixtureExtensions
{
    /// <summary>
    /// Creates an empty <see cref="AspireDbTargetRegistry"/> scoped to this fixture's lifecycle.
    /// </summary>
    public static AspireDbTargetRegistry CreateDbRegistry(this IAspireAppFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        return new();
    }
}
