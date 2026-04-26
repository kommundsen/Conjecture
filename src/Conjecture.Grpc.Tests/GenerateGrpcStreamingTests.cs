// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Collections.Generic;
using System.Text;

using Conjecture.Core;
using Conjecture.Grpc;

using Grpc.Core;

namespace Conjecture.Grpc.Tests;

public class GenerateGrpcStreamingTests
{
    private sealed record TestRequest(int Value);
    private sealed record TestResponse(string Echo);

    private static Method<TestRequest, TestResponse> BuildMethod(
        string serviceName = "test.TestService",
        string methodName = "TestCall",
        MethodType methodType = MethodType.Unary)
    {
        Marshaller<TestRequest> requestMarshaller = Marshallers.Create(
            static req => BitConverter.GetBytes(req.Value),
            static bytes => new TestRequest(BitConverter.ToInt32(bytes)));
        Marshaller<TestResponse> responseMarshaller = Marshallers.Create(
            static resp => Encoding.UTF8.GetBytes(resp.Echo),
            static bytes => new TestResponse(Encoding.UTF8.GetString(bytes)));
        return new Method<TestRequest, TestResponse>(
            methodType,
            serviceName,
            methodName,
            requestMarshaller,
            responseMarshaller);
    }

    // ── ServerStream ──────────────────────────────────────────────────────────

    [Fact]
    public void ServerStream_GeneratedInteraction_ModeIsServerStream()
    {
        Method<TestRequest, TestResponse> method = BuildMethod(methodType: MethodType.ServerStreaming);
        Strategy<TestRequest> requestStrategy = Generate.Just(new TestRequest(1));

        Strategy<GrpcInteraction> strategy = GenerateGrpc.ServerStream("svc", method, requestStrategy);
        GrpcInteraction interaction = DataGen.SampleOne(strategy, seed: 1UL);

        Assert.Equal(GrpcRpcMode.ServerStream, interaction.Mode);
    }

    [Fact]
    public void ServerStream_GeneratedInteraction_HasExactlyOneRequestMessage()
    {
        Method<TestRequest, TestResponse> method = BuildMethod(methodType: MethodType.ServerStreaming);
        Strategy<TestRequest> requestStrategy = Generate.Just(new TestRequest(7));

        Strategy<GrpcInteraction> strategy = GenerateGrpc.ServerStream("svc", method, requestStrategy);
        GrpcInteraction interaction = DataGen.SampleOne(strategy, seed: 1UL);

        Assert.Single(interaction.RequestMessages);
    }

    [Fact]
    public void ServerStream_GeneratedInteraction_RequestBytesRoundTripViaMarshaller()
    {
        Method<TestRequest, TestResponse> method = BuildMethod(methodType: MethodType.ServerStreaming);
        TestRequest original = new(55);
        Strategy<TestRequest> requestStrategy = Generate.Just(original);

        Strategy<GrpcInteraction> strategy = GenerateGrpc.ServerStream("svc", method, requestStrategy);
        GrpcInteraction interaction = DataGen.SampleOne(strategy, seed: 1UL);

        byte[] bytes = interaction.RequestMessages[0].ToArray();
        TestRequest roundTripped = method.RequestMarshaller.Deserializer(bytes);
        Assert.Equal(original, roundTripped);
    }

    [Fact]
    public void ServerStream_GeneratedInteraction_FullMethodNameMatchesMethodFullName()
    {
        Method<TestRequest, TestResponse> method = BuildMethod("pkg.StreamSvc", "Watch", MethodType.ServerStreaming);
        Strategy<TestRequest> requestStrategy = Generate.Just(new TestRequest(0));

        Strategy<GrpcInteraction> strategy = GenerateGrpc.ServerStream("svc", method, requestStrategy);
        GrpcInteraction interaction = DataGen.SampleOne(strategy, seed: 1UL);

        Assert.Equal(method.FullName, interaction.FullMethodName);
    }

