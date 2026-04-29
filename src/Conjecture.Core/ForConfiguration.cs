// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Conjecture.Core;

/// <summary>Configures property overrides for <see cref="Strategy.For{T}(System.Action{ForConfiguration{T}})"/>.</summary>
public sealed class ForConfiguration<T>
{
    private readonly Dictionary<string, object> overrides = [];

    /// <summary>Overrides the strategy for the property selected by <paramref name="selector"/>.</summary>
    public ForConfiguration<T> Override<TProp>(Expression<Func<T, TProp>> selector, Strategy<TProp> strategy)
    {
        ArgumentNullException.ThrowIfNull(selector);
        ArgumentNullException.ThrowIfNull(strategy);

        if (selector.Body is not MemberExpression memberExpr)
        {
            throw new InvalidOperationException("Selector must be a member expression.");
        }

        overrides[memberExpr.Member.Name] = strategy;
        return this;
    }

    /// <summary>Infrastructure — called by source-generated <c>CreateWithOverrides</c> methods. Returns the override strategy for <paramref name="propertyName"/>, or <see langword="null"/> if not overridden.</summary>
    public Strategy<TProp>? TryGet<TProp>(string propertyName)
    {
        return overrides.TryGetValue(propertyName, out object? value) ? (Strategy<TProp>)value : null;
    }
}