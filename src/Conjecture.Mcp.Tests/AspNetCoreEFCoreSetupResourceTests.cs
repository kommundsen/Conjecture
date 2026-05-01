// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Mcp.Resources;

using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Conjecture.Mcp.Tests;

public class AspNetCoreEFCoreSetupResourceTests
{

    [Fact]
    public async Task AspNetCoreEFCoreSetupResource_ExistsInListResources()
    {
        ListResourcesResult result = await ApiReferenceResources.HandleListResources(
            ResourceTestHelpers.MakeUninitializedContext<ListResourcesRequestParams>(), default);

        Assert.Contains(result.Resources, r => r.Uri == "conjecture://api/aspnetcore-efcore-setup");
    }

    [Theory]
    [InlineData("AspNetCoreDbTarget")]
    [InlineData("AssertNoPartialWritesOnErrorAsync")]
    [InlineData("AssertCascadeCorrectnessAsync")]
    [InlineData("AssertIdempotentAsync")]
    public async Task AspNetCoreEFCoreSetupResource_ExistsAndContainsExpectedSnippets(string expectedTerm)
    {
        RequestContext<ReadResourceRequestParams> context =
            ResourceTestHelpers.MakeReadContext("conjecture://api/aspnetcore-efcore-setup");

        ReadResourceResult result = await ApiReferenceResources.HandleReadResource(
            context, default);

        TextResourceContents contents = Assert.IsType<TextResourceContents>(result.Contents[0]);
        Assert.Contains(expectedTerm, contents.Text);
    }

    [Fact]
    public async Task AspNetCoreEFCoreSetupResource_ContentMentionsTestHostWiring()
    {
        RequestContext<ReadResourceRequestParams> context =
            ResourceTestHelpers.MakeReadContext("conjecture://api/aspnetcore-efcore-setup");

        ReadResourceResult result = await ApiReferenceResources.HandleReadResource(
            context, default);

        TextResourceContents contents = Assert.IsType<TextResourceContents>(result.Contents[0]);
        bool mentionsHostWiring =
            contents.Text.Contains("WebApplicationFactory", StringComparison.Ordinal) ||
            contents.Text.Contains("IClassFixture", StringComparison.Ordinal);
        Assert.True(mentionsHostWiring, "Expected WebApplicationFactory or IClassFixture in aspnetcore-efcore-setup content");
    }
}