    [Theory]
    [InlineData("events")]
    [InlineData("stream/v2")]
    public void ServerStream_GeneratedInteraction_ResourceNameMatchesArgument(string resourceName)
    {
        Method<TestRequest, TestResponse> method = BuildMethod(methodType: MethodType.ServerStreaming);
        Strategy<TestRequest> requestStrategy = Generate.Just(new TestRequest(0));

        Strategy<GrpcInteraction> strategy = GenerateGrpc.ServerStream(resourceName, method, requestStrategy);
        GrpcInteraction interaction = DataGen.SampleOne(strategy, seed: 1UL);

        Assert.Equal(resourceName, interaction.ResourceName);
    }

    [Fact]
    public void ServerStream_DefaultMetadata_IsEmpty()
    {
        Method<TestRequest, TestResponse> method = BuildMethod(methodType: MethodType.ServerStreaming);
        Strategy<TestRequest> requestStrategy = Generate.Just(new TestRequest(3));

        Strategy<GrpcInteraction> strategy = GenerateGrpc.ServerStream("svc", method, requestStrategy);
        GrpcInteraction interaction = DataGen.SampleOne(strategy, seed: 1UL);

        Assert.Empty(interaction.Metadata);
    }

    [Fact]
    public void ServerStream_CustomMetadata_FlowsThroughToInteraction()
    {
        Method<TestRequest, TestResponse> method = BuildMethod(methodType: MethodType.ServerStreaming);
        Strategy<TestRequest> requestStrategy = Generate.Just(new TestRequest(3));
        Dictionary<string, string> customMeta = new() { ["x-trace-id"] = "abc123" };

        Strategy<GrpcInteraction> strategy = GenerateGrpc.ServerStream("svc", method, requestStrategy, customMeta);
        GrpcInteraction interaction = DataGen.SampleOne(strategy, seed: 1UL);

        Assert.Equal("abc123", interaction.Metadata["x-trace-id"]);
    }

    // ── ClientStream ──────────────────────────────────────────────────────────

    [Fact]
    public void ClientStream_GeneratedInteraction_ModeIsClientStream()
    {
        Method<TestRequest, TestResponse> method = BuildMethod(methodType: MethodType.ClientStreaming);
        Strategy<IReadOnlyList<TestRequest>> requestsStrategy =
            Generate.Lists(Generate.Just(new TestRequest(1)), minSize: 2, maxSize: 2)
                    .Select(static list => (IReadOnlyList<TestRequest>)list);

        Strategy<GrpcInteraction> strategy = GenerateGrpc.ClientStream("svc", method, requestsStrategy);
        GrpcInteraction interaction = DataGen.SampleOne(strategy, seed: 1UL);

        Assert.Equal(GrpcRpcMode.ClientStream, interaction.Mode);
    }

    [Fact]
    public void ClientStream_GeneratedInteraction_RequestCountMatchesInputListCount()
    {
        Method<TestRequest, TestResponse> method = BuildMethod(methodType: MethodType.ClientStreaming);
        IReadOnlyList<TestRequest> fixedList = [new(10), new(20), new(30)];
        Strategy<IReadOnlyList<TestRequest>> requestsStrategy = Generate.Just(fixedList);

        Strategy<GrpcInteraction> strategy = GenerateGrpc.ClientStream("svc", method, requestsStrategy);
        GrpcInteraction interaction = DataGen.SampleOne(strategy, seed: 1UL);

        Assert.Equal(3, interaction.RequestMessages.Count);
    }

    [Fact]
    public void ClientStream_GeneratedInteraction_RequestBytesPreserveOrder()
    {
        Method<TestRequest, TestResponse> method = BuildMethod(methodType: MethodType.ClientStreaming);
        List<TestRequest> requests = [new(100), new(200), new(300)];
        Strategy<IReadOnlyList<TestRequest>> requestsStrategy =
            Generate.Just((IReadOnlyList<TestRequest>)requests);

        Strategy<GrpcInteraction> strategy = GenerateGrpc.ClientStream("svc", method, requestsStrategy);
        GrpcInteraction interaction = DataGen.SampleOne(strategy, seed: 1UL);

        for (int i = 0; i < requests.Count; i++)
        {
            TestRequest roundTripped = method.RequestMarshaller.Deserializer(
                interaction.RequestMessages[i].ToArray());
            Assert.Equal(requests[i], roundTripped);
        }
    }

