// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Mcp.Tools;

namespace Conjecture.Mcp.Tests.Tools;

public class StrategyToolsAspNetCoreEFCoreTypesTests
{
    [Theory]
    [InlineData("WebApplicationFactory<MyApp>")]
    [InlineData("AspNetCoreDbTarget<OrdersContext>")]
    public void SuggestStrategy_DbContextInWebHostType_SuggestsCompositeInvariants(string typeName)
    {
        string result = StrategyTools.SuggestForType(typeName);

        Assert.Contains("AspNetCoreDbTarget", result);
        bool mentionsInvariant =
            result.Contains("AssertNoPartialWritesOnErrorAsync", StringComparison.Ordinal) ||
            result.Contains("AssertCascadeCorrectnessAsync", StringComparison.Ordinal) ||
            result.Contains("AssertIdempotentAsync", StringComparison.Ordinal);
        Assert.True(mentionsInvariant, $"Expected at least one AspNetCoreEFCore invariant method name in: {result}");
    }

    [Fact]
    public void SuggestStrategy_PlainDbContext_StillSuggestsEFCoreOnly_NotComposite()
    {
        string result = StrategyTools.SuggestForType("DbContext");

        Assert.Contains("Generate.Entity", result);
        Assert.DoesNotContain("AspNetCoreDbTarget", result, StringComparison.Ordinal);
        Assert.DoesNotContain("AssertNoPartialWritesOnErrorAsync", result, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("int")]
    [InlineData("Guid")]
    public void SuggestStrategy_NonAspNetCoreEFCoreType_DoesNotMentionComposite(string typeName)
    {
        string result = StrategyTools.SuggestForType(typeName);

        Assert.DoesNotContain("AspNetCoreDbTarget", result, StringComparison.Ordinal);
        Assert.DoesNotContain("AssertNoPartialWritesOnErrorAsync", result, StringComparison.Ordinal);
        Assert.DoesNotContain("AssertCascadeCorrectnessAsync", result, StringComparison.Ordinal);
        Assert.DoesNotContain("AssertIdempotentAsync", result, StringComparison.Ordinal);
    }
}
