// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Microsoft.EntityFrameworkCore;

namespace Conjecture.EFCore;

/// <summary>Extension methods on <see cref="IDbTarget"/>.</summary>
public static class IDbTargetExtensions
{
    /// <summary>
    /// Resolves the target's <see cref="DbContext"/> and casts to <typeparamref name="TContext"/>.
    /// Throws <see cref="InvalidOperationException"/> if the resolved context is not assignable to
    /// <typeparamref name="TContext"/>.
    /// </summary>
    public static TContext Resolve<TContext>(this IDbTarget target)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(target);
        DbContext ctx = target.ResolveContext(target.ResourceName);
        if (ctx is not TContext typed)
        {
            ctx.Dispose();
            throw new InvalidOperationException(
                FormattableString.Invariant($"IDbTarget '{target.ResourceName}' resolved a {ctx.GetType().Name}, not a {typeof(TContext).Name}."));
        }
        return typed;
    }
}