    [Fact]
    public void ClientStream_EmptyRequestList_ProducesInteractionWithZeroRequestMessages()
    {
        Method<TestRequest, TestResponse> method = BuildMethod(methodType: MethodType.ClientStreaming);
        IReadOnlyList<TestRequest> emptyList = [];
        Strategy<IReadOnlyList<TestRequest>> requestsStrategy = Generate.Just(emptyList);

        Strategy<GrpcInteraction> strategy = GenerateGrpc.ClientStream("svc", method, requestsStrategy);
        GrpcInteraction interaction = DataGen.SampleOne(strategy, seed: 1UL);

        Assert.Empty(interaction.RequestMessages);
    }

    [Fact]
    public void ClientStream_DefaultMetadata_IsEmpty()
    {
        Method<TestRequest, TestResponse> method = BuildMethod(methodType: MethodType.ClientStreaming);
        IReadOnlyList<TestRequest> singleItem = [new(1)];
        Strategy<IReadOnlyList<TestRequest>> requestsStrategy = Generate.Just(singleItem);

        Strategy<GrpcInteraction> strategy = GenerateGrpc.ClientStream("svc", method, requestsStrategy);
        GrpcInteraction interaction = DataGen.SampleOne(strategy, seed: 1UL);

        Assert.Empty(interaction.Metadata);
    }

    [Fact]
    public void ClientStream_CustomMetadata_FlowsThroughToInteraction()
    {
        Method<TestRequest, TestResponse> method = BuildMethod(methodType: MethodType.ClientStreaming);
        IReadOnlyList<TestRequest> singleItem = [new(1)];
        Strategy<IReadOnlyList<TestRequest>> requestsStrategy = Generate.Just(singleItem);
        Dictionary<string, string> customMeta = new() { ["authorization"] = "Bearer tok" };

        Strategy<GrpcInteraction> strategy = GenerateGrpc.ClientStream("svc", method, requestsStrategy, customMeta);
        GrpcInteraction interaction = DataGen.SampleOne(strategy, seed: 1UL);

        Assert.Equal("Bearer tok", interaction.Metadata["authorization"]);
    }

    [Fact]
    public void ClientStream_SequenceShrinkingComposition_RequestCountCorrelatesWithListStrategy()
    {
        Method<TestRequest, TestResponse> method = BuildMethod(methodType: MethodType.ClientStreaming);
        Strategy<IReadOnlyList<TestRequest>> requestsStrategy =
            Generate.Lists(Generate.Just(new TestRequest(1)), minSize: 3, maxSize: 5)
                    .Select(static list => (IReadOnlyList<TestRequest>)list);

        Strategy<GrpcInteraction> strategy = GenerateGrpc.ClientStream("svc", method, requestsStrategy);
        GrpcInteraction interaction = DataGen.SampleOne(strategy, seed: 42UL);

        Assert.InRange(interaction.RequestMessages.Count, 3, 5);
    }

    // ── BidiStream ────────────────────────────────────────────────────────────

    [Fact]
    public void BidiStream_GeneratedInteraction_ModeIsBidi()
    {
        Method<TestRequest, TestResponse> method = BuildMethod(methodType: MethodType.DuplexStreaming);
        IReadOnlyList<TestRequest> twoItems = [new(1), new(2)];
        Strategy<IReadOnlyList<TestRequest>> requestsStrategy = Generate.Just(twoItems);

        Strategy<GrpcInteraction> strategy = GenerateGrpc.BidiStream("svc", method, requestsStrategy);
        GrpcInteraction interaction = DataGen.SampleOne(strategy, seed: 1UL);

        Assert.Equal(GrpcRpcMode.Bidi, interaction.Mode);
    }

