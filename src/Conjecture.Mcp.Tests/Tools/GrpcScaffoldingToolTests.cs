// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Mcp.Tools;

namespace Conjecture.Mcp.Tests.Tools;

public class GrpcScaffoldingToolTests
{
    // --- default invocation: key tokens ---

    [Fact]
    public void ScaffoldGrpcPropertyTest_DefaultParams_ContainsConjectureGrpcUsing()
    {
        string result = GrpcScaffoldingTool.ScaffoldGrpcPropertyTest(
            serviceName: "Greeter",
            methodName: "SayHello");

        Assert.Contains("Conjecture.Grpc", result);
    }

    [Fact]
    public void ScaffoldGrpcPropertyTest_DefaultParams_ContainsGenerateGrpcUnary()
    {
        string result = GrpcScaffoldingTool.ScaffoldGrpcPropertyTest(
            serviceName: "Greeter",
            methodName: "SayHello");

        Assert.Contains("Generate.Grpc.Unary", result);
    }

    [Fact]
    public void ScaffoldGrpcPropertyTest_DefaultParams_ContainsPropertyAttribute()
    {
        string result = GrpcScaffoldingTool.ScaffoldGrpcPropertyTest(
            serviceName: "Greeter",
            methodName: "SayHello");

        Assert.Contains("[Property]", result);
    }

    [Fact]
    public void ScaffoldGrpcPropertyTest_DefaultParams_ContainsAssertStatusOk()
    {
        string result = GrpcScaffoldingTool.ScaffoldGrpcPropertyTest(
            serviceName: "Greeter",
            methodName: "SayHello");

        Assert.Contains("AssertStatusOk", result);
    }

    [Fact]
    public void ScaffoldGrpcPropertyTest_DefaultParams_ContainsExecuteAsync()
    {
        string result = GrpcScaffoldingTool.ScaffoldGrpcPropertyTest(
            serviceName: "Greeter",
            methodName: "SayHello");

        Assert.Contains("ExecuteAsync", result);
    }

    // --- methodType: strategy call ---

    [Theory]
    [InlineData("unary", "Generate.Grpc.Unary")]
    [InlineData("server-stream", "Generate.Grpc.ServerStream")]
    [InlineData("client-stream", "Generate.Grpc.ClientStream")]
    [InlineData("bidi", "Generate.Grpc.BidiStream")]
    public void ScaffoldGrpcPropertyTest_MethodType_EmitsCorrectStrategyCall(string methodType, string expectedStrategy)
    {
        string result = GrpcScaffoldingTool.ScaffoldGrpcPropertyTest(
            serviceName: "Greeter",
            methodName: "SayHello",
            methodType: methodType);

        Assert.Contains(expectedStrategy, result);
    }

    // --- framework: using directives and attributes ---

    [Fact]
    public void ScaffoldGrpcPropertyTest_XunitFramework_ContainsXunitUsing()
    {
        string result = GrpcScaffoldingTool.ScaffoldGrpcPropertyTest(
            serviceName: "Greeter",
            methodName: "SayHello",
            framework: "xunit");

        Assert.Contains("using Conjecture.Xunit;", result);
        Assert.Contains("[Property]", result);
    }

    [Fact]
    public void ScaffoldGrpcPropertyTest_XunitV3Framework_ContainsXunitV3Using()
    {
        string result = GrpcScaffoldingTool.ScaffoldGrpcPropertyTest(
            serviceName: "Greeter",
            methodName: "SayHello",
            framework: "xunit-v3");

        Assert.Contains("using Conjecture.Xunit.V3;", result);
    }

    [Fact]
    public void ScaffoldGrpcPropertyTest_NUnitFramework_ContainsNUnitUsing()
    {
        string result = GrpcScaffoldingTool.ScaffoldGrpcPropertyTest(
            serviceName: "Greeter",
            methodName: "SayHello",
            framework: "nunit");

        Assert.Contains("using Conjecture.NUnit;", result);
        Assert.Contains("using NUnit.Framework;", result);
    }

