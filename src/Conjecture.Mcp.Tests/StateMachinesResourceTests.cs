// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Mcp.Resources;

using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Conjecture.Mcp.Tests;

public class StateMachinesResourceTests
{
    [Fact]
    public async Task StateMachines_AppearsInListResources()
    {
        ListResourcesResult result = await ApiReferenceResources.HandleListResources(
            ResourceTestHelpers.MakeUninitializedContext<ListResourcesRequestParams>(), default);

        Assert.Contains(result.Resources, r => r.Uri == "conjecture://api/state-machines");
    }

    [Fact]
    public async Task StateMachines_ReadReturnsNonEmptyMarkdown()
    {
        RequestContext<ReadResourceRequestParams> context =
            ResourceTestHelpers.MakeReadContext("conjecture://api/state-machines");

        ReadResourceResult result = await ApiReferenceResources.HandleReadResource(context, default);

        TextResourceContents contents = Assert.IsType<TextResourceContents>(result.Contents[0]);
        Assert.DoesNotContain("not found", contents.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("IStateMachine")]
    [InlineData("StateMachineRun")]
    [InlineData("Strategy.StateMachine")]
    public async Task StateMachines_ContentMentionsKeyTerms(string expectedTerm)
    {
        RequestContext<ReadResourceRequestParams> context =
            ResourceTestHelpers.MakeReadContext("conjecture://api/state-machines");

        ReadResourceResult result = await ApiReferenceResources.HandleReadResource(context, default);

        TextResourceContents contents = Assert.IsType<TextResourceContents>(result.Contents[0]);
        Assert.Contains(expectedTerm, contents.Text);
    }
}
