// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.ComponentModel;
using System.Text;

using ModelContextProtocol.Server;

namespace Conjecture.Mcp.Tools;

[McpServerToolType]
internal static class GrpcScaffoldingTool
{
    [McpServerTool(Name = "scaffold-grpc-property-test")]
    [Description(
        "Generates a Conjecture gRPC property-test skeleton for a given service, method, and framework. " +
        "Emits a ready-to-fill test class with the correct using directives, target construction, " +
        "and a Property test skeleton.")]
    public static string ScaffoldGrpcPropertyTest(
        [Description("gRPC service name, e.g. 'Greeter'. Required.")] string serviceName,
        [Description("gRPC method name, e.g. 'SayHello'. Required.")] string methodName,
        [Description("Method type: 'unary' (default), 'server-stream', 'client-stream', or 'bidi'")] string methodType = "unary",
        [Description("Test framework: 'xunit' (default), 'xunit-v3', 'nunit', or 'mstest'")] string framework = "xunit",
        [Description("Target: 'host' (default, HostGrpcTarget) or 'channel' (GrpcChannelTarget)")] string target = "host")
    {
        string fw = framework.ToLowerInvariant();
        string mt = methodType.ToLowerInvariant();
        string tgt = target.ToLowerInvariant();

        StringBuilder sb = new();

        AppendUsings(sb, fw);
        sb.AppendLine();
        AppendClassBody(sb, fw, mt, tgt, serviceName, methodName);

        return sb.ToString();
    }

    private static void AppendUsings(StringBuilder sb, string framework)
    {
        sb.AppendLine("using System.Threading;");
        sb.AppendLine("using System.Threading.Tasks;");
        sb.AppendLine("using Conjecture.Core;");
        sb.AppendLine("using Conjecture.Grpc;");

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

    private static void AppendClassBody(StringBuilder sb, string framework, string methodType, string target, string serviceName, string methodName)
    {
        sb.AppendLine($"public class {serviceName}PropertyTests");
        sb.AppendLine("{");

        string attrLine = framework switch
        {
            "nunit" => "    [Conjecture.NUnit.Property]",
            "mstest" => "    [Conjecture.MSTest.Property]",
            _ => "    [Property]"
        };

        string strategyCall = methodType switch
        {
            "server-stream" => "Strategy.Grpc.ServerStream",
            "client-stream" => "Strategy.Grpc.ClientStream",
            "bidi" => "Strategy.Grpc.BidiStream",
            _ => "Strategy.Grpc.Unary"
        };

        string targetType = target == "channel" ? "GrpcChannelTarget" : "HostGrpcTarget";

        sb.AppendLine(attrLine);
        sb.AppendLine($"    public async Task {methodName}_SatisfiesProperty(CancellationToken ct)");
        sb.AppendLine("    {");
        sb.AppendLine($"        {targetType} grpcTarget = new();");
        sb.AppendLine();
        sb.AppendLine("        await Property.ForAll(");
        sb.AppendLine($"            {strategyCall}(grpcTarget),");
        sb.AppendLine("            async (request) =>");
        sb.AppendLine("            {");
        sb.AppendLine("                GrpcResponse response = await grpcTarget.ExecuteAsync(request, ct);");
        sb.AppendLine("                response.AssertStatusOk();");
        sb.AppendLine("            },");
        sb.AppendLine("            ct);");
        sb.AppendLine("    }");

        sb.AppendLine("}");
    }
}