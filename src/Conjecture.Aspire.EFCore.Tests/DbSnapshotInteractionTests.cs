// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Threading.Tasks;

using Conjecture.Aspire;
using Conjecture.Aspire.EFCore;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Conjecture.Aspire.EFCore.Tests;

/// <summary>
/// Tests for <see cref="DbSnapshotInteraction"/> and its dispatch through
/// <see cref="AspireDbTarget{TContext}"/> and <see cref="InteractionTraceReporter"/>.
/// </summary>
public sealed class DbSnapshotInteractionTests : IAsyncLifetime
{
    private SqliteConnection connection = null!;

    public async Task InitializeAsync()
    {
        connection = new("DataSource=:memory:");
        await connection.OpenAsync();

        DbContextOptions<SnapshotDbContext> opts = SharedOpts();
        await using SnapshotDbContext seed = new(opts);
        await seed.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await connection.DisposeAsync();
    }

    private DbContextOptions<SnapshotDbContext> SharedOpts() =>
        new DbContextOptionsBuilder<SnapshotDbContext>().UseSqlite(connection).Options;

    private async Task<AspireDbTarget<SnapshotDbContext>> CreateTargetAsync()
    {
        return await AspireDbTarget<SnapshotDbContext>.CreateAsync(
            (_, _) => Task.FromResult<string?>(connection.ConnectionString),
            "snapshot-db",
            _ => new SnapshotDbContext(SharedOpts()));
    }

    // -----------------------------------------------------------------------
    // DbSnapshotInteraction — executes capture and returns value
    // -----------------------------------------------------------------------

    [Fact]
    public async Task DbSnapshotInteraction_executes_capture_and_returns_value()
    {
        DbSnapshotInteraction snapshot = new(
            ResourceName: "snapshot-db",
            Label: "row-count",
            Capture: static async ctx => (object?)await ctx.Set<SnapshotRow>().CountAsync());

        SnapshotDbContext ctx = new(SharedOpts());
        await using (ctx)
        {
            object? result = await snapshot.Capture(ctx);
            Assert.Equal(0, result);
        }
    }

    // -----------------------------------------------------------------------
    // AspireDbTarget — dispatches DbSnapshotInteraction via ExecuteAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task AspireDbTarget_dispatches_DbSnapshotInteraction_via_ExecuteAsync()
    {
        AspireDbTarget<SnapshotDbContext> target = await CreateTargetAsync();
        await using (target)
        {
            DbSnapshotInteraction snapshot = new(
                ResourceName: "snapshot-db",
                Label: "count",
                Capture: static async ctx => (object?)await ctx.Set<SnapshotRow>().CountAsync());

            object? result = await target.ExecuteAsync(snapshot, default);
            Assert.Equal(0, result);
        }
    }

    // -----------------------------------------------------------------------
    // AspireDbTarget — throws when resource name mismatches
    // -----------------------------------------------------------------------

    [Fact]
    public async Task AspireDbTarget_DbSnapshot_throws_when_resource_name_mismatches()
    {
        AspireDbTarget<SnapshotDbContext> target = await CreateTargetAsync();
        await using (target)
        {
            DbSnapshotInteraction snapshot = new(
                ResourceName: "wrong-db",
                Label: "count",
                Capture: static async ctx => (object?)await ctx.Set<SnapshotRow>().CountAsync());

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => target.ExecuteAsync(snapshot, default));
        }
    }

    // -----------------------------------------------------------------------
    // InteractionTraceReporter — records snapshot label and value
    // -----------------------------------------------------------------------

    [Fact]
    public void InteractionTraceReporter_includes_snapshot_label_and_value_in_trace_output()
    {
        InteractionTraceReporter reporter = new();

        DbSnapshotInteraction snapshot = new(
            ResourceName: "snapshot-db",
            Label: "row-count",
            Capture: static ctx => Task.FromResult<object?>(42));

        reporter.RecordSnapshot(snapshot, capturedValue: 42);

        string report = reporter.FormatReport(app: null);

        Assert.Contains("row-count", report, StringComparison.Ordinal);
        Assert.Contains("42", report, StringComparison.Ordinal);
    }

    // -----------------------------------------------------------------------
    // Nested helpers
    // -----------------------------------------------------------------------

    internal sealed class SnapshotDbContext(DbContextOptions<SnapshotDbContext> opts) : DbContext(opts)
    {
        public DbSet<SnapshotRow> Rows => Set<SnapshotRow>();
    }

    internal sealed class SnapshotRow
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }
}