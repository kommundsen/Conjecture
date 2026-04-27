// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Conjecture.EFCore;

/// <summary>Asserts migration Up/Down symmetry for a given <see cref="DbContext"/>.</summary>
public static class MigrationHarness
{
    /// <summary>
    /// Applies all pending migrations to head, rolls back the latest one, re-applies it,
    /// and asserts that the resulting schema is identical to the schema before the rollback.
    /// </summary>
    public static async Task AssertUpDownIdempotentAsync(
        DbContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        IReadOnlyList<string> all = context.Database.GetMigrations().ToList();

        if (all.Count == 0)
        {
            throw new InvalidOperationException("No migrations defined for this context.");
        }

        IMigrator migrator = context.GetService<IMigrator>();

        await migrator.MigrateAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

        string snapshot1 = await CaptureSchemaAsync(context, cancellationToken).ConfigureAwait(false);

        string rollbackTarget = all.Count == 1
            ? Migration.InitialDatabase
            : all[^2];

        await migrator.MigrateAsync(rollbackTarget, cancellationToken).ConfigureAwait(false);

        try
        {
            await migrator.MigrateAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new MigrationAssertionException(
                FormattableString.Invariant(
                    $"Migration Up/Down is not idempotent: re-applying Up failed after Down.{Environment.NewLine}{ex.Message}"),
                ex);
        }

        string snapshot2 = await CaptureSchemaAsync(context, cancellationToken).ConfigureAwait(false);

        if (snapshot1 != snapshot2)
        {
            throw new MigrationAssertionException(
                FormattableString.Invariant(
                    $"Migration Up/Down is not idempotent.{Environment.NewLine}Schema before rollback:{Environment.NewLine}{snapshot1}{Environment.NewLine}Schema after re-apply:{Environment.NewLine}{snapshot2}"));
        }
    }

    private static async Task<string> CaptureSchemaAsync(
        DbContext context,
        CancellationToken cancellationToken)
    {
        DbConnection conn = context.Database.GetDbConnection();
        bool shouldClose = conn.State != ConnectionState.Open;

        if (shouldClose)
        {
            await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
        }

        try
        {
            await using DbCommand cmd = conn.CreateCommand();
            cmd.CommandText =
                "SELECT name, type, sql FROM sqlite_master " +
                "WHERE type IN ('table','index','view') " +
                "AND name NOT LIKE 'sqlite_%' " +
                "AND name NOT LIKE '__EFMigrations%' " +
                "ORDER BY type, name";

            await using DbDataReader reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            StringBuilder sb = new();

            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                string name = reader.GetString(0);
                string type = reader.GetString(1);
                string sql = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
                sb.Append(type).Append('|').Append(name).Append('|').AppendLine(sql);
            }

            return sb.ToString();
        }
        finally
        {
            if (shouldClose)
            {
                await conn.CloseAsync().ConfigureAwait(false);
            }
        }
    }
}