// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using Conjecture.Interactions;

using Microsoft.Extensions.Hosting;

namespace Conjecture.Http.Tests;

public class HostHttpTargetTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        public HttpResponseMessage Response { get; set; } = new(HttpStatusCode.OK);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(Response);
        }
    }

    private sealed class TrackingHost : IHost
    {
        public bool Disposed { get; private set; }

        public IServiceProvider Services { get; } = new HostBuilder().Build().Services;

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            Disposed = true;
        }
    }

    private sealed class TrackingAsyncHost : IHost, IAsyncDisposable
    {
        public bool DisposedSync { get; private set; }

        public bool DisposedAsync { get; private set; }

        public IServiceProvider Services { get; } = new HostBuilder().Build().Services;

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            DisposedSync = true;
        }

        public ValueTask DisposeAsync()
        {
            DisposedAsync = true;
            return ValueTask.CompletedTask;
        }
    }

    [Fact]
    public void HostHttpTarget_ImplementsIHttpTargetAndIAsyncDisposable()
    {
        Assert.True(typeof(IHttpTarget).IsAssignableFrom(typeof(HostHttpTarget)));
        Assert.True(typeof(IAsyncDisposable).IsAssignableFrom(typeof(HostHttpTarget)));
    }

    [Fact]
    public void ResolveClient_ReturnsWrappedClient_RegardlessOfResourceName()
    {
        using TrackingHost host = new();
        using HttpClient client = new();
        HostHttpTarget target = new(host, client);

        HttpClient resolvedA = target.ResolveClient("anything");
        HttpClient resolvedB = target.ResolveClient("other");

        Assert.Same(client, resolvedA);
        Assert.Same(client, resolvedB);
    }

    [Fact]
    public async Task ExecuteAsync_DispatchesThroughWrappedClient()
    {
        StubHandler handler = new() { Response = new HttpResponseMessage(HttpStatusCode.Created) };
        HttpClient client = new(handler) { BaseAddress = new Uri("http://localhost/") };
        TrackingHost host = new();
        HostHttpTarget target = new(host, client);
        IInteractionTarget sink = target;

        HttpInteraction interaction = new("api", "GET", "/ping", null, null);
        object? result = await sink.ExecuteAsync(interaction, CancellationToken.None);

        HttpResponseMessage response = Assert.IsType<HttpResponseMessage>(result);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(handler.LastRequest);
        Assert.Equal(new Uri("http://localhost/ping"), handler.LastRequest!.RequestUri);
    }

    [Fact]
    public async Task DisposeAsync_DisposesHost_AndClient()
    {
        StubHandler handler = new();
        HttpClient client = new(handler);
        TrackingHost host = new();
        HostHttpTarget target = new(host, client);

        await target.DisposeAsync();

        Assert.True(host.Disposed);
        Assert.Throws<ObjectDisposedException>(() => client.CancelPendingRequests());
    }

    [Fact]
    public async Task DisposeAsync_PrefersAsyncDisposal_WhenHostSupportsIt()
    {
        StubHandler handler = new();
        HttpClient client = new(handler);
        TrackingAsyncHost host = new();
        HostHttpTarget target = new(host, client);

        await target.DisposeAsync();

        Assert.True(host.DisposedAsync);
    }

    [Fact]
    public void Constructor_NullHost_Throws()
    {
        using HttpClient client = new();
        Assert.Throws<ArgumentNullException>(() => new HostHttpTarget(null!, client));
    }

    [Fact]
    public void Constructor_NullClient_Throws()
    {
        using TrackingHost host = new();
        Assert.Throws<ArgumentNullException>(() => new HostHttpTarget(host, null!));
    }
}
