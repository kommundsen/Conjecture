// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using Conjecture.Core;

using Microsoft.EntityFrameworkCore.Metadata;

namespace Conjecture.EFCore;

/// <summary>Fluent builder returned by <c>Generate.Db</c> for composing DB interaction strategies.</summary>
public sealed class GenerateDbBlock
{
    internal GenerateDbBlock()
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

        return Generate.Constant(new DbInteraction(resourceName, DbOpKind.SaveChanges, null));
    }

    /// <summary>
    /// Returns a strategy producing a list of <see cref="DbInteraction"/> records with length in [<paramref name="min"/>, <paramref name="max"/>].
    /// Each element is drawn uniformly from Add, Update, Remove, and SaveChanges operations across all entity types in the model.
    /// </summary>
    public Strategy<IReadOnlyList<DbInteraction>> Sequence(
        string resourceName,
        IModel model,
        int min = 1,
        int max = 5)
    {
        ArgumentException.ThrowIfNullOrEmpty(resourceName);
        ArgumentNullException.ThrowIfNull(model);

        IReadOnlyList<IEntityType> entityTypes = model.GetEntityTypes().ToList();

        List<Strategy<DbInteraction>> candidates = [];
        foreach (IEntityType entityType in entityTypes)
        {
            Strategy<object> entityStrategy = BuildObjectStrategy(entityType, model);
            candidates.Add(entityStrategy.Select(entity => new DbInteraction(resourceName, DbOpKind.Add, entity)));
            candidates.Add(entityStrategy.Select(entity => new DbInteraction(resourceName, DbOpKind.Update, entity)));
            candidates.Add(entityStrategy.Select(entity => new DbInteraction(resourceName, DbOpKind.Remove, entity)));
        }
        candidates.Add(Generate.Constant(new DbInteraction(resourceName, DbOpKind.SaveChanges, null)));

        Strategy<DbInteraction> elementStrategy = Generate.OneOf([.. candidates]);
        return Generate.Lists(elementStrategy, min, max)
            .Select(static list => (IReadOnlyList<DbInteraction>)list);
    }

    private static readonly MethodInfo BuildGenericMethod =
        typeof(GenerateDbBlock).GetMethod(nameof(BuildTypedObjectStrategy), BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("BuildTypedObjectStrategy not found.");

    private static Strategy<object> BuildObjectStrategy(IEntityType entityType, IModel model)
    {
        // Sequence iterates model.GetEntityTypes() at runtime, so the entity type is not statically known — reflection bridges to the generic Build<T>.
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

/// <summary>Extension methods on <see cref="Generate"/> for database interaction generation.</summary>
public static class DbGenerateExtensions
{
    extension(Generate)
    {
        /// <summary>Returns a <see cref="GenerateDbBlock"/> for composing database interaction strategies.</summary>
        public static GenerateDbBlock Db => new();
    }
}