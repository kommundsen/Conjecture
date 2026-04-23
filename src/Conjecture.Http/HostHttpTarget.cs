// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Net.Http;
using System.Threading.Tasks;

using Microsoft.Extensions.Hosting;

namespace Conjecture.Http;

/// <summary>
/// An <see cref="IHttpTarget"/> that wraps a single <see cref="IHost"/> and its
/// associated <see cref="HttpClient"/> (typical <c>WebApplicationFactory</c> usage).
/// </summary>
public sealed class HostHttpTarget : IHttpTarget, IAsyncDisposable
{
    private readonly IHost host;
    private readonly HttpClient client;

    /// <summary>Creates a new <see cref="HostHttpTarget"/>.</summary>
    /// <param name="host">The in-process host backing the HTTP endpoint.</param>
    /// <param name="client">An <see cref="HttpClient"/> wired to <paramref name="host"/>.</param>
    public HostHttpTarget(IHost host, HttpClient client)
    {
        this.host = host ?? throw new ArgumentNullException(nameof(host));
        this.client = client ?? throw new ArgumentNullException(nameof(client));
    }

    /// <inheritdoc/>
    public HttpClient ResolveClient(string resourceName)
    {
        return this.client;
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (this.host is IAsyncDisposable asyncHost)
        {
            await asyncHost.DisposeAsync();
        }
        else
        {
            this.host.Dispose();
        }

        this.client.Dispose();
    }
}
