// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.ComponentModel;
using System.Text;

using ModelContextProtocol.Server;

namespace Conjecture.Mcp.Tools;

[McpServerToolType]
internal static class MessagingScaffoldingTool
{
    [McpServerTool(Name = "scaffold-messaging-property-test")]
    [Description(
        "Generates a Conjecture messaging property-test skeleton for a given broker and framework. " +
        "Emits a ready-to-fill test class with the correct using directives, adapter construction, " +
        "and a Property.ForAll round-trip skeleton.")]
    public static string ScaffoldMessagingPropertyTest(
        [Description("The destination (queue/topic) name. Required.")] string destination,
        [Description("Test framework: 'xunit' (default), 'xunit-v3', 'nunit', or 'mstest'")] string framework = "xunit",
        [Description("Message broker: 'inmemory' (default), 'azureservicebus', or 'rabbitmq'")] string broker = "inmemory",
        [Description("Body type: 'bytes' (default), 'protobuf', or 'jsonschema'")] string bodyType = "bytes")
    {
        string effectiveDestination = string.IsNullOrWhiteSpace(destination)
            ? "YOUR_DESTINATION"
            : destination;

        string fw = framework.ToLowerInvariant();
        string bk = broker.ToLowerInvariant();
        string bt = bodyType.ToLowerInvariant();

        StringBuilder sb = new();

        AppendUsings(sb, fw, bk);
        sb.AppendLine();
        AppendClassAndFixture(sb, fw, bk, effectiveDestination, bt);

        return sb.ToString();
    }

    private static void AppendUsings(StringBuilder sb, string framework, string broker)
    {
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Threading;");
        sb.AppendLine("using System.Threading.Tasks;");
        sb.AppendLine("using Conjecture.Core;");
        sb.AppendLine("using Conjecture.Messaging;");

        switch (broker)
        {
            case "azureservicebus":
                sb.AppendLine("using Conjecture.Messaging.AzureServiceBus;");
                break;
            case "rabbitmq":
                sb.AppendLine("using Conjecture.Messaging.RabbitMq;");
                break;
        }

        switch (framework)
        {
            case "xunit-v3":
                sb.AppendLine("using Conjecture.Xunit.V3;");
                sb.AppendLine("using Xunit;");
                break;
            case "nunit":
                sb.AppendLine("using Conjecture.NUnit;");
                sb.AppendLine("using NUnit.Framework;");
                break;
            case "mstest":
                sb.AppendLine("using Conjecture.MSTest;");
                sb.AppendLine("using Microsoft.VisualStudio.TestTools.UnitTesting;");
                break;
            default:
                sb.AppendLine("using Conjecture.Xunit;");
                sb.AppendLine("using Xunit;");
                break;
        }
    }

    private static void AppendClassAndFixture(StringBuilder sb, string framework, string broker, string destination, string bodyType)
    {
        bool needsFixture = broker is "azureservicebus" or "rabbitmq";

        if (needsFixture)
        {
            AppendFixtureClass(sb, framework, broker);
            sb.AppendLine();
        }

        AppendTestClassDeclaration(sb, framework, needsFixture);
        sb.AppendLine("{");

        if (needsFixture)
        {
            AppendFixtureSetup(sb, framework);
        }

        AppendTestMethod(sb, framework, destination, bodyType, needsFixture);

        sb.AppendLine("}");
    }

    private static void AppendFixtureClass(StringBuilder sb, string framework, string broker)
    {
        switch (framework)
        {
            case "nunit":
                sb.AppendLine("public class MessagingFixture");
                sb.AppendLine("{");
                AppendBrokerField(sb, broker);
                sb.AppendLine();
                sb.AppendLine("    [OneTimeSetUp]");
                sb.AppendLine("    public async Task SetUpAsync()");
                sb.AppendLine("    {");
                AppendBrokerConnect(sb, broker, "        ");
                sb.AppendLine("    }");
                sb.AppendLine();
                sb.AppendLine("    [OneTimeTearDown]");
                sb.AppendLine("    public async Task TearDownAsync()");
                sb.AppendLine("    {");
                sb.AppendLine("        await Target.DisposeAsync();");
                sb.AppendLine("    }");
                sb.AppendLine("}");
                break;
            case "mstest":
                sb.AppendLine("public class MessagingFixture");
                sb.AppendLine("{");
                AppendBrokerField(sb, broker);
                sb.AppendLine();
                sb.AppendLine("    [ClassInitialize]");
                sb.AppendLine("    public static async Task InitializeAsync(TestContext context)");
                sb.AppendLine("    {");
                AppendBrokerConnect(sb, broker, "        ");
                sb.AppendLine("    }");
                sb.AppendLine();
                sb.AppendLine("    [ClassCleanup]");
                sb.AppendLine("    public static async Task CleanupAsync()");
                sb.AppendLine("    {");
                sb.AppendLine("        await Target.DisposeAsync();");
                sb.AppendLine("    }");
                sb.AppendLine("}");
                break;
            default:
                // xunit / xunit-v3: IClassFixture
                sb.AppendLine("public class MessagingFixture : IAsyncLifetime");
                sb.AppendLine("{");
                AppendBrokerField(sb, broker);
                sb.AppendLine();
                sb.AppendLine("    public async Task InitializeAsync()");
                sb.AppendLine("    {");
                AppendBrokerConnect(sb, broker, "        ");
                sb.AppendLine("    }");
                sb.AppendLine();
                sb.AppendLine("    public async Task DisposeAsync()");
                sb.AppendLine("    {");
                sb.AppendLine("        await Target.DisposeAsync();");
                sb.AppendLine("    }");
                sb.AppendLine("}");
                break;
        }
    }

