// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core.Internal;

namespace Conjecture.Core.Tests.Internal.Shrinker;

public class NumericAwareShrinkPassTests
{
    private static ShrinkState MakeState(
        IReadOnlyList<IRNode> nodes,
        Func<IReadOnlyList<IRNode>, Status> isInteresting)
        => new(nodes, n => new ValueTask<Status>(isInteresting(n)));

    private static IRNode Len(ulong value, ulong max = 64) =>
        IRNode.ForStringLength(value, 0, max);

    private static IRNode Ch(char c) =>
        IRNode.ForStringChar((ulong)c, 32, 127);

    private static IReadOnlyList<IRNode> StringToNodes(string s)
    {
        List<IRNode> nodes = [Len((ulong)s.Length)];
        foreach (char c in s)
        {
            nodes.Add(Ch(c));
        }
        return nodes;
    }

    private static string NodesToString(IReadOnlyList<IRNode> nodes)
    {
        System.Text.StringBuilder sb = new();
        foreach (IRNode n in nodes)
        {
            if (n.Kind == IRNodeKind.StringChar)
            {
                sb.Append((char)n.Value);
            }
        }
        return sb.ToString();
    }

    // ── Trailing numeric segment minimized toward 0 ───────────────────────────

    [Fact]
    public async Task TryReduce_TrailingNumericSegment_ShrinksTowardZero()
    {
        // "item9847" — trailing digits should be minimized toward 0
        // Interesting as long as there are any trailing digits (the non-digit prefix
        // "item" stays put, only the numeric run is reduced).
        IReadOnlyList<IRNode> nodes = StringToNodes("item9847");
        static Status HasTrailingDigits(IReadOnlyList<IRNode> ns)
        {
            string s = "";
            System.Text.StringBuilder sb = new();
            foreach (IRNode n in ns)
            {
                if (n.Kind == IRNodeKind.StringChar)
                {
                    sb.Append((char)n.Value);
                }
            }
            s = sb.ToString();
            // interesting as long as the string contains trailing digits
            return s.Length > 0 && char.IsDigit(s[^1]) ? Status.Interesting : Status.Valid;
        }
        ShrinkState state = MakeState(nodes, HasTrailingDigits);
        NumericAwareShrinkPass pass = new();

        while (await pass.TryReduce(state)) { }

        string result = NodesToString(state.Nodes);
        Assert.EndsWith("0", result);
        // The non-digit prefix should be preserved
        Assert.StartsWith("item", result);
    }

    // ── Leading zeros stripped on shrink (007 → 0, not 000) ─────────────────

    [Fact]
    public async Task TryReduce_LeadingZeroNumericSegment_ShrinksToSingleZero()
    {
        // "log_007_event" — leading zeros should be stripped, 007 → 0
        IReadOnlyList<IRNode> nodes = StringToNodes("log_007_event");
        static Status HasNumericRun(IReadOnlyList<IRNode> ns)
        {
            System.Text.StringBuilder sb = new();
            foreach (IRNode n in ns)
            {
                if (n.Kind == IRNodeKind.StringChar)
                {
                    sb.Append((char)n.Value);
                }
            }
            string s = sb.ToString();
            // interesting as long as the numeric segment is present (any digits between underscores)
            return s.Contains('_') && s.Length >= 5 ? Status.Interesting : Status.Valid;
        }
        ShrinkState state = MakeState(nodes, HasNumericRun);
        NumericAwareShrinkPass pass = new();

        while (await pass.TryReduce(state)) { }

        string result = NodesToString(state.Nodes);
        // 007 should shrink to 0, not 000 — no leading zeros in shrunken output
        Assert.DoesNotContain("007", result);
        Assert.DoesNotContain("00", result);
    }

    [Fact]
    public async Task TryReduce_NumericSegmentWithLeadingZeros_ProducesMinimalZero()
    {
        // Predicate specifically accepts only when numeric run is "0" (no leading zeros).
        IReadOnlyList<IRNode> nodes = StringToNodes("log_007_event");
        static Status AcceptsOnlySingleZeroRun(IReadOnlyList<IRNode> ns)
        {
            System.Text.StringBuilder sb = new();
            foreach (IRNode n in ns)
            {
                if (n.Kind == IRNodeKind.StringChar)
                {
                    sb.Append((char)n.Value);
                }
            }
            string s = sb.ToString();
            return s.Contains("_0_") ? Status.Interesting : Status.Valid;
        }
        ShrinkState state = MakeState(nodes, AcceptsOnlySingleZeroRun);
        NumericAwareShrinkPass pass = new();

        bool progress = await pass.TryReduce(state);

        Assert.True(progress);
        string result = NodesToString(state.Nodes);
        Assert.Contains("_0_", result);
    }

    // ── No numeric segments — pass makes no change ───────────────────────────

