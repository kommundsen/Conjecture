// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Conjecture.Aspire.EFCore;
using Conjecture.EFCore;
using Conjecture.Http;
using Conjecture.Interactions;
using Conjecture.Messaging;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Conjecture.Aspire.EFCore.Tests;

/// <summary>
/// Tests for <see cref="AspireEFCoreInvariants"/>.
/// Uses a SQLite in-memory DB wired through a delegate-based <see cref="AspireDbTarget{TContext}"/>
/// and stub <see cref="IInteractionTarget"/> implementations.
/// </summary>
public sealed class AspireEFCoreInvariantsTests : IAsyncLifetime
{
    private SqliteConnection connection = null!;

    public async Task InitializeAsync()
    {
        connection = new("DataSource=:memory:");
        await connection.OpenAsync();

        DbContextOptions<InvariantsDbContext> opts = SharedOpts();
        await using InvariantsDbContext seed = new(opts);
        await seed.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await connection.DisposeAsync();
    }

    private DbContextOptions<InvariantsDbContext> SharedOpts() =>
        new DbContextOptionsBuilder<InvariantsDbContext>().UseSqlite(connection).Options;

    private async Task<AspireDbTarget<InvariantsDbContext>> CreateDbTargetAsync()
    {
        return await AspireDbTarget<InvariantsDbContext>.CreateAsync(
            (_, _) => Task.FromResult<string?>(connection.ConnectionString),
            "invariants-db",
            _ => new InvariantsDbContext(SharedOpts()));
    }

    private static async Task<int> CountRowsAsync(DbContext ctx)
    {
        return await ctx.Set<InvariantsRow>().CountAsync();
    }

    // -----------------------------------------------------------------------
    // AssertNoPartialWritesOnErrorAsync — success path
    // -----------------------------------------------------------------------

    [Fact]
    public async Task AssertNoPartialWritesOnErrorAsync_passes_when_success_increments_count()
    {
        AspireDbTarget<InvariantsDbContext> db = await CreateDbTargetAsync();
        await using (db)
        {
            StubInteractionTarget writer = new(static (_, _) => Task.FromResult<object?>(null));
            AspireEFCoreInvariants invariants = new(writer, db);

            HttpInteraction interaction = new("api", "POST", "/items", null, null);

            // Writer succeeds; a row gets added by the side-effect.
            await invariants.AssertNoPartialWritesOnErrorAsync(
                interaction,
                static async ctx => await ctx.Set<InvariantsRow>().CountAsync());
        }
    }

    // -----------------------------------------------------------------------
    // AssertNoPartialWritesOnErrorAsync — error leaves count unchanged
    // -----------------------------------------------------------------------

    [Fact]
    public async Task AssertNoPartialWritesOnErrorAsync_passes_when_error_leaves_count_unchanged()
    {
        AspireDbTarget<InvariantsDbContext> db = await CreateDbTargetAsync();
        await using (db)
        {
            // Writer throws; nothing writes to the DB.
            StubInteractionTarget writer = new(static (_, _) => throw new InvalidOperationException("simulated error"));
            AspireEFCoreInvariants invariants = new(writer, db);

            HttpInteraction interaction = new("api", "POST", "/items", null, null);

            // Should not throw because count remains unchanged.
            await invariants.AssertNoPartialWritesOnErrorAsync(
                interaction,
                static async ctx => await ctx.Set<InvariantsRow>().CountAsync());
        }
    }

    // -----------------------------------------------------------------------
    // AssertNoPartialWritesOnErrorAsync — partial write throws
    // -----------------------------------------------------------------------

    [Fact]
    public async Task AssertNoPartialWritesOnErrorAsync_throws_AspireEFCoreInvariantException_when_error_partially_writes()
    {
        AspireDbTarget<InvariantsDbContext> db = await CreateDbTargetAsync();
        await using (db)
        {
            // Writer inserts a row AND throws — a partial write.
            StubInteractionTarget writer = new(async (_, ct) =>
            {
                InvariantsDbContext ctx = new(SharedOpts());
                await using (ctx)
                {
                    ctx.Rows.Add(new InvariantsRow { Name = "partial" });
                    await ctx.SaveChangesAsync(ct);
                }

                throw new InvalidOperationException("simulated error after partial write");
            });

            AspireEFCoreInvariants invariants = new(writer, db);
            HttpInteraction interaction = new("api", "POST", "/items", null, null);

            await Assert.ThrowsAsync<AspireEFCoreInvariantException>(
                () => invariants.AssertNoPartialWritesOnErrorAsync(
                    interaction,
                    static async ctx => await ctx.Set<InvariantsRow>().CountAsync()));
        }
    }

