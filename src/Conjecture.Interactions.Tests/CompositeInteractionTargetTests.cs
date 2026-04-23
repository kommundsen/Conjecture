// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Conjecture.Interactions;

namespace Conjecture.Interactions.Tests;

public class CompositeInteractionTargetTests
{
    private sealed class AddressedInteraction(string resourceName) : IAddressedInteraction
    {
        public string ResourceName => resourceName;
    }

    private sealed class ConstantTarget(object? value) : IInteractionTarget
    {
        public Task<object?> ExecuteAsync(IInteraction interaction, CancellationToken ct)
        {
            return Task.FromResult(value);
        }
    }

    [Fact]
    public async Task ExecuteAsync_KnownResourceName_DispatchesToMatchingTarget()
    {
        ConstantTarget targetA = new("result-a");
        ConstantTarget targetB = new("result-b");
        CompositeInteractionTarget composite = new(("a", targetA), ("b", targetB));
        AddressedInteraction interaction = new("a");

        object? result = await composite.ExecuteAsync(interaction, CancellationToken.None);

        Assert.Equal("result-a", result);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownResourceName_ThrowsWithRegisteredNamesListed()
    {
        ConstantTarget targetA = new("result-a");
        ConstantTarget targetB = new("result-b");
        CompositeInteractionTarget composite = new(("a", targetA), ("b", targetB));
        AddressedInteraction interaction = new("unknown");

        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
            async () =>
            {
                await composite.ExecuteAsync(interaction, CancellationToken.None);
            });

        Assert.Contains("unknown", ex.Message);
        Assert.Contains("a", ex.Message);
        Assert.Contains("b", ex.Message);
    }

    [Fact]
    public async Task ExecuteAsync_ConcurrentCallsToDifferentTargets_AllDispatchCorrectly()
    {
        ConstantTarget targetA = new("result-a");
        ConstantTarget targetB = new("result-b");
        CompositeInteractionTarget composite = new(("a", targetA), ("b", targetB));

        List<Task<object?>> tasks = [];
        for (int i = 0; i < 20; i++)
        {
            string name = i % 2 == 0 ? "a" : "b";
            tasks.Add(composite.ExecuteAsync(new AddressedInteraction(name), CancellationToken.None));
        }

        object?[] results = await Task.WhenAll(tasks);

        for (int i = 0; i < results.Length; i++)
        {
            string expected = i % 2 == 0 ? "result-a" : "result-b";
            Assert.Equal(expected, results[i]);
        }
    }
}
