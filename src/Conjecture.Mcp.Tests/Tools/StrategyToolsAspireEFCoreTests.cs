// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Mcp.Tools;

namespace Conjecture.Mcp.Tests.Tools;

public class StrategyToolsAspireEFCoreTests
{
    [Fact]
    public void SuggestStrategy_for_AspireDbTarget_recommends_CreateAsync_with_factory()
    {
        string result = StrategyTools.SuggestForType("AspireDbTarget<OrdersContext>");

        Assert.Contains("AspireDbTarget", result, StringComparison.Ordinal);
        Assert.Contains("CreateAsync", result, StringComparison.Ordinal);
        Assert.Contains("contextFactory", result, StringComparison.Ordinal);
    }

    [Fact]
    public void SuggestStrategy_for_AspireDbTargetRegistry_describes_fixture_wiring()
    {
        string result = StrategyTools.SuggestForType("AspireDbTargetRegistry");

        Assert.Contains("AspireDbTargetRegistry", result, StringComparison.Ordinal);
        Assert.Contains("ResetAsync", result, StringComparison.Ordinal);
        Assert.Contains("IAspireAppFixture", result, StringComparison.Ordinal);
    }

    [Fact]
    public void SuggestStrategy_for_AspireEFCoreInvariants_lists_NoPartialWritesOnError_and_Idempotent()
    {
        string result = StrategyTools.SuggestForType("AspireEFCoreInvariants");

        Assert.Contains("AspireEFCoreInvariants", result, StringComparison.Ordinal);
        Assert.Contains("AssertNoPartialWritesOnErrorAsync", result, StringComparison.Ordinal);
        Assert.Contains("AssertIdempotentAsync", result, StringComparison.Ordinal);
    }

    [Fact]
    public void SuggestStrategy_for_DbSnapshotInteraction_recommends_AspireInteractionSequenceBuilder()
    {
        string result = StrategyTools.SuggestForType("DbSnapshotInteraction");

        Assert.Contains("AspireInteractionSequenceBuilder", result, StringComparison.Ordinal);
        Assert.Contains("DbSnapshot", result, StringComparison.Ordinal);
    }

    [Fact]
    public void SuggestStrategy_for_AspireTarget_composite_surfaces_AspireEFCoreInvariants_when_DbTarget_present()
    {
        string result = StrategyTools.SuggestForType("AspireDbTarget<OrdersContext>");

        Assert.Contains("AspireEFCoreInvariants", result, StringComparison.Ordinal);
    }
}