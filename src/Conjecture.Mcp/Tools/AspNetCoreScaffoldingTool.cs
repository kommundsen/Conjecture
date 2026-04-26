// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.ComponentModel;
using System.Text;

using ModelContextProtocol.Server;

namespace Conjecture.Mcp.Tools;

[McpServerToolType]
internal static class AspNetCoreScaffoldingTool
{
    [McpServerTool(Name = "scaffold-aspnetcore-property-test")]
    [Description(
        "Generates a Conjecture.AspNetCore property-test skeleton for a given entry-point type, route, and framework. " +
        "Emits a ready-to-fill test class with the correct using directives, WebApplicationFactory wiring, " +
        "Generate.AspNetCoreRequests strategy construction, and the AssertNot5xx invariant. " +
        "When bodyType is provided, emits a paired malformed-request test asserting Assert4xx.")]
    public static string ScaffoldAspNetCorePropertyTest(
        [Description("ASP.NET Core entry-point type, e.g. 'Program'. Required.")] string entryPointType,
        [Description("Endpoint route to focus on, e.g. '/orders'. Used as a filter and in the test method name. Required.")] string endpointRoute,
        [Description("HTTP method: 'GET' (default), 'POST', 'PUT', 'DELETE', 'PATCH'.")] string httpMethod = "GET",
        [Description("Optional body DTO type name, e.g. 'CreateOrderRequest'. When set, a paired malformed-request test is emitted.")] string? bodyType = null,
        [Description("Test framework: 'xunit' (default), 'xunit-v3', 'nunit', or 'mstest'.")] string framework = "xunit")
    {
        string fw = framework.ToLowerInvariant();
        string method = httpMethod.ToUpperInvariant();
        string sanitizedRoute = SanitizeRoute(endpointRoute);

        StringBuilder sb = new();
        AppendUsings(sb, fw);
        sb.AppendLine();
        AppendClassBody(sb, fw, entryPointType, endpointRoute, method, sanitizedRoute, bodyType);
        return sb.ToString();
    }

    private static string SanitizeRoute(string route)
    {
        StringBuilder sb = new(route.Length);
        bool capitalizeNext = true;
        foreach (char c in route)
        {
            if (char.IsLetterOrDigit(c))
            {
                sb.Append(capitalizeNext ? char.ToUpperInvariant(c) : c);
                capitalizeNext = false;
            }
            else
            {
                capitalizeNext = true;
            }
        }
        string name = sb.ToString();
        return string.IsNullOrEmpty(name) ? "Endpoint" : name;
    }

    private static void AppendUsings(StringBuilder sb, string framework)
    {
        sb.AppendLine("using System.Net.Http;");
        sb.AppendLine("using System.Threading;");
        sb.AppendLine("using System.Threading.Tasks;");
        sb.AppendLine("using Conjecture.AspNetCore;");
        sb.AppendLine("using Conjecture.Core;");
        sb.AppendLine("using Conjecture.Http;");
        sb.AppendLine("using Microsoft.AspNetCore.Mvc.Testing;");
        sb.AppendLine("using Microsoft.Extensions.DependencyInjection;");
        sb.AppendLine("using Microsoft.Extensions.Hosting;");

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

    private static void AppendClassBody(
        StringBuilder sb,
        string framework,
        string entryPointType,
        string endpointRoute,
        string httpMethod,
        string sanitizedRoute,
        string? bodyType)
    {
        string attrLine = framework switch
        {
            "nunit" => "    [Conjecture.NUnit.Property]",
            "mstest" => "    [Conjecture.MSTest.Property]",
            _ => "    [Property]",
        };

        sb.AppendLine($"public class {sanitizedRoute}PropertyTests");
        sb.AppendLine("{");

        sb.AppendLine(attrLine);
        sb.AppendLine($"    public async Task {sanitizedRoute}_NeverReturns5xx_OnValidRequests(CancellationToken ct)");
        sb.AppendLine("    {");
        sb.AppendLine($"        await using WebApplicationFactory<{entryPointType}> factory = new();");
        sb.AppendLine("        using HttpClient client = factory.CreateClient();");
        sb.AppendLine("        IHost host = factory.Services.GetRequiredService<IHost>();");
        sb.AppendLine("        HostHttpTarget target = new(host, client);");
        sb.AppendLine();
        sb.AppendLine("        Strategy<HttpInteraction> strategy = Generate.AspNetCoreRequests(host, client)");
        sb.AppendLine($"            .ExcludeEndpoints(static ep => !string.Equals(ep.RoutePattern.RawText, \"{endpointRoute}\", System.StringComparison.OrdinalIgnoreCase) || !string.Equals(ep.HttpMethod, \"{httpMethod}\", System.StringComparison.OrdinalIgnoreCase))");
        sb.AppendLine("            .ValidRequestsOnly()");
        sb.AppendLine("            .Build();");
        sb.AppendLine();
        sb.AppendLine("        await Property.ForAll(target, strategy, static async (t, request) =>");
        sb.AppendLine("        {");
        sb.AppendLine("            HttpResponseMessage response = await request.Response((IHttpTarget)t);");
        sb.AppendLine("            await Task.FromResult(response).AssertNot5xx();");
        sb.AppendLine("        }, ct: ct);");
        sb.AppendLine("    }");

        if (!string.IsNullOrEmpty(bodyType))
        {
            sb.AppendLine();
            sb.AppendLine(attrLine);
            sb.AppendLine($"    public async Task {sanitizedRoute}_Returns4xx_OnMalformedRequests(CancellationToken ct)");
            sb.AppendLine("    {");
            sb.AppendLine($"        await using WebApplicationFactory<{entryPointType}> factory = new();");
            sb.AppendLine("        using HttpClient client = factory.CreateClient();");
            sb.AppendLine("        IHost host = factory.Services.GetRequiredService<IHost>();");
            sb.AppendLine("        HostHttpTarget target = new(host, client);");
            sb.AppendLine();
            sb.AppendLine("        Strategy<HttpInteraction> malformed = Generate.AspNetCoreRequests(host, client)");
            sb.AppendLine($"            .ExcludeEndpoints(static ep => !string.Equals(ep.RoutePattern.RawText, \"{endpointRoute}\", System.StringComparison.OrdinalIgnoreCase) || !string.Equals(ep.HttpMethod, \"{httpMethod}\", System.StringComparison.OrdinalIgnoreCase))");
            sb.AppendLine("            .MalformedRequestsOnly()");
            sb.AppendLine("            .Build();");
            sb.AppendLine();
            sb.AppendLine("        await Property.ForAll(target, malformed, static async (t, request) =>");
            sb.AppendLine("        {");
            sb.AppendLine("            HttpResponseMessage response = await request.Response((IHttpTarget)t);");
            sb.AppendLine("            await Task.FromResult(response).Assert4xx();");
            sb.AppendLine("        }, ct: ct);");
            sb.AppendLine($"        // Body type for reference: {bodyType}");
            sb.AppendLine("    }");
        }

        sb.AppendLine("}");
    }
}