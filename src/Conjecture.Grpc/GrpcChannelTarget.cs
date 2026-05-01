// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Threading;
using System.Threading.Tasks;

using Grpc.Core;
using Grpc.Net.Client;

using Conjecture.Abstractions.Interactions;

namespace Conjecture.Grpc;

/// <summary>gRPC target wrapping a <see cref="GrpcChannel"/> for external or containerised services.</summary>
/// <remarks>Initialises a new instance with a pre-built channel.</remarks>
public sealed class GrpcChannelTarget(string resourceName, GrpcChannel channel) : IGrpcTarget, IAsyncDisposable
{
    private readonly GrpcChannel channel = channel ?? throw new ArgumentNullException(nameof(channel));

    /// <summary>Initialises a new instance by creating a channel for the given address.</summary>
    public GrpcChannelTarget(string resourceName, string address)
        : this(resourceName, GrpcChannel.ForAddress(address))
    {
    }

    /// <summary>Gets the resource name this target is registered under.</summary>
    public string ResourceName { get; } = resourceName ?? throw new ArgumentNullException(nameof(resourceName));

    /// <inheritdoc/>
    public CallInvoker GetCallInvoker(string resourceName) =>
        resourceName == ResourceName
            ? channel.CreateCallInvoker()
            : throw new ArgumentException(
                $"Unknown resource '{resourceName}'; expected '{ResourceName}'",
                nameof(resourceName));

    /// <inheritdoc/>
    public Task<object?> ExecuteAsync(
        Conjecture.Abstractions.Interactions.IInteraction interaction,
        CancellationToken ct)
    {
        throw new NotImplementedException(
            "Dispatch logic will be implemented in a subsequent cycle.");
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await channel.ShutdownAsync().ConfigureAwait(false);
    }
}