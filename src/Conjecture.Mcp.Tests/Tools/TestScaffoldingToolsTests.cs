// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Mcp.Tools;

namespace Conjecture.Mcp.Tests.Tools;

public class TestScaffoldingToolsTests
{
    [Fact]
    public void ParseMethodName_ReturnsLastWordBeforeParen()
    {
        Assert.Equal("Add", TestScaffoldingTools.ParseMethodName("public static int Add(int a, int b)"));
        Assert.Equal("Reverse", TestScaffoldingTools.ParseMethodName("string Reverse(string input)"));
    }

    [Fact]
    public void ParseParameters_ExtractsTypeAndName()
    {
        var result = TestScaffoldingTools.ParseParameters("public static int Add(int a, int b)");
        Assert.Equal(2, result.Count);
        Assert.Equal("int", result[0].Type);
        Assert.Equal("a", result[0].Name);
        Assert.Equal("int", result[1].Type);
        Assert.Equal("b", result[1].Name);
    }

    [Fact]
    public void ParseParameters_NoParams_ReturnsEmpty()
    {
        var result = TestScaffoldingTools.ParseParameters("void NoArgs()");
        Assert.Empty(result);
    }

    [Fact]
    public void ScaffoldPropertyTest_ContainsPropertyAttribute()
    {
        var result = TestScaffoldingTools.ScaffoldPropertyTest("public static int Add(int a, int b)");
        Assert.Contains("[Property]", result);
    }

    [Fact]
    public void ScaffoldPropertyTest_ContainsParameterNames()
    {
        var result = TestScaffoldingTools.ScaffoldPropertyTest("public static int Add(int a, int b)");
        Assert.Contains("int a", result);
        Assert.Contains("int b", result);
    }

    [Fact]
    public void ScaffoldPropertyTest_NUnit_UsesNUnitNamespace()
    {
        var result = TestScaffoldingTools.ScaffoldPropertyTest("void Test(int x)", framework: "nunit");
        Assert.Contains("Conjecture.NUnit", result);
    }

    [Fact]
    public void ScaffoldPropertyTest_MSTest_UsesMSTestNamespace()
    {
        var result = TestScaffoldingTools.ScaffoldPropertyTest("void Test(int x)", framework: "mstest");
        Assert.Contains("Conjecture.MSTest", result);
    }
}