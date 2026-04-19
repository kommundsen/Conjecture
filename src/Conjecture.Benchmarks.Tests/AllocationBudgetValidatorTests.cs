// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

namespace Conjecture.Benchmarks.Tests;

public sealed class AllocationBudgetValidatorTests
{
    [Fact]
    public void Validate_ReturnsEmpty_WhenAllMethodsWithinBudget()
    {
        long baseline = 24;
        IReadOnlyDictionary<string, (long Actual, long Budget)> methods = new Dictionary<string, (long Actual, long Budget)>
        {
            ["Select_Single"] = (24, 1),
        };

        List<(string Method, string Message)> failures = AllocationBudgetValidator.Validate(baseline, methods);

        Assert.Empty(failures);
    }

    [Fact]
    public void Validate_ReturnsFailure_WhenMethodExceedsBudget()
    {
        long baseline = 24;
        IReadOnlyDictionary<string, (long Actual, long Budget)> methods = new Dictionary<string, (long Actual, long Budget)>
        {
            ["Select_Single"] = (50, 1),
        };

        List<(string Method, string Message)> failures = AllocationBudgetValidator.Validate(baseline, methods);

        Assert.Single(failures);
        Assert.Contains("Select_Single", failures[0].Method);
        Assert.Contains("budget", failures[0].Message);
    }

    [Fact]
    public void Validate_ReturnsMultipleFailures_WhenMultipleExceed()
    {
        long baseline = 24;
        IReadOnlyDictionary<string, (long Actual, long Budget)> methods = new Dictionary<string, (long Actual, long Budget)>
        {
            ["MethodA"] = (100, 1),
            ["MethodB"] = (200, 1),
        };

        List<(string Method, string Message)> failures = AllocationBudgetValidator.Validate(baseline, methods);

        Assert.Equal(2, failures.Count);
    }

    [Fact]
    public void Validate_ReturnsEmpty_WhenExactlyAtBudget()
    {
        long baseline = 24;
        IReadOnlyDictionary<string, (long Actual, long Budget)> methods = new Dictionary<string, (long Actual, long Budget)>
        {
            ["Select_Single"] = (25, 1),
        };

        List<(string Method, string Message)> failures = AllocationBudgetValidator.Validate(baseline, methods);

        Assert.Empty(failures);
    }

    [Fact]
    public void Validate_ReturnsFailure_WhenOneByteOverBudget()
    {
        long baseline = 24;
        IReadOnlyDictionary<string, (long Actual, long Budget)> methods = new Dictionary<string, (long Actual, long Budget)>
        {
            ["Select_Single"] = (26, 1),
        };

        List<(string Method, string Message)> failures = AllocationBudgetValidator.Validate(baseline, methods);

        Assert.Single(failures);
    }

    [Fact]
    public void Validate_ReturnsEmpty_WhenMethodsDictionaryIsEmpty()
    {
        long baseline = 24;
        IReadOnlyDictionary<string, (long Actual, long Budget)> methods = new Dictionary<string, (long Actual, long Budget)>();

        List<(string Method, string Message)> failures = AllocationBudgetValidator.Validate(baseline, methods);

        Assert.Empty(failures);
    }

    [Fact]
    public void Validate_HandlesNegativeBaseline()
    {
        long baseline = -10;
        IReadOnlyDictionary<string, (long Actual, long Budget)> methods = new Dictionary<string, (long Actual, long Budget)>
        {
            ["Select_Single"] = (-5, 1),
        };

        List<(string Method, string Message)> failures = AllocationBudgetValidator.Validate(baseline, methods);

        Assert.Single(failures);
    }
}