    private static void AppendBrokerField(StringBuilder sb, string broker)
    {
        switch (broker)
        {
            case "azureservicebus":
                sb.AppendLine("    public AzureServiceBusTarget Target { get; private set; } = null!;");
                break;
            case "rabbitmq":
                sb.AppendLine("    public RabbitMqTarget Target { get; private set; } = null!;");
                break;
        }
    }

    private static void AppendBrokerConnect(StringBuilder sb, string broker, string indent)
    {
        switch (broker)
        {
            case "azureservicebus":
                sb.AppendLine($"{indent}Target = AzureServiceBusTarget.Connect(\"YOUR_CONNECTION_STRING\");");
                break;
            case "rabbitmq":
                sb.AppendLine($"{indent}Target = await RabbitMqTarget.ConnectAsync(\"YOUR_CONNECTION_STRING\", CancellationToken.None);");
                break;
        }
    }

    private static void AppendTestClassDeclaration(StringBuilder sb, string framework, bool needsFixture)
    {
        if (needsFixture && framework is "xunit" or "xunit-v3")
        {
            sb.AppendLine("public class MessagingPropertyTests : IClassFixture<MessagingFixture>");
        }
        else
        {
            sb.AppendLine("public class MessagingPropertyTests");
        }
    }

    private static void AppendFixtureSetup(StringBuilder sb, string framework)
    {
        switch (framework)
        {
            case "xunit":
            case "xunit-v3":
                sb.AppendLine("    private readonly MessagingFixture fixture;");
                sb.AppendLine();
                sb.AppendLine("    public MessagingPropertyTests(MessagingFixture fixture)");
                sb.AppendLine("    {");
                sb.AppendLine("        this.fixture = fixture;");
                sb.AppendLine("    }");
                sb.AppendLine();
                break;
        }
    }

    private static void AppendTestMethod(StringBuilder sb, string framework, string destination, string bodyType, bool needsFixture)
    {
        string attrLine = framework switch
        {
            "nunit" => "    [Conjecture.NUnit.Property]",
            "mstest" => "    [Conjecture.MSTest.Property]",
            _ => "    [Property]"
        };

        sb.AppendLine(attrLine);
        sb.AppendLine("    public async Task RoundTrip_Message_SatisfiesProperty(CancellationToken ct)");
        sb.AppendLine("    {");

        if (!needsFixture)
        {
            sb.AppendLine("        InMemoryMessageBusTarget bus = new();");
        }

        sb.AppendLine();
        AppendBodyTypeComment(sb, bodyType);
        sb.AppendLine("        await Property.ForAll(");
        sb.AppendLine(needsFixture ? "            fixture.Target," : "            bus,");
        sb.AppendLine($"            Strategy.Messaging.Publish(\"{destination}\", Strategy.Arrays(Strategy.Integers<byte>(), 0, 1024).Select(b => (ReadOnlyMemory<byte>)b)),");
        sb.AppendLine("            async (target, sent) =>");
        sb.AppendLine("            {");
        sb.AppendLine("                await target.ExecuteAsync(sent, ct);");
        sb.AppendLine($"                MessageInteraction? received = await target.ReceiveAsync(\"{destination}\", TimeSpan.FromSeconds(1), ct);");
        sb.AppendLine("                Assert.NotNull(received);");
        sb.AppendLine("                Assert.Equal(sent.MessageId, received.MessageId);");
        sb.AppendLine("                Assert.Equal(sent.Body.ToArray(), received.Body.ToArray());");
        sb.AppendLine("                await target.AcknowledgeAsync(received, ct);");
        sb.AppendLine("            },");
        sb.AppendLine("            ct);");
        sb.AppendLine("    }");
    }

    private static void AppendBodyTypeComment(StringBuilder sb, string bodyType)
    {
        switch (bodyType)
        {
            case "protobuf":
                sb.AppendLine("        // Suggested strategy: Strategy.FromProtobuf<T>()");
                break;
            case "jsonschema":
                sb.AppendLine("        // Suggested strategy: Strategy.FromJsonSchema(schema)");
                break;
        }
    }
}