    [Fact]
    public async Task TryReduce_NoDigitsInString_ReturnsFalse()
    {
        IReadOnlyList<IRNode> nodes = StringToNodes("no_numbers_here");
        static Status AlwaysInteresting(IReadOnlyList<IRNode> _) => Status.Interesting;
        ShrinkState state = MakeState(nodes, AlwaysInteresting);
        NumericAwareShrinkPass pass = new();

        bool progress = await pass.TryReduce(state);

        Assert.False(progress);
    }

    [Fact]
    public async Task TryReduce_NoDigitsInString_LeavesNodesUnchanged()
    {
        IReadOnlyList<IRNode> nodes = StringToNodes("no_numbers_here");
        static Status AlwaysInteresting(IReadOnlyList<IRNode> _) => Status.Interesting;
        ShrinkState state = MakeState(nodes, AlwaysInteresting);
        NumericAwareShrinkPass pass = new();

        await pass.TryReduce(state);

        Assert.Equal("no_numbers_here", NodesToString(state.Nodes));
    }

    // ── Multiple numeric segments each shrink independently ──────────────────

    [Fact]
    public async Task TryReduce_MultipleNumericSegments_EachSegmentReduced()
    {
        // "v2_patch9" — both "2" and "9" should be reducible
        // Predicate: interesting as long as the string still contains digits
        IReadOnlyList<IRNode> nodes = StringToNodes("v2_patch9");
        static Status ContainsAnyDigit(IReadOnlyList<IRNode> ns)
        {
            foreach (IRNode n in ns)
            {
                if (n.Kind == IRNodeKind.StringChar && char.IsDigit((char)n.Value))
                {
                    return Status.Interesting;
                }
            }
            return Status.Valid;
        }
        ShrinkState state = MakeState(nodes, ContainsAnyDigit);
        NumericAwareShrinkPass pass = new();

        while (await pass.TryReduce(state)) { }

        string result = NodesToString(state.Nodes);
        // Both numeric segments must have been minimized; "2" and "9" should become "0"
        Assert.DoesNotContain("2", result);
        Assert.DoesNotContain("9", result);
    }

    [Fact]
    public async Task TryReduce_MultipleNumericSegments_AllReducedToZero()
    {
        // "v2_patch9" fully shrunken: "v0_patch0"
        IReadOnlyList<IRNode> nodes = StringToNodes("v2_patch9");
        static Status AcceptsWhenBothZero(IReadOnlyList<IRNode> ns)
        {
            System.Text.StringBuilder sb = new();
            foreach (IRNode n in ns)
            {
                if (n.Kind == IRNodeKind.StringChar)
                {
                    sb.Append((char)n.Value);
                }
            }
            string s = sb.ToString();
            // accept as long as there are digits (so shrink can proceed)
            return s.Contains("v") && s.Contains("_patch") ? Status.Interesting : Status.Valid;
        }
        ShrinkState state = MakeState(nodes, AcceptsWhenBothZero);
        NumericAwareShrinkPass pass = new();

        while (await pass.TryReduce(state)) { }

        string result = NodesToString(state.Nodes);
        Assert.Equal("v0_patch0", result);
    }

    // ── Single-digit segment already at "0" — no further reduction ───────────

    [Fact]
    public async Task TryReduce_SingleDigitAlreadyZero_ReturnsFalse()
    {
        // "item0" — the single digit is already 0, nothing to reduce
        IReadOnlyList<IRNode> nodes = StringToNodes("item0");
        static Status AlwaysInteresting(IReadOnlyList<IRNode> _) => Status.Interesting;
        ShrinkState state = MakeState(nodes, AlwaysInteresting);
        NumericAwareShrinkPass pass = new();

        bool progress = await pass.TryReduce(state);

        Assert.False(progress);
    }

    [Fact]
    public async Task TryReduce_SingleDigitAlreadyZero_LeavesNodesUnchanged()
    {
        IReadOnlyList<IRNode> nodes = StringToNodes("item0");
        static Status AlwaysInteresting(IReadOnlyList<IRNode> _) => Status.Interesting;
        ShrinkState state = MakeState(nodes, AlwaysInteresting);
        NumericAwareShrinkPass pass = new();

        await pass.TryReduce(state);

        Assert.Equal("item0", NodesToString(state.Nodes));
    }

    // ── Pass registered in shrinker pass tiers ───────────────────────────────

    [Fact]
    public void PassRegistration_TwelvePassTypes_ArePresent()
    {
        System.Reflection.FieldInfo? field = typeof(Core.Internal.Shrinker)
            .GetField("PassTiers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(field);
        IShrinkPass[][] tiers = (IShrinkPass[][])field!.GetValue(null)!;
        List<IShrinkPass> all = tiers.SelectMany(t => t).ToList();

        Assert.Equal(12, all.Count);
        Assert.Contains(all, p => p is NumericAwareShrinkPass);
    }
}