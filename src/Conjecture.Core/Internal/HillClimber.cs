// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

// Derived from the Python Hypothesis library.
// Original copyright: Copyright (c) 2013-present, David R. MacIver and contributors.

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Conjecture.Core.Internal;

internal static class HillClimber
{
    internal static async Task<(IReadOnlyList<IRNode> Nodes, double Score)> Climb(
        IReadOnlyList<IRNode> nodes,
        double currentScore,
        string label,
        Func<IReadOnlyList<IRNode>, Task<(Status, IReadOnlyDictionary<string, double>)>> evaluate,
        int budget,
        IRandom? rng = null,
        ILogger? logger = null)
    {
        ILogger log = logger ?? NullLogger.Instance;
        var bestNodes = nodes.ToList();
        var bestScore = currentScore;
        var remaining = budget;

        while (remaining > 0)
        {
            var greedyImproved = false;

            for (var i = 0; i < bestNodes.Count && remaining > 0; i++)
            {
                var node = bestNodes[i];
                if (!node.IsIntegerLike)
                {
                    continue;
                }

                foreach (var candidateValue in CandidateValues(node))
                {
                    if (remaining <= 0)
                    {
                        break;
                    }

                    remaining--;
                    var candidate = Mutate(bestNodes, i, candidateValue);
                    var (status, obs) = await evaluate(candidate);

                    if (status == Status.Valid
                        && obs.TryGetValue(label, out var score)
                        && score > bestScore)
                    {
                        bestNodes = candidate;
                        bestScore = score;
                        greedyImproved = true;
                        Log.TargetingStepImproved(log, label, score);
                        break;
                    }
                }
            }

            if (greedyImproved)
            {
                continue;
            }

            // Greedy stalled — try random perturbation if rng provided.
            if (rng is null)
            {
                break;
            }

            var perturbImproved = false;
            while (remaining > 0 && !perturbImproved)
            {
                remaining--;
                var perturbed = Perturb(bestNodes, rng);
                var (status, obs) = await evaluate(perturbed);

                if (status == Status.Valid
                    && obs.TryGetValue(label, out var score)
                    && score > bestScore)
                {
                    bestNodes = perturbed;
                    bestScore = score;
                    perturbImproved = true;
                    Log.TargetingStepImproved(log, label, score);
                }
            }

            if (!perturbImproved)
            {
                break;
            }
        }

        return (bestNodes, bestScore);
    }

    private static List<IRNode> Perturb(List<IRNode> nodes, IRandom rng)
    {
        var integerLikeIndices = new List<int>();
        for (var i = 0; i < nodes.Count; i++)
        {
            if (nodes[i].IsIntegerLike)
            {
                integerLikeIndices.Add(i);
            }
        }

        if (integerLikeIndices.Count == 0)
        {
            return nodes.ToList();
        }

        var count = (int)(rng.NextUInt64() % (ulong)Math.Min(3, integerLikeIndices.Count)) + 1;
        var result = nodes.ToList();

        for (var p = 0; p < count; p++)
        {
            var idx = integerLikeIndices[(int)(rng.NextUInt64() % (ulong)integerLikeIndices.Count)];
            var node = result[idx];
            result[idx] = node.WithValue(RandomInRange(rng, node.Min, node.Max));
        }

        return result;
    }

    private static ulong RandomInRange(IRandom rng, ulong min, ulong max)
    {
        var range = max - min;
        return range == ulong.MaxValue ? rng.NextUInt64() : min + rng.NextUInt64() % (range + 1);
    }

    private static IEnumerable<ulong> CandidateValues(IRNode node)
    {
        if (node.Value < node.Max)
        {
            yield return node.Value + 1;
        }

        if (node.Value > node.Min)
        {
            yield return node.Value - 1;
        }

        if (node.Value < node.Max)
        {
            var mid = node.Value + (node.Max - node.Value) / 2;
            yield return mid == node.Value ? node.Max : mid;
        }

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