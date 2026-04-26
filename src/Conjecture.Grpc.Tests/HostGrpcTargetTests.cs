// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Threading.Tasks;

using Conjecture.Grpc;

using Grpc.Core;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Conjecture.Grpc.Tests;

public class HostGrpcTargetTests
{
    private static IHost BuildMinimalHost()
    {
        return new HostBuilder()
            .ConfigureWebHost(static webBuilder => webBuilder
                    .UseTestServer()
                    .ConfigureServices(static services => services.AddGrpc())
                    .Configure(static app =>
                    {
                        app.UseRouting();
                        app.UseEndpoints(static _ => { });
                    }))
            .Build();
    }

    [Fact]
    public void HostGrpcTarget_ImplementsIGrpcTarget()
    {
        using IHost host = BuildMinimalHost();
        HostGrpcTarget target = new("greeter", host);

        Assert.IsAssignableFrom<IGrpcTarget>(target);
    }

    [Fact]
    public void HostGrpcTarget_ImplementsIAsyncDisposable()
    {
        using IHost host = BuildMinimalHost();
        HostGrpcTarget target = new("greeter", host);

        Assert.IsAssignableFrom<IAsyncDisposable>(target);
    }

    [Fact]
    public void ResourceName_ReturnsConstructorValue()
    {
        using IHost host = BuildMinimalHost();
        HostGrpcTarget target = new("my-greeter", host);

        Assert.Equal("my-greeter", target.ResourceName);
    }

    [Fact]
    public async Task GetCallInvoker_MatchingName_ReturnsNonNull()
    {
        using IHost host = BuildMinimalHost();
        await host.StartAsync();
        HostGrpcTarget target = new("greeter", host);

        CallInvoker invoker = target.GetCallInvoker("greeter");

        Assert.NotNull(invoker);
    }

    [Fact]
    public async Task GetCallInvoker_MismatchedName_ThrowsArgumentException()
    {
        using IHost host = BuildMinimalHost();
        await host.StartAsync();
        HostGrpcTarget target = new("greeter", host);

        Assert.Throws<ArgumentException>(() => target.GetCallInvoker("other"));
    }

    [Fact]
    public async Task DisposeAsync_DoesNotDisposeHost()
    {
        using IHost host = BuildMinimalHost();
        await host.StartAsync();
        HostGrpcTarget target = new("greeter", host);

        await target.DisposeAsync();

        // Host should still be running — its Services container must not be disposed
        Assert.NotNull(host.Services);
    }

    [Fact]
    public async Task DisposeAsync_DoesNotThrow()
    {
        using IHost host = BuildMinimalHost();
        await host.StartAsync();
        HostGrpcTarget target = new("greeter", host);

        await target.DisposeAsync();
    }

    [Fact]
    public async Task GetCallInvoker_MismatchedName_ExceptionMentionsExpectedName()
    {
        using IHost host = BuildMinimalHost();
        await host.StartAsync();
        HostGrpcTarget target = new("expected-name", host);

        ArgumentException ex = Assert.Throws<ArgumentException>(
            () => target.GetCallInvoker("wrong-name"));

        Assert.Contains("expected-name", ex.Message);
    }
}