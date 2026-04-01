// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core.Internal;

namespace Conjecture.Tests.Internal;

public class CounterexampleFormatterTests
{
    [Fact]
    public void Format_SingleIntParam_ReturnsNameEqualsValue()
    {
        var parameters = new[] { ("x", (object)6) };

        var result = CounterexampleFormatter.Format(parameters, seed: 0UL);

        Assert.Contains("x = 6", result);
    }

    [Fact]
    public void Format_MultipleParams_EachOnOwnLine()
    {
        var parameters = new[] { ("x", (object)6), ("y", (object)(-3)) };

        var result = CounterexampleFormatter.Format(parameters, seed: 0UL);

        Assert.Contains("x = 6", result);
        Assert.Contains("y = -3", result);
        // Ensure they are on separate lines
        var lines = result.Split('\n');
        Assert.True(lines.Length >= 2, "Expected multiple lines");
    }

    [Fact]
    public void Format_BoolParam_FormatsAsCSharpBoolString()
    {
        var parameters = new[] { ("flag", (object)true) };

        var result = CounterexampleFormatter.Format(parameters, seed: 0UL);

        Assert.Contains("flag = True", result);
    }

    [Fact]
    public void Format_IncludesSeedReproductionLine()
    {
        var parameters = new[] { ("x", (object)1) };

        var result = CounterexampleFormatter.Format(parameters, seed: 0xDEADBEEFUL);

        Assert.Contains("Reproduce with: [Property(Seed = 0xDEADBEEF)]", result);
    }

    [Fact]
    public void Format_UnknownType_FallsBackToToString()
    {
        var custom = new CustomType("hello");
        var parameters = new[] { ("val", (object)custom) };

        var result = CounterexampleFormatter.Format(parameters, seed: 0UL);

        Assert.Contains("val = hello", result);
    }

    private sealed class CustomType(string value)
    {
        public override string ToString() => value;
    }
}