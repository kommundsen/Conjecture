// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Mcp.Tools;

namespace Conjecture.Mcp.Tests.Tools;

public class MessagingScaffoldingToolTests
{
    // --- framework × broker: using directives ---

    [Fact]
    public void ScaffoldMessagingPropertyTest_XunitInMemory_ContainsXunitUsing()
    {
        string result = MessagingScaffoldingTool.ScaffoldMessagingPropertyTest(
            destination: "orders",
            framework: "xunit",
            broker: "inmemory");

        Assert.Contains("using Conjecture.Xunit;", result);
    }

    [Fact]
    public void ScaffoldMessagingPropertyTest_InMemory_ReceiveAsyncReturnsMessageInteraction()
    {
        string result = MessagingScaffoldingTool.ScaffoldMessagingPropertyTest(
            destination: "orders",
            framework: "xunit",
            broker: "inmemory");

        Assert.Contains("MessageInteraction? received = await target.ReceiveAsync(\"orders\", TimeSpan.FromSeconds(1), ct);", result);
        Assert.DoesNotContain("byte[] received", result);
    }

    [Fact]
    public void ScaffoldMessagingPropertyTest_AzureServiceBus_FixturePathReferencesFixtureTarget()
    {
        string result = MessagingScaffoldingTool.ScaffoldMessagingPropertyTest(
            destination: "orders",
            framework: "xunit-v3",
            broker: "azureservicebus");

        Assert.Contains("fixture.Target,", result);
    }

    [Fact]
    public void ScaffoldMessagingPropertyTest_XunitV3AzureServiceBus_ContainsXunitV3Using()
    {
        string result = MessagingScaffoldingTool.ScaffoldMessagingPropertyTest(
            destination: "orders",
            framework: "xunit-v3",
            broker: "azureservicebus");

        Assert.Contains("using Conjecture.Xunit.V3;", result);
    }

    [Fact]
    public void ScaffoldMessagingPropertyTest_XunitV3AzureServiceBus_ContainsAzureServiceBusUsing()
    {
        string result = MessagingScaffoldingTool.ScaffoldMessagingPropertyTest(
            destination: "orders",
            framework: "xunit-v3",
            broker: "azureservicebus");

        Assert.Contains("using Conjecture.Messaging.AzureServiceBus;", result);
    }

    [Fact]
    public void ScaffoldMessagingPropertyTest_NUnitRabbitMq_ContainsNUnitUsing()
    {
        string result = MessagingScaffoldingTool.ScaffoldMessagingPropertyTest(
            destination: "orders",
            framework: "nunit",
            broker: "rabbitmq");

        Assert.Contains("using Conjecture.NUnit;", result);
    }

    [Fact]
    public void ScaffoldMessagingPropertyTest_NUnitRabbitMq_ContainsRabbitMqUsing()
    {
        string result = MessagingScaffoldingTool.ScaffoldMessagingPropertyTest(
            destination: "orders",
            framework: "nunit",
            broker: "rabbitmq");

        Assert.Contains("using Conjecture.Messaging.RabbitMq;", result);
    }

    // --- framework × broker: test attributes ---

    [Fact]
    public void ScaffoldMessagingPropertyTest_XunitInMemory_ContainsPropertyAttribute()
    {
        string result = MessagingScaffoldingTool.ScaffoldMessagingPropertyTest(
            destination: "orders",
            framework: "xunit",
            broker: "inmemory");

        Assert.Contains("[Property]", result);
    }

    [Fact]
    public void ScaffoldMessagingPropertyTest_NUnit_ContainsNUnitPropertyAttribute()
    {
        string result = MessagingScaffoldingTool.ScaffoldMessagingPropertyTest(
            destination: "orders",
            framework: "nunit",
            broker: "inmemory");

        Assert.Contains("[Conjecture.NUnit.Property]", result);
    }

    [Fact]
    public void ScaffoldMessagingPropertyTest_MSTest_ContainsMSTestPropertyAttribute()
    {
        string result = MessagingScaffoldingTool.ScaffoldMessagingPropertyTest(
            destination: "orders",
            framework: "mstest",
            broker: "inmemory");

        Assert.Contains("[Conjecture.MSTest.Property]", result);
    }

    // --- broker: adapter construction ---

    [Fact]
    public void ScaffoldMessagingPropertyTest_InMemory_ContainsInMemoryTarget()
    {
        string result = MessagingScaffoldingTool.ScaffoldMessagingPropertyTest(
            destination: "orders",
            framework: "xunit",
            broker: "inmemory");

        Assert.Contains("InMemoryMessageBusTarget", result);
        Assert.Contains("new()", result);
    }

    [Fact]
    public void ScaffoldMessagingPropertyTest_AzureServiceBus_ContainsConnectCall()
    {
        string result = MessagingScaffoldingTool.ScaffoldMessagingPropertyTest(
            destination: "orders",
            framework: "xunit-v3",
            broker: "azureservicebus");

        Assert.Contains("AzureServiceBusTarget.Connect(", result);
    }