    [Fact]
    public void BidiStream_GeneratedInteraction_RequestCountMatchesInputListCount()
    {
        Method<TestRequest, TestResponse> method = BuildMethod(methodType: MethodType.DuplexStreaming);
        IReadOnlyList<TestRequest> fixedList = [new(11), new(22)];
        Strategy<IReadOnlyList<TestRequest>> requestsStrategy = Generate.Just(fixedList);

        Strategy<GrpcInteraction> strategy = GenerateGrpc.BidiStream("svc", method, requestsStrategy);
        GrpcInteraction interaction = DataGen.SampleOne(strategy, seed: 1UL);

        Assert.Equal(2, interaction.RequestMessages.Count);
    }

    [Fact]
    public void BidiStream_GeneratedInteraction_RequestBytesPreserveOrder()
    {
        Method<TestRequest, TestResponse> method = BuildMethod(methodType: MethodType.DuplexStreaming);
        List<TestRequest> requests = [new(7), new(8), new(9)];
        Strategy<IReadOnlyList<TestRequest>> requestsStrategy =
            Generate.Just((IReadOnlyList<TestRequest>)requests);

        Strategy<GrpcInteraction> strategy = GenerateGrpc.BidiStream("svc", method, requestsStrategy);
        GrpcInteraction interaction = DataGen.SampleOne(strategy, seed: 1UL);

        for (int i = 0; i < requests.Count; i++)
        {
            TestRequest roundTripped = method.RequestMarshaller.Deserializer(
                interaction.RequestMessages[i].ToArray());
            Assert.Equal(requests[i], roundTripped);
        }
    }

    [Fact]
    public void BidiStream_DefaultMetadata_IsEmpty()
    {
        Method<TestRequest, TestResponse> method = BuildMethod(methodType: MethodType.DuplexStreaming);
        IReadOnlyList<TestRequest> singleItem = [new(1)];
        Strategy<IReadOnlyList<TestRequest>> requestsStrategy = Generate.Just(singleItem);

        Strategy<GrpcInteraction> strategy = GenerateGrpc.BidiStream("svc", method, requestsStrategy);
        GrpcInteraction interaction = DataGen.SampleOne(strategy, seed: 1UL);

        Assert.Empty(interaction.Metadata);
    }

    [Fact]
    public void BidiStream_CustomMetadata_FlowsThroughToInteraction()
    {
        Method<TestRequest, TestResponse> method = BuildMethod(methodType: MethodType.DuplexStreaming);
        IReadOnlyList<TestRequest> singleItem = [new(1)];
        Strategy<IReadOnlyList<TestRequest>> requestsStrategy = Generate.Just(singleItem);
        Dictionary<string, string> customMeta = new() { ["x-request-id"] = "req-42" };

        Strategy<GrpcInteraction> strategy = GenerateGrpc.BidiStream("svc", method, requestsStrategy, customMeta);
        GrpcInteraction interaction = DataGen.SampleOne(strategy, seed: 1UL);

        Assert.Equal("req-42", interaction.Metadata["x-request-id"]);
    }

    [Fact]
    public void BidiStream_SequenceShrinkingComposition_RequestCountCorrelatesWithListStrategy()
    {
        Method<TestRequest, TestResponse> method = BuildMethod(methodType: MethodType.DuplexStreaming);
        Strategy<IReadOnlyList<TestRequest>> requestsStrategy =
            Generate.Lists(Generate.Just(new TestRequest(1)), minSize: 2, maxSize: 6)
                    .Select(static list => (IReadOnlyList<TestRequest>)list);

        Strategy<GrpcInteraction> strategy = GenerateGrpc.BidiStream("svc", method, requestsStrategy);
        GrpcInteraction interaction = DataGen.SampleOne(strategy, seed: 77UL);

        Assert.InRange(interaction.RequestMessages.Count, 2, 6);
    }
}