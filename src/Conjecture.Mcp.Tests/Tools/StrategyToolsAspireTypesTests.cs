// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Mcp.Tools;

namespace Conjecture.Mcp.Tests.Tools;

public class StrategyToolsAspireTypesTests
{
    [Theory]
    [InlineData("DistributedApplication")]
    [InlineData("Interaction")]
    [InlineData("IAspireAppFixture")]
    public void SuggestForType_AspireType_MentionsConjectureAspire(string typeName)
    {
        string result = StrategyTools.SuggestForType(typeName);
        Assert.Contains("Conjecture.Aspire", result);
    }

    [Fact]
    public void SuggestForType_Interaction_SuggestsHttpPost()
    {
        string result = StrategyTools.SuggestForType("Interaction");
        Assert.Contains("Strategy.HttpPost", result);
    }

    [Fact]
    public void SuggestForType_Interaction_SuggestsPublishMessage()
    {
        string result = StrategyTools.SuggestForType("Interaction");
        Assert.Contains("Strategy.PublishMessage", result);
    }

    [Fact]
    public void SuggestForType_DistributedApplication_SuggestsAspireStateMachine()
    {
        string result = StrategyTools.SuggestForType("DistributedApplication");
        Assert.Contains("AspireStateMachine<", result);
    }

    [Fact]
    public void SuggestForType_IAspireAppFixture_SuggestsAspireStateMachine()
    {
        string result = StrategyTools.SuggestForType("IAspireAppFixture");
        Assert.Contains("AspireStateMachine<", result);
    }

    [Fact]
    public void SuggestForType_IAspireAppFixture_MentionsResetAsync()
    {
        string result = StrategyTools.SuggestForType("IAspireAppFixture");
        Assert.Contains("ResetAsync", result);
    }
}