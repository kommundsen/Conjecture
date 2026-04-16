// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

// Derived from the Python Hypothesis library.
// Original copyright: Copyright (c) 2013-present, David R. MacIver and contributors.

namespace Conjecture.Core.Internal;

internal sealed class StringAwarePass : IShrinkPass
{
    public string PassName => "string_aware";

    private const ulong ACodepoint = 'a';
    private const ulong SpaceCodepoint = 32;
    private static readonly ulong[] CharTargets = [ACodepoint, SpaceCodepoint];

    public async ValueTask<bool> TryReduce(ShrinkState state)
    {
        for (int i = 0; i < state.Nodes.Count; i++)
        {
            if (state.Nodes[i].Kind == IRNodeKind.StringLength && await TryReduceLength(state, i))
            {
                return true;
            }
        }
        for (int i = 0; i < state.Nodes.Count; i++)
        {
            if (state.Nodes[i].Kind == IRNodeKind.StringChar && await TrySimplifyChar(state, i))
            {
                return true;
            }
        }
        return false;
    }

    private static async ValueTask<bool> TryReduceLength(ShrinkState state, int lenIndex)
    {
        IRNode lenNode = state.Nodes[lenIndex];
        ulong currentLen = lenNode.Value;
        if (currentLen <= lenNode.Min)
        {
            return false;
        }

        int charStart = lenIndex + 1;
        int charCount = 0;
        while (charStart + charCount < state.Nodes.Count
               && state.Nodes[charStart + charCount].Kind == IRNodeKind.StringChar
               && (ulong)charCount < currentLen)
        {
            charCount++;
        }

        ulong newLen = currentLen;
        while (newLen > lenNode.Min)
        {
            newLen--;
            int toRemove = (int)(currentLen - newLen);
            if (toRemove > charCount)
            {
                break;
            }

            int keepChars = charCount - toRemove;
            int afterChars = charStart + charCount;
            IRNode[] candidate = new IRNode[lenIndex + 1 + keepChars + (state.Nodes.Count - afterChars)];
            for (int k = 0; k < lenIndex; k++)
            {
                candidate[k] = state.Nodes[k];
            }
            candidate[lenIndex] = IRNode.ForStringLength(newLen, lenNode.Min, lenNode.Max);
            for (int k = 0; k < keepChars; k++)
            {
                candidate[lenIndex + 1 + k] = state.Nodes[charStart + k];
            }
            int dstStart = lenIndex + 1 + keepChars;
            for (int k = 0; k < state.Nodes.Count - afterChars; k++)
            {
                candidate[dstStart + k] = state.Nodes[afterChars + k];
            }

            if (await state.TryUpdate(candidate))
            {
                return true;
            }
        }
        return false;
    }

    private static async ValueTask<bool> TrySimplifyChar(ShrinkState state, int charIndex)
    {
        IRNode charNode = state.Nodes[charIndex];
        foreach (ulong target in CharTargets)
        {
            if (target >= charNode.Value || target < charNode.Min || target > charNode.Max)
            {
                continue;
            }
            IRNode[] candidate = [.. state.Nodes];
            candidate[charIndex] = IRNode.ForStringChar(target, charNode.Min, charNode.Max);
            if (await state.TryUpdate(candidate))
            {
                return true;
            }
        }
        return false;
    }
}