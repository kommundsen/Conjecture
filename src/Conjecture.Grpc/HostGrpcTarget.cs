// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Threading;
using System.Threading.Tasks;

using Grpc.Core;
using Grpc.Net.Client;

using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;

using Conjecture.Abstractions.Interactions;

namespace Conjecture.Grpc;

/// <summary>gRPC target wrapping an <see cref="IHost"/> for in-process testing.</summary>
/// <remarks>Initialises a new instance wrapping the given host.</remarks>
public sealed class HostGrpcTarget(string resourceName, IHost host) : IGrpcTarget, IAsyncDisposable
{
    private readonly IHost host = host ?? throw new ArgumentNullException(nameof(host));
    private GrpcChannel? channel;

    /// <summary>Gets the resource name this target is registered under.</summary>
    public string ResourceName { get; } = resourceName ?? throw new ArgumentNullException(nameof(resourceName));

    /// <inheritdoc/>
    public CallInvoker GetCallInvoker(string resourceName)
    {
        if (resourceName != ResourceName)
        {
            throw new ArgumentException(
                $"Unknown resource '{resourceName}'; expected '{ResourceName}'",
                nameof(resourceName));
        }

        channel ??= GrpcChannel.ForAddress(
            "http://localhost",
            new GrpcChannelOptions { HttpHandler = host.GetTestServer().CreateHandler() });
        return channel.CreateCallInvoker();
    }

    /// <inheritdoc/>
    public Task<object?> ExecuteAsync(
        Conjecture.Abstractions.Interactions.IInteraction interaction,
        CancellationToken ct)
    {
        throw new NotImplementedException(
            "Dispatch logic will be implemented in a subsequent cycle (#472/#473).");
    }

    /// <summary>Disposes the channel only; the host lifecycle is owned by the caller.</summary>
    public async ValueTask DisposeAsync()
    {
        if (channel is not null)
        {
            await channel.ShutdownAsync().ConfigureAwait(false);
        }
    }
}