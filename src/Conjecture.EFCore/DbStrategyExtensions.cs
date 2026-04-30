// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;

using Conjecture.Core;

using Microsoft.EntityFrameworkCore;

namespace Conjecture.EFCore;

/// <summary>Extension methods for generating EF Core entity instances.</summary>
public static class DbStrategyExtensions
{
    // Both overloads take optional maxDepth; suppress RS0026 as in RegexStrategyExtensions.
#pragma warning disable RS0026
    extension(Strategy)
    {
        /// <summary>Returns a strategy that generates instances of <typeparamref name="T"/> using the given <paramref name="context"/>.</summary>
        public static Strategy<T> Entity<T>(DbContext context, int maxDepth = 2) where T : class
        {
            ArgumentNullException.ThrowIfNull(context);
            return new EntityStrategyBuilder(context.Model)
                .WithMaxDepth(maxDepth)
                .Build<T>();
        }

        /// <summary>Returns a strategy that generates instances of <typeparamref name="T"/> using a fresh <see cref="DbContext"/> per example.</summary>
        public static Strategy<T> Entity<T>(Func<DbContext> contextFactory, int maxDepth = 2) where T : class
        {
            ArgumentNullException.ThrowIfNull(contextFactory);
            return Strategy.Compose<T>(ctx =>
            {
                using DbContext context = contextFactory();
                Strategy<T> inner = new EntityStrategyBuilder(context.Model)
                    .WithMaxDepth(maxDepth)
                    .Build<T>();
                return ctx.Generate(inner);
            });
        }

        /// <summary>Returns a <see cref="DbStrategies"/> for composing database interaction strategies.</summary>
        public static DbStrategies Db => new();
    }
#pragma warning restore RS0026
}