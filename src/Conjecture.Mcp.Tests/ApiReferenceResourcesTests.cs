// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Reflection;
using System.Runtime.CompilerServices;

using Conjecture.Mcp.Resources;

using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Conjecture.Mcp.Tests;

public class ApiReferenceResourcesTests
{
    private static RequestContext<T> MakeUninitializedContext<T>()
        where T : class
    {
        Type contextType = typeof(RequestContext<T>);
        object ctx = RuntimeHelpers.GetUninitializedObject(contextType);
        return (RequestContext<T>)ctx;
    }

    private static RequestContext<ReadResourceRequestParams> MakeReadContext(string uri)
    {
        Type contextType = typeof(RequestContext<ReadResourceRequestParams>);
        object ctx = RuntimeHelpers.GetUninitializedObject(contextType);
        FieldInfo field = contextType.GetField(
            "<Params>k__BackingField",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        field.SetValue(ctx, new ReadResourceRequestParams { Uri = uri });
        return (RequestContext<ReadResourceRequestParams>)ctx;
    }

    [Fact]
    public async Task TestingPlatform_AppearsInListResources()
    {
        ListResourcesResult result = await ApiReferenceResources.HandleListResources(
            MakeUninitializedContext<ListResourcesRequestParams>(), default);

        Assert.Contains(result.Resources, r => r.Uri == "conjecture://api/testing-platform");
    }

    [Fact]
    public async Task TestingPlatform_ReadReturnsNonEmptyMarkdown()
    {
        RequestContext<ReadResourceRequestParams> context =
            MakeReadContext("conjecture://api/testing-platform");

        ReadResourceResult result = await ApiReferenceResources.HandleReadResource(
            context, default);

        TextResourceContents contents = Assert.IsType<TextResourceContents>(result.Contents[0]);
        Assert.DoesNotContain("not found", contents.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("Conjecture.TestingPlatform")]
    [InlineData("--conjecture-seed")]
    [InlineData("--conjecture-max-examples")]
    public async Task TestingPlatform_ContentMentionsKeyTerms(string expectedTerm)
    {
        RequestContext<ReadResourceRequestParams> context =
            MakeReadContext("conjecture://api/testing-platform");

        ReadResourceResult result = await ApiReferenceResources.HandleReadResource(
            context, default);

        TextResourceContents contents = Assert.IsType<TextResourceContents>(result.Contents[0]);
        Assert.Contains(expectedTerm, contents.Text);
    }
}