    // -----------------------------------------------------------------------
    // AssertIdempotentAsync — second call is a no-op
    // -----------------------------------------------------------------------

    [Fact]
    public async Task AssertIdempotentAsync_passes_when_second_call_is_noop()
    {
        AspireDbTarget<InvariantsDbContext> db = await CreateDbTargetAsync();
        await using (db)
        {
            int callCount = 0;
            StubInteractionTarget writer = new(async (_, ct) =>
            {
                callCount++;
                if (callCount == 1)
                {
                    // First call inserts a row.
                    InvariantsDbContext ctx = new(SharedOpts());
                    await using (ctx)
                    {
                        ctx.Rows.Add(new InvariantsRow { Name = "idempotent" });
                        await ctx.SaveChangesAsync(ct);
                    }
                }

                // Second call is a no-op — row already exists (upsert).
                return null;
            });

            AspireEFCoreInvariants invariants = new(writer, db);
            HttpInteraction interaction = new("api", "PUT", "/items/1", null, null);

            await invariants.AssertIdempotentAsync(
                interaction,
                static async ctx => await ctx.Set<InvariantsRow>().CountAsync(),
                TimeSpan.FromSeconds(5));
        }
    }

    // -----------------------------------------------------------------------
    // AssertIdempotentAsync — second call creates duplicate (throws)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task AssertIdempotentAsync_throws_when_second_call_creates_duplicate()
    {
        AspireDbTarget<InvariantsDbContext> db = await CreateDbTargetAsync();
        await using (db)
        {
            // Writer always inserts a new row — not idempotent.
            StubInteractionTarget writer = new(async (_, ct) =>
            {
                InvariantsDbContext ctx = new(SharedOpts());
                await using (ctx)
                {
                    ctx.Rows.Add(new InvariantsRow { Name = "duplicate" });
                    await ctx.SaveChangesAsync(ct);
                }

                return null;
            });

            AspireEFCoreInvariants invariants = new(writer, db);
            HttpInteraction interaction = new("api", "POST", "/items", null, null);

            await Assert.ThrowsAsync<AspireEFCoreInvariantException>(
                () => invariants.AssertIdempotentAsync(
                    interaction,
                    static async ctx => await ctx.Set<InvariantsRow>().CountAsync(),
                    TimeSpan.FromSeconds(5)));
        }
    }

    // -----------------------------------------------------------------------
    // AssertIdempotentAsync — eventual convergence via WaitForAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task AssertIdempotentAsync_uses_WaitForAsync_for_eventual_convergence()
    {
        AspireDbTarget<InvariantsDbContext> db = await CreateDbTargetAsync();
        await using (db)
        {
            // Second call inserts a second row but a background task removes it after ~150ms,
            // simulating eventual-consistency convergence that WaitForAsync must poll for.
            int callCount = 0;
            StubInteractionTarget writer = new(async (ignored, ct) =>
            {
                int current = Interlocked.Increment(ref callCount);

                InvariantsDbContext ctx = new(SharedOpts());
                await using (ctx)
                {
                    if (current == 1)
                    {
                        // First call: insert the canonical row.
                        ctx.Rows.Add(new InvariantsRow { Name = "canonical" });
                        await ctx.SaveChangesAsync(ct);
                    }
                    else
                    {
                        // Second call: insert a transient extra row; background cleanup follows.
                        ctx.Rows.Add(new InvariantsRow { Name = "transient" });
                        await ctx.SaveChangesAsync(ct);

                        // Schedule async removal so the count eventually returns to 1.
                        DbContextOptions<InvariantsDbContext> opts = SharedOpts();
                        // Fire and forget — WaitForAsync will poll until convergence.
                        FireAndForget(Task.Run(async () =>
                        {
                            await Task.Delay(150, CancellationToken.None);
                            InvariantsDbContext cleanCtx = new(opts);
                            await using (cleanCtx)
                            {
                                InvariantsRow? transient = await cleanCtx.Rows
                                    .FirstOrDefaultAsync(r => r.Name == "transient", CancellationToken.None);
                                if (transient is not null)
                                {
                                    cleanCtx.Rows.Remove(transient);
                                    await cleanCtx.SaveChangesAsync(CancellationToken.None);
                                }
                            }
                        }, CancellationToken.None));
                    }
                }

                return null;
            });

            AspireEFCoreInvariants invariants = new(writer, db);
            MessageInteraction interaction = new(
                Destination: "orders-queue",
                Body: new ReadOnlyMemory<byte>([]),
                Headers: new Dictionary<string, string>(),
                MessageId: "msg-eventual");

            // eventualTimeout gives WaitForAsync time to poll until count converges to 1.
            await invariants.AssertIdempotentAsync(
                interaction,
                static async ctx => await ctx.Set<InvariantsRow>().CountAsync(),
                TimeSpan.FromSeconds(5));
        }
    }