    [Fact]
    public void ScaffoldGrpcPropertyTest_MSTestFramework_ContainsMSTestUsing()
    {
        string result = GrpcScaffoldingTool.ScaffoldGrpcPropertyTest(
            serviceName: "Greeter",
            methodName: "SayHello",
            framework: "mstest");

        Assert.Contains("using Conjecture.MSTest;", result);
        Assert.Contains("using Microsoft.VisualStudio.TestTools.UnitTesting;", result);
    }

    [Fact]
    public void ScaffoldGrpcPropertyTest_NUnitFramework_ContainsNUnitPropertyAttribute()
    {
        string result = GrpcScaffoldingTool.ScaffoldGrpcPropertyTest(
            serviceName: "Greeter",
            methodName: "SayHello",
            framework: "nunit");

        Assert.Contains("[Conjecture.NUnit.Property]", result);
    }

    [Fact]
    public void ScaffoldGrpcPropertyTest_MSTestFramework_ContainsMSTestPropertyAttribute()
    {
        string result = GrpcScaffoldingTool.ScaffoldGrpcPropertyTest(
            serviceName: "Greeter",
            methodName: "SayHello",
            framework: "mstest");

        Assert.Contains("[Conjecture.MSTest.Property]", result);
    }

    // --- target: HostGrpcTarget vs GrpcChannelTarget ---

    [Fact]
    public void ScaffoldGrpcPropertyTest_HostTarget_ContainsHostGrpcTarget()
    {
        string result = GrpcScaffoldingTool.ScaffoldGrpcPropertyTest(
            serviceName: "Greeter",
            methodName: "SayHello",
            target: "host");

        Assert.Contains("HostGrpcTarget", result);
        Assert.DoesNotContain("GrpcChannelTarget", result);
    }

    [Fact]
    public void ScaffoldGrpcPropertyTest_ChannelTarget_ContainsGrpcChannelTarget()
    {
        string result = GrpcScaffoldingTool.ScaffoldGrpcPropertyTest(
            serviceName: "Greeter",
            methodName: "SayHello",
            target: "channel");

        Assert.Contains("GrpcChannelTarget", result);
        Assert.DoesNotContain("HostGrpcTarget", result);
    }

    // --- service/method name interpolation ---

    [Theory]
    [InlineData("Greeter", "SayHello")]
    [InlineData("OrderService", "PlaceOrder")]
    [InlineData("PaymentService", "Charge")]
    public void ScaffoldGrpcPropertyTest_ServiceAndMethodNames_FlowThroughToOutput(string serviceName, string methodName)
    {
        string result = GrpcScaffoldingTool.ScaffoldGrpcPropertyTest(
            serviceName: serviceName,
            methodName: methodName);

        Assert.Contains(serviceName, result);
        Assert.Contains(methodName, result);
    }

    // --- representative cross-product: methodType × framework × target ---

    [Theory]
    [InlineData("unary", "xunit", "host", "Generate.Grpc.Unary", "using Conjecture.Xunit;", "HostGrpcTarget")]
    [InlineData("server-stream", "xunit-v3", "channel", "Generate.Grpc.ServerStream", "using Conjecture.Xunit.V3;", "GrpcChannelTarget")]
    [InlineData("client-stream", "nunit", "host", "Generate.Grpc.ClientStream", "using Conjecture.NUnit;", "HostGrpcTarget")]
    [InlineData("bidi", "mstest", "channel", "Generate.Grpc.BidiStream", "using Conjecture.MSTest;", "GrpcChannelTarget")]
    public void ScaffoldGrpcPropertyTest_RepresentativeCombinations_ContainsExpectedTokens(
        string methodType,
        string framework,
        string target,
        string expectedStrategy,
        string expectedUsing,
        string expectedTargetType)
    {
        string result = GrpcScaffoldingTool.ScaffoldGrpcPropertyTest(
            serviceName: "TestService",
            methodName: "TestMethod",
            methodType: methodType,
            framework: framework,
            target: target);

        Assert.Contains(expectedStrategy, result);
        Assert.Contains(expectedUsing, result);
        Assert.Contains(expectedTargetType, result);
        Assert.Contains("AssertStatusOk", result);
    }
}