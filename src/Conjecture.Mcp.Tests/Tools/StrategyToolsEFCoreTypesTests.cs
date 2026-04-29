// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Mcp.Tools;

namespace Conjecture.Mcp.Tests.Tools;

public class StrategyToolsEFCoreTypesTests
{
    [Fact]
    public void StrategyTools_SuggestStrategy_DbContext_ReturnsEFCoreEntityStrategySuggestion()
    {
        string result = StrategyTools.SuggestForType("DbContext");

        Assert.Contains("Strategy.Entity", result);
    }

    [Fact]
    public void StrategyTools_SuggestStrategy_EntityRegisteredInModel_ReturnsEntitySetSuggestion()
    {
        string result = StrategyTools.SuggestForType("EntitySet");

        Assert.Contains("Strategy.EntitySet", result);
    }

    [Fact]
    public void StrategyTools_SuggestStrategy_NonEntityType_DoesNotMentionEFCore()
    {
        string result = StrategyTools.SuggestForType("int");

        Assert.DoesNotContain("Strategy.EFCore", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("EntityStrategyBuilder", result, StringComparison.OrdinalIgnoreCase);
    }
}