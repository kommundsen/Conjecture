// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

using Conjecture.Core;

using Microsoft.EntityFrameworkCore.Metadata;

namespace Conjecture.EFCore;

/// <summary>Builds a <see cref="Strategy{T}"/> for a complete EF Core entity graph.</summary>
public sealed class EntityStrategyBuilder
{
    private readonly IModel model;
    private int maxDepth = 2;
    private readonly HashSet<(Type, string)> excludedNavigations = [];

    /// <summary>Initializes a new <see cref="EntityStrategyBuilder"/> with the given EF Core model.</summary>
    public EntityStrategyBuilder(IModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        this.model = model;
    }

    /// <summary>Sets the maximum navigation depth. Default is 2.</summary>
    public EntityStrategyBuilder WithMaxDepth(int depth)
    {
        maxDepth = depth;
        return this;
    }

    /// <summary>Excludes the navigation identified by <paramref name="navigation"/> from generation.</summary>
    public EntityStrategyBuilder WithoutNavigation<TEntity>(Expression<Func<TEntity, object?>> navigation) where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(navigation);
        string propertyName = ExtractMemberName(navigation);
        excludedNavigations.Add((typeof(TEntity), propertyName));
        return this;
    }

    /// <summary>Builds a <see cref="Strategy{TEntity}"/> that generates entity graphs up to the configured depth.</summary>
    public Strategy<TEntity> Build<TEntity>() where TEntity : class
    {
        IEntityType entityType = model.FindEntityType(typeof(TEntity))
            ?? throw new InvalidOperationException(
                $"Entity type '{typeof(TEntity).FullName}' is not registered in the EF Core model.");

        HashSet<IEntityType> visitedPath = [];
        return BuildStrategy<TEntity>(entityType, currentDepth: 0, visitedPath);
    }

    private Strategy<TEntity> BuildStrategy<TEntity>(
        IEntityType entityType,
        int currentDepth,
        HashSet<IEntityType> visitedPath) where TEntity : class
    {
        return Generate.Compose<TEntity>(ctx =>
        {
            TEntity entity = (TEntity)Activator.CreateInstance(typeof(TEntity))!;
            PopulateEntity(ctx, entity, entityType, currentDepth, visitedPath);
            return entity;
        });
    }

    private void PopulateEntity(
        IGeneratorContext ctx,
        object entity,
        IEntityType entityType,
        int currentDepth,
        HashSet<IEntityType> visitedPath)
    {
        foreach (IProperty property in entityType.GetProperties())
        {
            object? value = ctx.Generate(PropertyStrategyBuilder.Build(property));
            property.PropertyInfo?.SetValue(entity, value);
        }

        HashSet<IEntityType> newPath = [.. visitedPath, entityType];

        foreach (INavigation nav in entityType.GetNavigations())
        {
            PopulateNavigation(ctx, entity, nav, currentDepth, newPath);
        }
    }

    private void PopulateNavigation(
        IGeneratorContext ctx,
        object entity,
        INavigation nav,
        int currentDepth,
        HashSet<IEntityType> visitedPath)
    {
        PropertyInfo? navProp = nav.PropertyInfo;
        if (navProp is null)
        {
            return;
        }

        bool isOwned = nav.ForeignKey.IsOwnership;
        bool isExcluded = excludedNavigations.Contains((entity.GetType(), nav.Name));

        if (isExcluded)
        {
            SetNavigationDefault(navProp, entity, nav);
            return;
        }

        IEntityType targetType = nav.TargetEntityType;
        bool cycleDetected = visitedPath.Contains(targetType);

        if (isOwned)
        {
            object? owned = Activator.CreateInstance(targetType.ClrType);
            if (owned is not null)
            {
                HashSet<IEntityType> ownedPath = [.. visitedPath, targetType];
                PopulateEntity(ctx, owned, targetType, currentDepth, ownedPath);
            }
            navProp.SetValue(entity, owned);
            return;
        }

        if (cycleDetected || currentDepth >= maxDepth)
        {
            SetNavigationDefault(navProp, entity, nav);
            return;
        }

        if (nav.IsCollection)
        {
            object? collection = Activator.CreateInstance(navProp.PropertyType);
            if (collection is null)
            {
                return;
            }

            int count = ctx.Generate(Generate.Integers<int>(0, 3));
            MethodInfo? addMethod = navProp.PropertyType.GetMethod("Add");
            if (addMethod is not null)
            {
                HashSet<IEntityType> childPath = [.. visitedPath, targetType];
                for (int i = 0; i < count; i++)
                {
                    object? child = BuildEntityInstance(ctx, targetType, currentDepth + 1, childPath);
                    if (child is not null)
                    {
                        addMethod.Invoke(collection, [child]);
                    }
                }
            }
            navProp.SetValue(entity, collection);
        }
        else
        {
            if (!nav.ForeignKey.IsRequired)
            {
                navProp.SetValue(entity, null);
                return;
            }

            HashSet<IEntityType> childPath = [.. visitedPath, targetType];
            object? child = BuildEntityInstance(ctx, targetType, currentDepth + 1, childPath);
            navProp.SetValue(entity, child);
        }
    }

    private object? BuildEntityInstance(
        IGeneratorContext ctx,
        IEntityType entityType,
        int currentDepth,
        HashSet<IEntityType> visitedPath)
    {
        object? entity = Activator.CreateInstance(entityType.ClrType);
        if (entity is null)
        {
            return null;
        }
        PopulateEntity(ctx, entity, entityType, currentDepth, visitedPath);
        return entity;
    }

    private static void SetNavigationDefault(PropertyInfo navProp, object entity, INavigation nav)
    {
        if (nav.IsCollection)
        {
            object? emptyCollection = Activator.CreateInstance(navProp.PropertyType);
            navProp.SetValue(entity, emptyCollection);
        }
        else
        {
            navProp.SetValue(entity, null);
        }
    }

    private static string ExtractMemberName<TEntity>(Expression<Func<TEntity, object?>> expression)
    {
        Expression body = expression.Body;
        if (body is UnaryExpression unary && unary.NodeType == ExpressionType.Convert)
        {
            body = unary.Operand;
        }
        return body is MemberExpression member
            ? member.Member.Name
            : throw new ArgumentException("Expression must be a property access expression.", nameof(expression));
    }
}