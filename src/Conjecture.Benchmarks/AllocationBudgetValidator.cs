// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

namespace Conjecture.Benchmarks;

public static class AllocationBudgetValidator
{
    public static List<(string Method, string Message)> Validate(
        long baseline,
        IReadOnlyDictionary<string, (long Actual, long Budget)> methods)
    {
        List<(string Method, string Message)> failures = [];
        foreach (KeyValuePair<string, (long Actual, long Budget)> entry in methods)
        {
            long limit = baseline + entry.Value.Budget;
            if (entry.Value.Actual > limit)
            {
                failures.Add((entry.Key, $"{entry.Key} exceeded allocation budget: actual={entry.Value.Actual}, limit={limit}"));
            }
        }

        return failures;
    }
}