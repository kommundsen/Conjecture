// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Generic;
using System.Linq;

using Conjecture.Core;

namespace Conjecture.Core.Tests.Strategies;

public class StringStrategyTests
{
    [Fact]
    public void Strings_ProducesString()
    {
        Strategy<string> strategy = Strategy.Strings();
        Assert.IsType<string>(strategy.Sample());
    }

    [Fact]
    public void Strings_DefaultCharset_IsPrintableAscii()
    {
        Strategy<string> strategy = Strategy.Strings();
        Assert.All(strategy.WithSeed(42UL).Sample(200), s =>
        {
            foreach (char c in s)
            {
                Assert.InRange((int)c, 32, 126);
            }
        });
    }

    [Fact]
    public void Strings_DeterministicWithSeed()
    {
        Strategy<string> strategy = Strategy.Strings();
        IReadOnlyList<string> results1 = strategy.WithSeed(99UL).Sample(20);
        IReadOnlyList<string> results2 = strategy.WithSeed(99UL).Sample(20);
        Assert.Equal(results1, results2);
    }

    [Fact]
    public void Strings_RespectsBoundsWhenMinAndMaxLengthSet()
    {
        Strategy<string> strategy = Strategy.Strings(minLength: 5, maxLength: 10);
        Assert.All(strategy.WithSeed(42UL).Sample(100), s => Assert.InRange(s.Length, 5, 10));
    }

    [Fact]
    public void Strings_MinLengthZero_CanProduceEmptyString()
    {
        Strategy<string> strategy = Strategy.Strings(minLength: 0, maxLength: 5);
        IReadOnlyList<string> samples = strategy.WithSeed(0UL).Sample(1000);
        Assert.Contains(samples, s => s.Length == 0);
    }

}
