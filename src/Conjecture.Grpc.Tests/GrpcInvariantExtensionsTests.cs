// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Collections.Generic;

using Conjecture.Grpc;

using Grpc.Core;

namespace Conjecture.Grpc.Tests;

public class GrpcInvariantExtensionsTests
{
    private static readonly IReadOnlyList<ReadOnlyMemory<byte>> EmptyMessages =
        Array.Empty<ReadOnlyMemory<byte>>();

    private static readonly IReadOnlyDictionary<string, string> EmptyDict =
        new Dictionary<string, string>(0);

    private static GrpcResponse MakeResponse(
        StatusCode status,
        string? statusDetail = null,
        IReadOnlyDictionary<string, string>? trailers = null)
    {
        return new GrpcResponse(
            status,
            statusDetail,
            EmptyMessages,
            EmptyDict,
            trailers ?? EmptyDict);
    }

    // AssertStatusOk — happy path

    [Fact]
    public void AssertStatusOk_WhenStatusIsOk_ReturnsResponse()
    {
        GrpcResponse response = MakeResponse(StatusCode.OK);

        GrpcResponse result = response.AssertStatusOk();

        Assert.Equal(response, result);
    }

    // AssertStatusOk — failure paths

    [Theory]
    [InlineData(StatusCode.NotFound)]
    [InlineData(StatusCode.Unavailable)]
    [InlineData(StatusCode.Unknown)]
    [InlineData(StatusCode.PermissionDenied)]
    public void AssertStatusOk_WhenStatusIsNotOk_Throws(StatusCode status)
    {
        GrpcResponse response = MakeResponse(status);

        Assert.Throws<GrpcInvariantException>(() => response.AssertStatusOk());
    }

    [Fact]
    public void AssertStatusOk_WhenStatusIsNotOk_ExceptionMessageContainsActualStatus()
    {
        GrpcResponse response = MakeResponse(StatusCode.NotFound);

        GrpcInvariantException ex = Assert.Throws<GrpcInvariantException>(
            () => response.AssertStatusOk());

        Assert.Contains(StatusCode.NotFound.ToString(), ex.Message);
    }

    [Fact]
    public void AssertStatusOk_WhenStatusDetailSet_ExceptionMessageContainsDetail()
    {
        GrpcResponse response = MakeResponse(StatusCode.Unavailable, "service down");

        GrpcInvariantException ex = Assert.Throws<GrpcInvariantException>(
            () => response.AssertStatusOk());

        Assert.Contains("service down", ex.Message);
    }

    [Fact]
    public void AssertStatusOk_WhenTrailersPresent_ExceptionMessageContainsTrailers()
    {
        Dictionary<string, string> trailers = new() { ["grpc-message"] = "deadline exceeded" };
        GrpcResponse response = MakeResponse(StatusCode.DeadlineExceeded, null, trailers);

        GrpcInvariantException ex = Assert.Throws<GrpcInvariantException>(
            () => response.AssertStatusOk());

        Assert.Contains("grpc-message", ex.Message);
    }

    // AssertStatus — happy path

    [Fact]
    public void AssertStatus_WhenStatusMatches_ReturnsResponse()
    {
        GrpcResponse response = MakeResponse(StatusCode.NotFound);

        GrpcResponse result = response.AssertStatus(StatusCode.NotFound);

        Assert.Equal(response, result);
    }

    // AssertStatus — failure paths

    [Fact]
    public void AssertStatus_WhenStatusDoesNotMatch_Throws()
    {
        GrpcResponse response = MakeResponse(StatusCode.OK);

        Assert.Throws<GrpcInvariantException>(
            () => response.AssertStatus(StatusCode.NotFound));
    }

    [Fact]
    public void AssertStatus_WhenStatusDoesNotMatch_ExceptionMessageContainsActualStatus()
    {
        GrpcResponse response = MakeResponse(StatusCode.Unavailable);

        GrpcInvariantException ex = Assert.Throws<GrpcInvariantException>(
            () => response.AssertStatus(StatusCode.OK));

        Assert.Contains(StatusCode.Unavailable.ToString(), ex.Message);
    }

    [Fact]
    public void AssertStatus_WhenStatusDetailSet_ExceptionMessageContainsDetail()
    {
        GrpcResponse response = MakeResponse(StatusCode.Internal, "panic");

        GrpcInvariantException ex = Assert.Throws<GrpcInvariantException>(
            () => response.AssertStatus(StatusCode.OK));

        Assert.Contains("panic", ex.Message);
    }

    // AssertNoUnknownStatus — happy paths

    [Theory]
    [InlineData(StatusCode.OK)]
    [InlineData(StatusCode.NotFound)]
    [InlineData(StatusCode.DeadlineExceeded)]
    public void AssertNoUnknownStatus_WhenStatusIsNotUnknown_ReturnsResponse(StatusCode status)
    {
        GrpcResponse response = MakeResponse(status);

        GrpcResponse result = response.AssertNoUnknownStatus();

        Assert.Equal(response, result);
    }

    // AssertNoUnknownStatus — failure path

    [Fact]
    public void AssertNoUnknownStatus_WhenStatusIsUnknown_Throws()
    {
        GrpcResponse response = MakeResponse(StatusCode.Unknown);

        Assert.Throws<GrpcInvariantException>(() => response.AssertNoUnknownStatus());
    }

    [Fact]
    public void AssertNoUnknownStatus_WhenStatusIsUnknown_ExceptionMessageContainsActualStatus()
    {
        GrpcResponse response = MakeResponse(StatusCode.Unknown);

        GrpcInvariantException ex = Assert.Throws<GrpcInvariantException>(
            () => response.AssertNoUnknownStatus());

        Assert.Contains(StatusCode.Unknown.ToString(), ex.Message);
    }

    [Fact]
    public void AssertNoUnknownStatus_WhenStatusDetailSet_ExceptionMessageContainsDetail()
    {
        GrpcResponse response = MakeResponse(StatusCode.Unknown, "no route to service");

        GrpcInvariantException ex = Assert.Throws<GrpcInvariantException>(
            () => response.AssertNoUnknownStatus());

        Assert.Contains("no route to service", ex.Message);
    }
}