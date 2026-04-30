// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Collections.Generic;
using System.Text;

using Conjecture.Core;
using Conjecture.Grpc;

using Grpc.Core;

namespace Conjecture.Grpc.Tests;

public class GenerateGrpcTests
{
    // Private nested types — no generated protobuf needed.
    private sealed record TestRequest(int Value);
    private sealed record TestResponse(string Echo);

    // Build a Method<TestRequest, TestResponse> using simple byte-encoding marshallers.
    private static Method<TestRequest, TestResponse> BuildMethod(
        string serviceName = "test.TestService",
        string methodName = "TestCall")
    {
        Marshaller<TestRequest> requestMarshaller = Marshallers.Create(
            static req => BitConverter.GetBytes(req.Value),
            static bytes => new TestRequest(BitConverter.ToInt32(bytes)));
        Marshaller<TestResponse> responseMarshaller = Marshallers.Create(
            static resp => Encoding.UTF8.GetBytes(resp.Echo),
            static bytes => new TestResponse(Encoding.UTF8.GetString(bytes)));
        return new Method<TestRequest, TestResponse>(
            MethodType.Unary,
            serviceName,
            methodName,
            requestMarshaller,
            responseMarshaller);
    }

    [Fact]
    public void Unary_ReturnsStrategyOfGrpcInteraction()
    {
        Method<TestRequest, TestResponse> method = BuildMethod();
        Strategy<TestRequest> requestStrategy = Strategy.Just(new TestRequest(42));

        Strategy<GrpcInteraction> strategy = GenerateGrpc.Unary("greeter", method, requestStrategy);

        Assert.NotNull(strategy);
    }

    [Fact]
    public void Unary_GeneratedInteraction_ModeIsUnary()
    {
        Method<TestRequest, TestResponse> method = BuildMethod();
        Strategy<TestRequest> requestStrategy = Strategy.Just(new TestRequest(1));

        Strategy<GrpcInteraction> strategy = GenerateGrpc.Unary("svc", method, requestStrategy);
        GrpcInteraction interaction = strategy.WithSeed(1UL).Sample();

        Assert.Equal(GrpcRpcMode.Unary, interaction.Mode);
    }

    [Fact]
    public void Unary_GeneratedInteraction_HasExactlyOneRequestMessage()
    {
        Method<TestRequest, TestResponse> method = BuildMethod();
        Strategy<TestRequest> requestStrategy = Strategy.Just(new TestRequest(7));

        Strategy<GrpcInteraction> strategy = GenerateGrpc.Unary("svc", method, requestStrategy);
        GrpcInteraction interaction = strategy.WithSeed(1UL).Sample();

        Assert.Single(interaction.RequestMessages);
    }

    [Fact]
    public void Unary_GeneratedInteraction_RequestBytesRoundTripViaMarshaller()
    {
        Method<TestRequest, TestResponse> method = BuildMethod();
        TestRequest original = new(99);
        Strategy<TestRequest> requestStrategy = Strategy.Just(original);

        Strategy<GrpcInteraction> strategy = GenerateGrpc.Unary("svc", method, requestStrategy);
        GrpcInteraction interaction = strategy.WithSeed(1UL).Sample();

        byte[] bytes = interaction.RequestMessages[0].ToArray();
        TestRequest roundTripped = method.RequestMarshaller.Deserializer(bytes);
        Assert.Equal(original, roundTripped);
    }

    [Fact]
    public void Unary_GeneratedInteraction_FullMethodNameMatchesMethodFullName()
    {
        Method<TestRequest, TestResponse> method = BuildMethod("pkg.Greeter", "SayHello");
        Strategy<TestRequest> requestStrategy = Strategy.Just(new TestRequest(0));

        Strategy<GrpcInteraction> strategy = GenerateGrpc.Unary("svc", method, requestStrategy);
        GrpcInteraction interaction = strategy.WithSeed(1UL).Sample();

        Assert.Equal(method.FullName, interaction.FullMethodName);
    }

    [Theory]
    [InlineData("greeter")]
    [InlineData("orders/v1")]
    [InlineData("my-service")]
    public void Unary_GeneratedInteraction_ResourceNameMatchesArgument(string resourceName)
    {
        Method<TestRequest, TestResponse> method = BuildMethod();
        Strategy<TestRequest> requestStrategy = Strategy.Just(new TestRequest(3));

        Strategy<GrpcInteraction> strategy = GenerateGrpc.Unary(resourceName, method, requestStrategy);
        GrpcInteraction interaction = strategy.WithSeed(1UL).Sample();

        Assert.Equal(resourceName, interaction.ResourceName);
    }

    [Fact]
    public void Unary_MultipleDraws_ProduceDistinctInteractionsWhenSourceStrategyVaries()
    {
        Method<TestRequest, TestResponse> method = BuildMethod();
        Strategy<int> intStrategy = Strategy.Integers<int>(1, 1000);
        Strategy<TestRequest> requestStrategy = intStrategy.Select(static i => new TestRequest(i));

        Strategy<GrpcInteraction> strategy = GenerateGrpc.Unary("svc", method, requestStrategy);
        IReadOnlyList<GrpcInteraction> samples = strategy.WithSeed(42UL).Sample(10);

        HashSet<int> distinctValues = [];
        foreach (GrpcInteraction interaction in samples)
        {
            int value = BitConverter.ToInt32(interaction.RequestMessages[0].ToArray());
            distinctValues.Add(value);
        }

        Assert.True(distinctValues.Count > 1, "Expected multiple distinct request values across 10 samples.");
    }

    [Fact]
    public void Unary_DefaultMetadata_IsEmpty()
    {
        Method<TestRequest, TestResponse> method = BuildMethod();
        Strategy<TestRequest> requestStrategy = Strategy.Just(new TestRequest(5));

        Strategy<GrpcInteraction> strategy = GenerateGrpc.Unary("svc", method, requestStrategy);
        GrpcInteraction interaction = strategy.WithSeed(1UL).Sample();

        Assert.Empty(interaction.Metadata);
    }

    [Fact]
    public void Unary_CustomMetadata_FlowsThroughToInteraction()
    {
        Method<TestRequest, TestResponse> method = BuildMethod();
        Strategy<TestRequest> requestStrategy = Strategy.Just(new TestRequest(5));
        Dictionary<string, string> customMeta = new() { ["authorization"] = "Bearer tok" };

        Strategy<GrpcInteraction> strategy = GenerateGrpc.Unary("svc", method, requestStrategy, customMeta);
        GrpcInteraction interaction = strategy.WithSeed(1UL).Sample();

        Assert.Equal("Bearer tok", interaction.Metadata["authorization"]);
    }
}