    [Fact]
    public void ScaffoldMessagingPropertyTest_XunitV3AzureServiceBus_ContainsClassScopedFixtureSkeleton()
    {
        string result = MessagingScaffoldingTool.ScaffoldMessagingPropertyTest(
            destination: "orders",
            framework: "xunit-v3",
            broker: "azureservicebus");

        Assert.Contains("IClassFixture", result);
    }

    [Fact]
    public void ScaffoldMessagingPropertyTest_RabbitMq_ContainsConnectAsyncCall()
    {
        string result = MessagingScaffoldingTool.ScaffoldMessagingPropertyTest(
            destination: "orders",
            framework: "nunit",
            broker: "rabbitmq");

        Assert.Contains("RabbitMqTarget.ConnectAsync(", result);
        Assert.Contains("CancellationToken.None", result);
    }

    // --- Property.ForAll skeleton ---

    [Fact]
    public void ScaffoldMessagingPropertyTest_ContainsPropertyForAll()
    {
        string result = MessagingScaffoldingTool.ScaffoldMessagingPropertyTest(
            destination: "orders",
            framework: "xunit",
            broker: "inmemory");

        Assert.Contains("Property.ForAll(", result);
    }

    [Fact]
    public void ScaffoldMessagingPropertyTest_ContainsGenerateMessagingPublish()
    {
        string result = MessagingScaffoldingTool.ScaffoldMessagingPropertyTest(
            destination: "orders",
            framework: "xunit",
            broker: "inmemory");

        Assert.Contains("Generate.Messaging.Publish(", result);
    }

    // --- destination interpolation ---

    [Theory]
    [InlineData("orders")]
    [InlineData("payments.events")]
    [InlineData("my-queue")]
    public void ScaffoldMessagingPropertyTest_DestinationAppearsInPublishCall(string destination)
    {
        string result = MessagingScaffoldingTool.ScaffoldMessagingPropertyTest(
            destination: destination,
            framework: "xunit",
            broker: "inmemory");

        Assert.Contains($"\"{destination}\"", result);
    }

    [Theory]
    [InlineData("orders")]
    [InlineData("payments.events")]
    public void ScaffoldMessagingPropertyTest_DestinationAppearsInReceiveAsync(string destination)
    {
        string result = MessagingScaffoldingTool.ScaffoldMessagingPropertyTest(
            destination: destination,
            framework: "xunit",
            broker: "inmemory");

        Assert.Contains("ReceiveAsync(", result);
        Assert.Contains(destination, result);
    }

    // --- bodyType variants ---

    [Fact]
    public void ScaffoldMessagingPropertyTest_DefaultBodyType_ContainsGenerateBytes()
    {
        string result = MessagingScaffoldingTool.ScaffoldMessagingPropertyTest(
            destination: "orders",
            framework: "xunit",
            broker: "inmemory");

        Assert.Contains("Generate.Bytes(", result);
    }

    [Fact]
    public void ScaffoldMessagingPropertyTest_ProtobufBodyType_ContainsProtobufComment()
    {
        string result = MessagingScaffoldingTool.ScaffoldMessagingPropertyTest(
            destination: "orders",
            framework: "xunit",
            broker: "inmemory",
            bodyType: "protobuf");

        Assert.Contains("Generate.FromProtobuf<T>()", result);
    }

    [Fact]
    public void ScaffoldMessagingPropertyTest_JsonSchemaBodyType_ContainsJsonSchemaComment()
    {
        string result = MessagingScaffoldingTool.ScaffoldMessagingPropertyTest(
            destination: "orders",
            framework: "xunit",
            broker: "inmemory",
            bodyType: "jsonschema");

        Assert.Contains("Generate.FromJsonSchema(", result);
    }

    // --- required destination: empty/null ---

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void ScaffoldMessagingPropertyTest_EmptyOrNullDestination_UsesFallbackOrError(string? destination)
    {
        // The contract is either: throw ArgumentException, or emit a placeholder token.
        // Either outcome is acceptable; what is NOT acceptable is silently emitting an empty string.
        try
        {
            string result = MessagingScaffoldingTool.ScaffoldMessagingPropertyTest(
                destination: destination!,
                framework: "xunit",
                broker: "inmemory");

            // If no exception is thrown, a placeholder must appear in the output.
            Assert.False(string.IsNullOrWhiteSpace(result));
            // The output should contain SOME non-empty destination token
            // (e.g. "<destination>" or "TODO").
            Assert.True(
                result.Contains("<destination>") || result.Contains("TODO") || result.Contains("DESTINATION"),
                "Expected a placeholder token when destination is empty/null");
        }
        catch (ArgumentException)
        {
            // Throwing is also an acceptable contract.
        }
    }
}
