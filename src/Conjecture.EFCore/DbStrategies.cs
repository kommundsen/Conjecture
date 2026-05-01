// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;

using Conjecture.Core;

using Microsoft.EntityFrameworkCore.Metadata;

namespace Conjecture.EFCore;

/// <summary>Single-op factory access object returned by <c>Strategy.Db</c> for composing DB interaction strategies.</summary>
public sealed class DbStrategies
{
    internal DbStrategies()
    {
    }

    /// <summary>Returns a strategy that generates an <see cref="DbOpKind.Add"/> <see cref="DbInteraction"/> for <typeparamref name="T"/>.</summary>
    public Strategy<DbInteraction> Add<T>(string resourceName, IModel model, int maxDepth = 2) where T : class
    {
        ArgumentException.ThrowIfNullOrEmpty(resourceName);
        ArgumentNullException.ThrowIfNull(model);

        Strategy<T> entityStrategy = new EntityStrategyBuilder(model).WithMaxDepth(maxDepth).Build<T>();
        return entityStrategy.Select(entity => new DbInteraction(resourceName, DbOpKind.Add, entity));
    }

    /// <summary>Returns a strategy that generates an <see cref="DbOpKind.Update"/> <see cref="DbInteraction"/> for <typeparamref name="T"/>.</summary>
    public Strategy<DbInteraction> Update<T>(string resourceName, IModel model, int maxDepth = 2) where T : class
    {
        ArgumentException.ThrowIfNullOrEmpty(resourceName);
        ArgumentNullException.ThrowIfNull(model);

        Strategy<T> entityStrategy = new EntityStrategyBuilder(model).WithMaxDepth(maxDepth).Build<T>();
        return entityStrategy.Select(entity => new DbInteraction(resourceName, DbOpKind.Update, entity));
    }

    /// <summary>Returns a strategy that generates a <see cref="DbOpKind.Remove"/> <see cref="DbInteraction"/> for <typeparamref name="T"/>.</summary>
    public Strategy<DbInteraction> Remove<T>(string resourceName, IModel model) where T : class
    {
        ArgumentException.ThrowIfNullOrEmpty(resourceName);
        ArgumentNullException.ThrowIfNull(model);

        Strategy<T> entityStrategy = new EntityStrategyBuilder(model).Build<T>();
        return entityStrategy.Select(entity => new DbInteraction(resourceName, DbOpKind.Remove, entity));
    }

    /// <summary>Returns a strategy that always produces a <see cref="DbOpKind.SaveChanges"/> <see cref="DbInteraction"/> with null payload.</summary>
    public Strategy<DbInteraction> SaveChanges(string resourceName)
    {
        ArgumentException.ThrowIfNullOrEmpty(resourceName);

        return Strategy.Just(new DbInteraction(resourceName, DbOpKind.SaveChanges, null));
    }

    /// <summary>
    /// Returns a fluent <see cref="DbInteractionSequenceBuilder"/> for composing a sequence of
    /// <see cref="DbInteraction"/> records generated uniformly from Add, Update, Remove, and SaveChanges
    /// across all entity types in <paramref name="model"/>.
    /// </summary>
    public DbInteractionSequenceBuilder Sequence(string resourceName, IModel model)
    {
        ArgumentException.ThrowIfNullOrEmpty(resourceName);
        ArgumentNullException.ThrowIfNull(model);

        return new DbInteractionSequenceBuilder(resourceName, model);
    }
}
