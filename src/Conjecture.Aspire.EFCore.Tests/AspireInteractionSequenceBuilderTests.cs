// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Conjecture.Aspire.EFCore;
using Conjecture.Core;
using Conjecture.Http;
using Conjecture.Interactions;
using Conjecture.Messaging;

using Microsoft.EntityFrameworkCore;

using Conjecture.Abstractions.Interactions;

namespace Conjecture.Aspire.EFCore.Tests;

/// <summary>
/// Tests for <see cref="AspireInteractionSequenceBuilder"/>.
/// </summary>
public sealed class AspireInteractionSequenceBuilderTests
{
    // -----------------------------------------------------------------------
    // Build — every generated step is an IAddressedInteraction from a registered builder
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Build_produces_sequence_with_only_registered_step_kinds()
    {
        Strategy<HttpInteraction> httpStep = Strategy.Http("api")
            .Get("/health")
            .Build();

        AspireInteractionSequenceBuilder builder = new AspireInteractionSequenceBuilder()
            .Http("api", httpStep);

        Strategy<IReadOnlyList<IAddressedInteraction>> strategy = builder.Build(minSize: 1, maxSize: 5);

        ConjectureSettings settings = new() { MaxExamples = 50, Seed = 1UL };
        StubTarget apiTarget = new(static (_, _) => Task.FromResult<object?>(null));

        await Property.ForAll(
            apiTarget,
            strategy,
            static (target, seq) =>
            {
                Assert.NotEmpty(seq);
                foreach (IAddressedInteraction step in seq)
                {
                    Assert.IsAssignableFrom<IAddressedInteraction>(step);
                }

                return Task.CompletedTask;
            },
            settings);
    }

    // -----------------------------------------------------------------------
    // Build — respects minSize and maxSize
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Build_respects_minSize_and_maxSize()
    {
        Strategy<HttpInteraction> httpStep = Strategy.Http("api")
            .Get("/ping")
            .Build();

        AspireInteractionSequenceBuilder builder = new AspireInteractionSequenceBuilder()
            .Http("api", httpStep);

        Strategy<IReadOnlyList<IAddressedInteraction>> strategy = builder.Build(minSize: 2, maxSize: 4);

        ConjectureSettings settings = new() { MaxExamples = 100, Seed = 1UL };
        StubTarget apiTarget = new(static (_, _) => Task.FromResult<object?>(null));

        await Property.ForAll(
            apiTarget,
            strategy,
            static (target, seq) =>
            {
                Assert.True(seq.Count >= 2, $"Expected count >= 2 but got {seq.Count}.");
                Assert.True(seq.Count <= 4, $"Expected count <= 4 but got {seq.Count}.");
                return Task.CompletedTask;
            },
            settings);
    }

    // -----------------------------------------------------------------------
    // Build — distribution covers Http, Message, and DbSnapshot across many examples
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Build_can_interleave_Http_Message_and_DbSnapshot_steps()
    {
        Strategy<HttpInteraction> httpStep = Strategy.Http("api")
            .Get("/ping")
            .Build();

        Strategy<MessageInteraction> messageStep = Strategy.Compose(static ctx =>
            new MessageInteraction(
                Destination: "orders",
                Body: new ReadOnlyMemory<byte>([]),
                Headers: new Dictionary<string, string>(),
                MessageId: ctx.Generate(Strategy.Strings(minLength: 1, maxLength: 8))));

        AspireInteractionSequenceBuilder builder = new AspireInteractionSequenceBuilder()
            .Http("api", httpStep)
            .Message("orders", messageStep)
            .DbSnapshot("snapshot-db", "row-count", static ctx => Task.FromResult<object?>(0));

        Strategy<IReadOnlyList<IAddressedInteraction>> strategy = builder.Build(minSize: 1, maxSize: 20);

        ConjectureSettings settings = new() { MaxExamples = 200, Seed = 1UL };

        bool sawHttp = false;
        bool sawMessage = false;
        bool sawSnapshot = false;

        StubTarget apiTarget = new(static (_, _) => Task.FromResult<object?>(null));

        await Property.ForAll(
            apiTarget,
            strategy,
            (target, seq) =>
            {
                foreach (IAddressedInteraction step in seq)
                {
                    if (step is HttpInteraction)
                    {
                        sawHttp = true;
                    }
                    else if (step is DbSnapshotInteraction)
                    {
                        sawSnapshot = true;
                    }
                    else if (step.ResourceName == "orders")
                    {
                        sawMessage = true;
                    }
                }

                return Task.CompletedTask;
            },
            settings);

        Assert.True(sawHttp, "Expected at least one HttpInteraction across 200 examples.");
        Assert.True(sawMessage, "Expected at least one MessageInteraction across 200 examples.");
        Assert.True(sawSnapshot, "Expected at least one DbSnapshotInteraction across 200 examples.");
    }

    // -----------------------------------------------------------------------
    // Build — sequence dispatches correctly through CompositeInteractionTarget
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Build_returned_sequence_dispatches_correctly_through_CompositeInteractionTarget()
    {
        Strategy<HttpInteraction> httpStep = Strategy.Http("api")
            .Get("/test")
            .Build();

        AspireInteractionSequenceBuilder builder = new AspireInteractionSequenceBuilder()
            .Http("api", httpStep);

        Strategy<IReadOnlyList<IAddressedInteraction>> strategy = builder.Build(minSize: 1, maxSize: 3);

        List<IInteraction> dispatched = [];
        StubTarget apiTarget = new(static (_, _) => Task.FromResult<object?>(null));
        CompositeInteractionTarget composite = new(("api", apiTarget));

        ConjectureSettings settings = new() { MaxExamples = 10, Seed = 1UL };

        await Property.ForAll(
            composite,
            strategy,
            async (target, seq) =>
            {
                foreach (IAddressedInteraction step in seq)
                {
                    object? result = await target.ExecuteAsync(step, CancellationToken.None);
                    dispatched.Add(step);
                }
            },
            settings);

        Assert.NotEmpty(dispatched);
        Assert.All(dispatched, static step => Assert.IsAssignableFrom<IAddressedInteraction>(step));
    }

    // -----------------------------------------------------------------------
    // Nested helpers
    // -----------------------------------------------------------------------

    private sealed class StubTarget(Func<IInteraction, CancellationToken, Task<object?>> execute)
        : IInteractionTarget
    {
        public Task<object?> ExecuteAsync(IInteraction interaction, CancellationToken ct)
        {
            return execute(interaction, ct);
        }
    }
}