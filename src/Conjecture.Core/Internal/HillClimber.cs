// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

namespace Conjecture.Core.Internal;

internal static class HillClimber
{
    internal static async Task<(IReadOnlyList<IRNode> Nodes, double Score)> Climb(
        IReadOnlyList<IRNode> nodes,
        double currentScore,
        string label,
        Func<IReadOnlyList<IRNode>, Task<(Status, IReadOnlyDictionary<string, double>)>> evaluate,
        int budget)
    {
        var bestNodes = nodes.ToList();
        var bestScore = currentScore;
        var remaining = budget;

        while (remaining > 0)
        {
            var improved = false;

            for (var i = 0; i < bestNodes.Count && remaining > 0; i++)
            {
                var node = bestNodes[i];
                if (!node.IsIntegerLike)
                    continue;

                foreach (var candidateValue in CandidateValues(node))
                {
                    if (remaining <= 0)
                        break;

                    remaining--;
                    var candidate = Mutate(bestNodes, i, candidateValue);
                    var (status, obs) = await evaluate(candidate);

                    if (status == Status.Valid
                        && obs.TryGetValue(label, out var score)
                        && score > bestScore)
                    {
                        bestNodes = candidate;
                        bestScore = score;
                        improved = true;
                        break; // move to next node
                    }
                }
            }

            if (!improved)
                break;
        }

        return (bestNodes, bestScore);
    }

    private static IEnumerable<ulong> CandidateValues(IRNode node)
    {
        // Increment by 1
        if (node.Value < node.Max)
            yield return node.Value + 1;

        // Decrement by 1
        if (node.Value > node.Min)
            yield return node.Value - 1;

        // Binary search toward Max
        if (node.Value < node.Max)
        {
            var mid = node.Value + (node.Max - node.Value) / 2;
            yield return mid == node.Value ? node.Max : mid;
        }

        // Binary search toward Min
        if (node.Value > node.Min)
        {
            var mid = node.Min + (node.Value - node.Min) / 2;
            yield return mid == node.Value ? node.Min : mid;
        }
    }

    private static List<IRNode> Mutate(List<IRNode> nodes, int index, ulong newValue)
    {
        var result = nodes.ToList();
        result[index] = nodes[index].WithValue(newValue);
        return result;
    }
}