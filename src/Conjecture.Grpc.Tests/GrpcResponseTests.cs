// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Collections.Generic;

using Conjecture.Grpc;

using Grpc.Core;

namespace Conjecture.Grpc.Tests;

public class GrpcResponseTests
{
    private static readonly IReadOnlyList<ReadOnlyMemory<byte>> EmptyMessages =
        Array.Empty<ReadOnlyMemory<byte>>();

    private static readonly IReadOnlyDictionary<string, string> EmptyDict =
        new Dictionary<string, string>(0);

    [Fact]
    public void GrpcResponse_Properties_RoundTrip()
    {
        IReadOnlyList<ReadOnlyMemory<byte>> msgs = new[] { new ReadOnlyMemory<byte>([7]) };
        Dictionary<string, string> headers = new() { ["content-type"] = "application/grpc" };
        Dictionary<string, string> trailers = new() { ["grpc-status"] = "0" };

        GrpcResponse response = new(
            StatusCode.OK,
            "OK",
            msgs,
            headers,
            trailers);

        Assert.Equal(StatusCode.OK, response.Status);
        Assert.Equal("OK", response.StatusDetail);
        Assert.Equal(msgs, response.ResponseMessages);
        Assert.Equal("application/grpc", response.ResponseHeaders["content-type"]);
        Assert.Equal("0", response.Trailers["grpc-status"]);
    }

    [Fact]
    public void GrpcResponse_StatusDetail_CanBeNull()
    {
        GrpcResponse response = new(
            StatusCode.OK,
            null,
            EmptyMessages,
            EmptyDict,
            EmptyDict);

        Assert.Null(response.StatusDetail);
    }

    [Fact]
    public void GrpcResponse_IsImmutableRecord_EqualityByValue()
    {
        GrpcResponse a = new(StatusCode.OK, null, EmptyMessages, EmptyDict, EmptyDict);
        GrpcResponse b = new(StatusCode.OK, null, EmptyMessages, EmptyDict, EmptyDict);

        Assert.Equal(a, b);
    }

    [Theory]
    [InlineData(StatusCode.OK)]
    [InlineData(StatusCode.NotFound)]
    [InlineData(StatusCode.Unavailable)]
    [InlineData(StatusCode.Unknown)]
    public void GrpcResponse_StatusCode_RoundTrip(StatusCode code)
    {
        GrpcResponse response = new(code, null, EmptyMessages, EmptyDict, EmptyDict);

        Assert.Equal(code, response.Status);
    }

    [Fact]
    public void GrpcResponse_MultipleResponseMessages_StoredInOrder()
    {
        IReadOnlyList<ReadOnlyMemory<byte>> msgs = new[]
        {
            new ReadOnlyMemory<byte>([10]),
            new ReadOnlyMemory<byte>([20]),
        };

        GrpcResponse response = new(StatusCode.OK, null, msgs, EmptyDict, EmptyDict);

        Assert.Equal(2, response.ResponseMessages.Count);
        Assert.Equal("\n"u8.ToArray(), response.ResponseMessages[0].ToArray());
        Assert.Equal(new byte[] { 20 }, response.ResponseMessages[1].ToArray());
    }
}