// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Collections.Generic;

using Conjecture.Grpc;
using Conjecture.Interactions;

using Grpc.Core;

using Conjecture.Abstractions.Interactions;

namespace Conjecture.Grpc.Tests;

public class GrpcInteractionTests
{
    private static readonly IReadOnlyList<ReadOnlyMemory<byte>> SingleMessage =
        new[] { new ReadOnlyMemory<byte>([1, 2, 3]) };

    private static readonly IReadOnlyDictionary<string, string> EmptyMetadata =
        new Dictionary<string, string>(0);

    [Fact]
    public void GrpcInteraction_ImplementsIInteraction()
    {
        GrpcInteraction interaction = new(
            "svc",
            "/pkg.Service/Method",
            GrpcRpcMode.Unary,
            SingleMessage,
            EmptyMetadata);

        Assert.IsAssignableFrom<IInteraction>(interaction);
    }

    [Fact]
    public void GrpcInteraction_Properties_RoundTrip()
    {
        TimeSpan deadline = TimeSpan.FromSeconds(30);
        Dictionary<string, string> meta = new() { ["authorization"] = "Bearer tok" };
        IReadOnlyList<ReadOnlyMemory<byte>> msgs = new[] { new ReadOnlyMemory<byte>([9, 8]) };

        GrpcInteraction interaction = new(
            "greeter",
            "/helloworld.Greeter/SayHello",
            GrpcRpcMode.ServerStream,
            msgs,
            meta,
            deadline);

        Assert.Equal("greeter", interaction.ResourceName);
        Assert.Equal("/helloworld.Greeter/SayHello", interaction.FullMethodName);
        Assert.Equal(GrpcRpcMode.ServerStream, interaction.Mode);
        Assert.Equal(msgs, interaction.RequestMessages);
        Assert.Equal("Bearer tok", interaction.Metadata["authorization"]);
        Assert.Equal(deadline, interaction.Deadline);
    }

    [Fact]
    public void GrpcInteraction_Deadline_DefaultsToNull()
    {
        GrpcInteraction interaction = new(
            "svc",
            "/pkg.Service/Method",
            GrpcRpcMode.Unary,
            SingleMessage,
            EmptyMetadata);

        Assert.Null(interaction.Deadline);
    }

    [Fact]
    public void GrpcInteraction_IsImmutableRecord_EqualityByValue()
    {
        GrpcInteraction a = new("svc", "/p.S/M", GrpcRpcMode.Unary, SingleMessage, EmptyMetadata);
        GrpcInteraction b = new("svc", "/p.S/M", GrpcRpcMode.Unary, SingleMessage, EmptyMetadata);

        Assert.Equal(a, b);
    }

    [Theory]
    [InlineData(GrpcRpcMode.Unary)]
    [InlineData(GrpcRpcMode.ServerStream)]
    [InlineData(GrpcRpcMode.ClientStream)]
    [InlineData(GrpcRpcMode.Bidi)]
    public void GrpcInteraction_AllModes_RoundTrip(GrpcRpcMode mode)
    {
        GrpcInteraction interaction = new(
            "svc",
            "/p.S/M",
            mode,
            SingleMessage,
            EmptyMetadata);

        Assert.Equal(mode, interaction.Mode);
    }

    [Fact]
    public void GrpcInteraction_MultipleRequestMessages_StoredInOrder()
    {
        IReadOnlyList<ReadOnlyMemory<byte>> msgs = new[]
        {
            new ReadOnlyMemory<byte>([1]),
            new ReadOnlyMemory<byte>([2]),
            new ReadOnlyMemory<byte>([3]),
        };

        GrpcInteraction interaction = new(
            "svc",
            "/p.S/M",
            GrpcRpcMode.ClientStream,
            msgs,
            EmptyMetadata);

        Assert.Equal(3, interaction.RequestMessages.Count);
        Assert.Equal(new byte[] { 1 }, interaction.RequestMessages[0].ToArray());
        Assert.Equal(new byte[] { 2 }, interaction.RequestMessages[1].ToArray());
        Assert.Equal(new byte[] { 3 }, interaction.RequestMessages[2].ToArray());
    }
}