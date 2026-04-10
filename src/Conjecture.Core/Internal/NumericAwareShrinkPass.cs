// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

namespace Conjecture.Core.Internal;

internal sealed class NumericAwareShrinkPass : IShrinkPass
{
    public async ValueTask<bool> TryReduce(ShrinkState state)
    {
        int i = 0;
        while (i < state.Nodes.Count)
        {
            if (state.Nodes[i].Kind != IRNodeKind.StringLength)
            {
                i++;
                continue;
            }

            int lenIndex = i;
            ulong strLen = state.Nodes[lenIndex].Value;
            int charStart = lenIndex + 1;
            int charCount = 0;
            while (charStart + charCount < state.Nodes.Count
                   && state.Nodes[charStart + charCount].Kind == IRNodeKind.StringChar
                   && (ulong)charCount < strLen)
            {
                charCount++;
            }

            if (await TryReduceNumericRuns(state, lenIndex, charStart, charCount))
            {
                return true;
            }

            i = charStart + charCount;
        }

        return false;
    }

    private static async ValueTask<bool> TryReduceNumericRuns(
        ShrinkState state,
        int lenIndex,
        int charStart,
        int charCount)
    {
        int runStart = 0;
        while (runStart < charCount)
        {
            if (!char.IsDigit((char)state.Nodes[charStart + runStart].Value))
            {
                runStart++;
                continue;
            }

            int runEnd = runStart;
            while (runEnd < charCount && char.IsDigit((char)state.Nodes[charStart + runEnd].Value))
            {
                runEnd++;
            }

            ulong numericValue = 0;
            for (int k = runStart; k < runEnd; k++)
            {
                numericValue = numericValue * 10 + (state.Nodes[charStart + k].Value - '0');
            }

            if (numericValue > 0 && await TryBinarySearchReduce(state, lenIndex, charStart, charCount, runStart, runEnd, numericValue))
            {
                return true;
            }

            runStart = runEnd;
        }

        return false;
    }

    private static async ValueTask<bool> TryBinarySearchReduce(
        ShrinkState state,
        int lenIndex,
        int charStart,
        int charCount,
        int runStart,
        int runEnd,
        ulong currentValue)
    {
        // Try 0 first — the most aggressive reduction.
        if (await TryCandidateValue(state, lenIndex, charStart, charCount, runStart, runEnd, 0))
        {
            return true;
        }

        // Binary search in [1, currentValue-1] for the smallest accepted value.
        // Accepting mutates state.Nodes (possibly changing array length), so
        // return immediately on success and let the outer fixpoint retry.
        ulong lo = 1;
        ulong hi = currentValue - 1;

        while (lo < hi)
        {
            ulong mid = lo + (hi - lo) / 2;
            if (await TryCandidateValue(state, lenIndex, charStart, charCount, runStart, runEnd, mid))
            {
                return true;
            }

            lo = mid + 1;
        }

        return await TryCandidateValue(state, lenIndex, charStart, charCount, runStart, runEnd, lo);
    }

    private static async ValueTask<bool> TryCandidateValue(
        ShrinkState state,
        int lenIndex,
        int charStart,
        int charCount,
        int runStart,
        int runEnd,
        ulong candidateValue)
    {
        string digits = candidateValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
        int oldRunLen = runEnd - runStart;
        int newRunLen = digits.Length;
        int delta = newRunLen - oldRunLen;

        IRNode lenNode = state.Nodes[lenIndex];
        ulong newStrLen = delta >= 0
            ? lenNode.Value + (ulong)delta
            : lenNode.Value - (ulong)(-delta);
        int newTotalCount = state.Nodes.Count + delta;

        IRNode[] candidate = new IRNode[newTotalCount];

        for (int k = 0; k < lenIndex; k++)
        {
            candidate[k] = state.Nodes[k];
        }

        candidate[lenIndex] = IRNode.ForStringLength(newStrLen, lenNode.Min, lenNode.Max);

        int dst = lenIndex + 1;

        for (int k = 0; k < runStart; k++)
        {
            candidate[dst++] = state.Nodes[charStart + k];
        }

        foreach (char ch in digits)
        {
            IRNode template = state.Nodes[charStart + runStart];
            candidate[dst++] = IRNode.ForStringChar((ulong)ch, template.Min, template.Max);
        }

        for (int k = runEnd; k < charCount; k++)
        {
            candidate[dst++] = state.Nodes[charStart + k];
        }

        int afterChars = charStart + charCount;
        for (int k = afterChars; k < state.Nodes.Count; k++)
        {
            candidate[dst++] = state.Nodes[k];
        }

        return await state.TryUpdate(candidate);
    }
}