    // -----------------------------------------------------------------------
    // HttpInteraction and MessageInteraction writer shape coverage
    // -----------------------------------------------------------------------

    [Fact]
    public async Task AssertNoPartialWritesOnErrorAsync_accepts_HttpInteraction_writer_shape()
    {
        AspireDbTarget<InvariantsDbContext> db = await CreateDbTargetAsync();
        await using (db)
        {
            StubInteractionTarget writer = new(static (_, _) => Task.FromResult<object?>(null));
            AspireEFCoreInvariants invariants = new(writer, db);

            HttpInteraction interaction = new("http-api", "GET", "/health", null, null);

            // Should dispatch without error — writer accepts any IInteraction.
            await invariants.AssertNoPartialWritesOnErrorAsync(
                interaction,
                static async ctx => await ctx.Set<InvariantsRow>().CountAsync());
        }
    }

    [Fact]
    public async Task AssertNoPartialWritesOnErrorAsync_accepts_MessageInteraction_writer_shape()
    {
        AspireDbTarget<InvariantsDbContext> db = await CreateDbTargetAsync();
        await using (db)
        {
            IInteraction? dispatched = null;
            StubInteractionTarget writer = new((interaction, _) =>
            {
                dispatched = interaction;
                return Task.FromResult<object?>(null);
            });
            AspireEFCoreInvariants invariants = new(writer, db);

            MessageInteraction interaction = new(
                Destination: "orders-queue",
                Body: new ReadOnlyMemory<byte>([0x01, 0x02]),
                Headers: new Dictionary<string, string>(),
                MessageId: "msg-1");

            await invariants.AssertNoPartialWritesOnErrorAsync(
                interaction,
                static async ctx => await ctx.Set<InvariantsRow>().CountAsync());

            Assert.IsType<MessageInteraction>(dispatched);
        }
    }

    // -----------------------------------------------------------------------
    // Nested helpers and test doubles
    // -----------------------------------------------------------------------

    /// <summary>Intentionally discards the task — caller accepts unobserved exceptions.</summary>
    private static void FireAndForget(Task task)
    {
        // Intentional fire-and-forget; exceptions are unobserved by design in this test helper.
        GC.KeepAlive(task);
    }

    internal sealed class InvariantsDbContext(DbContextOptions<InvariantsDbContext> opts) : DbContext(opts)
    {
        public DbSet<InvariantsRow> Rows => Set<InvariantsRow>();
    }

    internal sealed class InvariantsRow
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }

    /// <summary>
    /// Stub <see cref="IInteractionTarget"/> that delegates to a user-supplied async function.
    /// Captures the last dispatched <see cref="IInteraction"/> for assertion.
    /// </summary>
    private sealed class StubInteractionTarget(Func<IInteraction, CancellationToken, Task<object?>> execute) : IInteractionTarget
    {
        public IInteraction? LastDispatched { get; private set; }

        public async Task<object?> ExecuteAsync(IInteraction interaction, CancellationToken ct)
        {
            LastDispatched = interaction;
            return await execute(interaction, ct).ConfigureAwait(false);
        }
    }
}