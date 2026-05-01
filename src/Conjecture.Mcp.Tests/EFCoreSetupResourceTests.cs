// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Mcp.Resources;

using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Conjecture.Mcp.Tests;

public class EFCoreSetupResourceTests
{

    [Fact]
    public async Task EFCoreSetupResource_ExistsInListResources()
    {
        ListResourcesResult result = await ApiReferenceResources.HandleListResources(
            ResourceTestHelpers.MakeUninitializedContext<ListResourcesRequestParams>(), default);

        Assert.Contains(result.Resources, r => r.Uri == "conjecture://api/efcore-setup");
    }

    [Theory]
    [InlineData("Strategy.Entity")]
    [InlineData("Strategy.EntitySet")]
    [InlineData("RoundtripAsserter")]
    [InlineData("MigrationHarness")]
    public async Task EFCoreSetupResource_ExistsAndContainsExpectedSnippets(string expectedTerm)
    {
        RequestContext<ReadResourceRequestParams> context =
            ResourceTestHelpers.MakeReadContext("conjecture://api/efcore-setup");

        ReadResourceResult result = await ApiReferenceResources.HandleReadResource(
            context, default);

        TextResourceContents contents = Assert.IsType<TextResourceContents>(result.Contents[0]);
        Assert.Contains(expectedTerm, contents.Text);
    }
}