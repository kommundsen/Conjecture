// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core;
using Conjecture.Core.Internal;

namespace Conjecture.Core.Tests;

public class ExtensionPropertyTests
{
    private static ConjectureData MakeData(ulong seed = 42UL) =>
        ConjectureData.ForGeneration(new SplittableRandom(seed));

    [Fact]
    public void Positive_ReturnsOnlyPositiveIntegers()
    {
        Strategy<int> strategy = Generate.Integers<int>().Positive;
        ConjectureData data = MakeData();
        for (int i = 0; i < 100; i++)
        {
            Assert.True(strategy.Generate(data) > 0, "Positive drew a non-positive integer");
        }
    }

    [Fact]
    public void Negative_ReturnsOnlyNegativeIntegers()
    {
        Strategy<int> strategy = Generate.Integers<int>().Negative;
        ConjectureData data = MakeData();
        for (int i = 0; i < 100; i++)
        {
            Assert.True(strategy.Generate(data) < 0, "Negative drew a non-negative integer");
        }
    }

    [Fact]
    public void NonZero_NeverReturnsZero()
    {
        Strategy<int> strategy = Generate.Integers<int>().NonZero;
        ConjectureData data = MakeData();
        for (int i = 0; i < 100; i++)
        {
            Assert.NotEqual(0, strategy.Generate(data));
        }
    }

    [Fact]
    public void String_NonEmpty_NeverReturnsEmptyString()
    {
        Strategy<string> strategy = Generate.Strings().NonEmpty;
        ConjectureData data = MakeData();
        for (int i = 0; i < 100; i++)
        {
            Assert.NotEqual(string.Empty, strategy.Generate(data));
        }
    }

    [Fact]
    public void List_NonEmpty_NeverReturnsEmptyList()
    {
        Strategy<List<int>> strategy = Generate.Lists<int>(Generate.Integers<int>()).NonEmpty;
        ConjectureData data = MakeData();
        for (int i = 0; i < 100; i++)
        {
            Assert.True(strategy.Generate(data).Count > 0, "NonEmpty drew an empty list");
        }
    }

    [Fact]
    public void ExtensionProperties_ComposeWithSelectAndZip()
    {
        Strategy<int> doubled = Generate.Integers<int>().Positive.Select(x => x % (int.MaxValue / 2) + 1);
        ConjectureData data = MakeData();
        for (int i = 0; i < 100; i++)
        {
            Assert.True(doubled.Generate(data) > 0, "Positive.Select(x => x % (int.MaxValue / 2) + 1) returned a non-positive value");
        }
    }
}