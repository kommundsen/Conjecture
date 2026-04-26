// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Mcp.Tools;

namespace Conjecture.Mcp.Tests.Tools;

public class AspNetCoreScaffoldingToolTests
{
    [Fact]
    public void Scaffold_DefaultParams_ContainsAspNetCoreUsing()
    {
        string result = AspNetCoreScaffoldingTool.ScaffoldAspNetCorePropertyTest(
            entryPointType: "Program",
            endpointRoute: "/orders");

        Assert.Contains("using Conjecture.AspNetCore;", result);
    }

    [Fact]
    public void Scaffold_DefaultParams_ContainsGenerateAspNetCoreRequests()
    {
        string result = AspNetCoreScaffoldingTool.ScaffoldAspNetCorePropertyTest(
            entryPointType: "Program",
            endpointRoute: "/orders");

        Assert.Contains("Generate.AspNetCoreRequests", result);
    }

    [Fact]
    public void Scaffold_DefaultParams_ContainsAssertNot5xx()
    {
        string result = AspNetCoreScaffoldingTool.ScaffoldAspNetCorePropertyTest(
            entryPointType: "Program",
            endpointRoute: "/orders");

        Assert.Contains("AssertNot5xx", result);
    }

    [Fact]
    public void Scaffold_DefaultParams_ContainsWebApplicationFactory()
    {
        string result = AspNetCoreScaffoldingTool.ScaffoldAspNetCorePropertyTest(
            entryPointType: "MyApp",
            endpointRoute: "/items");

        Assert.Contains("WebApplicationFactory<MyApp>", result);
    }

    [Fact]
    public void Scaffold_DefaultParams_OmitsMalformedTest()
    {
        string result = AspNetCoreScaffoldingTool.ScaffoldAspNetCorePropertyTest(
            entryPointType: "Program",
            endpointRoute: "/orders");

        Assert.DoesNotContain("MalformedRequestsOnly", result);
        Assert.DoesNotContain("Assert4xx", result);
    }

    [Fact]
    public void Scaffold_WithBodyType_ContainsMalformedRequestsOnly()
    {
        string result = AspNetCoreScaffoldingTool.ScaffoldAspNetCorePropertyTest(
            entryPointType: "Program",
            endpointRoute: "/orders",
            httpMethod: "POST",
            bodyType: "CreateOrderRequest");

        Assert.Contains("MalformedRequestsOnly", result);
    }

    [Fact]
    public void Scaffold_WithBodyType_ContainsAssert4xx()
    {
        string result = AspNetCoreScaffoldingTool.ScaffoldAspNetCorePropertyTest(
            entryPointType: "Program",
            endpointRoute: "/orders",
            httpMethod: "POST",
            bodyType: "CreateOrderRequest");

        Assert.Contains("Assert4xx", result);
    }

    [Fact]
    public void Scaffold_WithBodyType_ReferencesBodyTypeName()
    {
        string result = AspNetCoreScaffoldingTool.ScaffoldAspNetCorePropertyTest(
            entryPointType: "Program",
            endpointRoute: "/orders",
            httpMethod: "POST",
            bodyType: "CreateOrderRequest");

        Assert.Contains("CreateOrderRequest", result);
    }

    [Theory]
    [InlineData("xunit", "using Conjecture.Xunit;", "[Property]")]
    [InlineData("xunit-v3", "using Conjecture.Xunit.V3;", "[Property]")]
    [InlineData("nunit", "using Conjecture.NUnit;", "[Conjecture.NUnit.Property]")]
    [InlineData("mstest", "using Conjecture.MSTest;", "[Conjecture.MSTest.Property]")]
    public void Scaffold_Framework_EmitsCorrectUsingsAndAttribute(string framework, string expectedUsing, string expectedAttribute)
    {
        string result = AspNetCoreScaffoldingTool.ScaffoldAspNetCorePropertyTest(
            entryPointType: "Program",
            endpointRoute: "/orders",
            framework: framework);

        Assert.Contains(expectedUsing, result);
        Assert.Contains(expectedAttribute, result);
    }

    [Theory]
    [InlineData("/orders", "Orders")]
    [InlineData("/api/v1/orders", "ApiV1Orders")]
    [InlineData("/users/{id}", "UsersId")]
    public void Scaffold_RouteSanitization_ProducesValidIdentifier(string route, string expectedFragment)
    {
        string result = AspNetCoreScaffoldingTool.ScaffoldAspNetCorePropertyTest(
            entryPointType: "Program",
            endpointRoute: route);

        Assert.Contains($"{expectedFragment}PropertyTests", result);
    }

    [Fact]
    public void Scaffold_HttpMethod_FlowsThroughToOutput()
    {
        string result = AspNetCoreScaffoldingTool.ScaffoldAspNetCorePropertyTest(
            entryPointType: "Program",
            endpointRoute: "/orders",
            httpMethod: "POST");

        Assert.Contains("\"POST\"", result);
    }
}