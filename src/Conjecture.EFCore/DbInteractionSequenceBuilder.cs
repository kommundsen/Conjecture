// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using Conjecture.Core;

using Microsoft.EntityFrameworkCore.Metadata;

namespace Conjecture.EFCore;

/// <summary>
/// Fluent builder that produces a strategy for a sequence of <see cref="DbInteraction"/> records.
/// Entry point: <c>Strategy.Db.Sequence(resourceName, model)</c>. Terminal: <see cref="Build"/>.
/// </summary>
public sealed class DbInteractionSequenceBuilder
{
    private readonly string resourceName;
    private readonly IModel model;
    private readonly int min;
    private readonly int max;

    internal DbInteractionSequenceBuilder(string resourceName, IModel model)
        : this(resourceName, model, min: 1, max: 5)
    {
    }

    private DbInteractionSequenceBuilder(string resourceName, IModel model, int min, int max)
    {
        this.resourceName = resourceName;
        this.model = model;
        this.min = min;
        this.max = max;
    }

    /// <summary>Sets the inclusive range of the generated sequence length.</summary>
    public DbInteractionSequenceBuilder WithLength(int min, int max)
    {
        return new DbInteractionSequenceBuilder(this.resourceName, this.model, min, max);
    }

    /// <summary>Builds a <see cref="Strategy{T}"/> producing sequences of <see cref="DbInteraction"/>.</summary>
    public Strategy<IReadOnlyList<DbInteraction>> Build()
    {
        IReadOnlyList<IEntityType> entityTypes = this.model.GetEntityTypes().ToList();

        List<Strategy<DbInteraction>> candidates = [];
        foreach (IEntityType entityType in entityTypes)
        {
            Strategy<object> entityStrategy = BuildObjectStrategy(entityType, this.model);
            candidates.Add(entityStrategy.Select(entity => new DbInteraction(this.resourceName, DbOpKind.Add, entity)));
            candidates.Add(entityStrategy.Select(entity => new DbInteraction(this.resourceName, DbOpKind.Update, entity)));
            candidates.Add(entityStrategy.Select(entity => new DbInteraction(this.resourceName, DbOpKind.Remove, entity)));
        }
        candidates.Add(Strategy.Just(new DbInteraction(this.resourceName, DbOpKind.SaveChanges, null)));

        Strategy<DbInteraction> elementStrategy = Strategy.OneOf([.. candidates]);
        return Strategy.Lists(elementStrategy, this.min, this.max)
            .Select(static list => (IReadOnlyList<DbInteraction>)list);
    }

    private static readonly MethodInfo BuildGenericMethod =
        typeof(DbInteractionSequenceBuilder).GetMethod(nameof(BuildTypedObjectStrategy), BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("BuildTypedObjectStrategy not found.");

    private static Strategy<object> BuildObjectStrategy(IEntityType entityType, IModel model)
    {
        // Build iterates model.GetEntityTypes() at runtime, so the entity type is not statically known — reflection bridges to the generic Build<T>.
        MethodInfo generic = BuildGenericMethod.MakeGenericMethod(entityType.ClrType);
        return (Strategy<object>)(generic.Invoke(null, [model])
            ?? throw new InvalidOperationException($"BuildTypedObjectStrategy<{entityType.ClrType.Name}> returned null."));
    }

    private static Strategy<object> BuildTypedObjectStrategy<T>(IModel model) where T : class
    {
        Strategy<T> inner = new EntityStrategyBuilder(model).Build<T>();
        return inner.Select(static entity => (object)entity);
    }
}
