// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Threading.Tasks;

using Conjecture.Grpc;

using Grpc.Core;
using Grpc.Net.Client;

namespace Conjecture.Grpc.Tests;

public class GrpcChannelTargetTests
{
    [Fact]
    public void GrpcChannelTarget_ImplementsIGrpcTarget()
    {
        using GrpcChannel channel = GrpcChannel.ForAddress("http://localhost:5001");
        GrpcChannelTarget target = new("svc", channel);

        Assert.IsAssignableFrom<IGrpcTarget>(target);
    }

    [Fact]
    public void GrpcChannelTarget_ImplementsIAsyncDisposable()
    {
        using GrpcChannel channel = GrpcChannel.ForAddress("http://localhost:5001");
        GrpcChannelTarget target = new("svc", channel);

        Assert.IsAssignableFrom<IAsyncDisposable>(target);
    }

    [Fact]
    public void ResourceName_ReturnsConstructorValue()
    {
        using GrpcChannel channel = GrpcChannel.ForAddress("http://localhost:5001");
        GrpcChannelTarget target = new("my-service", channel);

        Assert.Equal("my-service", target.ResourceName);
    }

    [Fact]
    public void GetCallInvoker_MatchingName_ReturnsNonNull()
    {
        using GrpcChannel channel = GrpcChannel.ForAddress("http://localhost:5001");
        GrpcChannelTarget target = new("svc", channel);

        CallInvoker invoker = target.GetCallInvoker("svc");

        Assert.NotNull(invoker);
    }

    [Fact]
    public void GetCallInvoker_MismatchedName_ThrowsArgumentException()
    {
        using GrpcChannel channel = GrpcChannel.ForAddress("http://localhost:5001");
        GrpcChannelTarget target = new("svc", channel);

        Assert.Throws<ArgumentException>(() => target.GetCallInvoker("other"));
    }

    [Fact]
    public void Constructor_AddressOverload_ResourceNameRoundTrips()
    {
        GrpcChannelTarget target = new("greeter", "http://localhost:9090");

        Assert.Equal("greeter", target.ResourceName);
    }

    [Fact]
    public void Constructor_AddressOverload_GetCallInvoker_ReturnsNonNull()
    {
        GrpcChannelTarget target = new("greeter", "http://localhost:9090");

        CallInvoker invoker = target.GetCallInvoker("greeter");

        Assert.NotNull(invoker);
    }

    [Fact]
    public async Task DisposeAsync_DoesNotThrow()
    {
        GrpcChannelTarget target = new("svc", "http://localhost:5001");

        await target.DisposeAsync();
    }

    [Fact]
    public void GetCallInvoker_MismatchedName_ExceptionMentionsExpectedName()
    {
        using GrpcChannel channel = GrpcChannel.ForAddress("http://localhost:5001");
        GrpcChannelTarget target = new("expected-name", channel);

        ArgumentException ex = Assert.Throws<ArgumentException>(
            () => target.GetCallInvoker("wrong-name"));

        Assert.Contains("expected-name", ex.Message);
    }
}