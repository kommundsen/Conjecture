// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Collections.Generic;
using System.Linq;

using Conjecture.Core;

namespace Conjecture.LinqPad.Tests;

public class StrategyCustomMemberProviderTests
{
    // --- GetNames() for numeric (IConvertible) strategy ---

    [Fact]
    public void GetNames_NumericStrategy_ReturnsPreviewSampleTableHistogram()
    {
        Strategy<int> strategy = Strategy.Integers<int>(1, 100);
        StrategyCustomMemberProvider<int> provider = new(strategy);

        IEnumerable<string> names = provider.GetNames();

        Assert.Equal(new[] { "Preview", "Sample Table", "Histogram" }, names);
    }

    // --- GetNames() for non-IConvertible strategy ---

    [Fact]
    public void GetNames_NonConvertibleStrategy_OmitsHistogram()
    {
        Strategy<object> strategy = Strategy.Just(new object());
        StrategyCustomMemberProvider<object> provider = new(strategy);

        IEnumerable<string> names = provider.GetNames();

        Assert.Equal(new[] { "Preview", "Sample Table" }, names);
    }

    // --- GetTypes() ---

    [Fact]
    public void GetTypes_NumericStrategy_AllTypesAreObject()
    {
        Strategy<int> strategy = Strategy.Integers<int>(1, 100);
        StrategyCustomMemberProvider<int> provider = new(strategy);

        IEnumerable<Type> types = provider.GetTypes();

        Assert.All(types, static t => Assert.Equal(typeof(object), t));
    }

    [Fact]
    public void GetTypes_NumericStrategy_CountMatchesNameCount()
    {
        Strategy<int> strategy = Strategy.Integers<int>(1, 100);
        StrategyCustomMemberProvider<int> provider = new(strategy);

        List<string> names = provider.GetNames().ToList();
        List<Type> types = provider.GetTypes().ToList();

        Assert.Equal(names.Count, types.Count);
    }

    [Fact]
    public void GetTypes_NonConvertibleStrategy_CountMatchesNameCount()
    {
        Strategy<object> strategy = Strategy.Just(new object());
        StrategyCustomMemberProvider<object> provider = new(strategy);

        List<string> names = provider.GetNames().ToList();
        List<Type> types = provider.GetTypes().ToList();

        Assert.Equal(names.Count, types.Count);
    }

    // --- GetValues() non-null ---

    [Fact]
    public void GetValues_NumericStrategy_AllValuesNonNull()
    {
        Strategy<int> strategy = Strategy.Integers<int>(1, 100);
        StrategyCustomMemberProvider<int> provider = new(strategy);

        IEnumerable<object?> values = provider.GetValues();

        Assert.All(values, v => Assert.NotNull(v));
    }

    [Fact]
    public void GetValues_NonConvertibleStrategy_AllValuesNonNull()
    {
        Strategy<object> strategy = Strategy.Just(new object());
        StrategyCustomMemberProvider<object> provider = new(strategy);

        IEnumerable<object?> values = provider.GetValues();

        Assert.All(values, v => Assert.NotNull(v));
    }

    // --- GetValues() count matches names ---

    [Fact]
    public void GetValues_NumericStrategy_CountMatchesNameCount()
    {
        Strategy<int> strategy = Strategy.Integers<int>(1, 100);
        StrategyCustomMemberProvider<int> provider = new(strategy);

        List<string> names = provider.GetNames().ToList();
        List<object?> values = provider.GetValues().ToList();

        Assert.Equal(names.Count, values.Count);
    }

    [Fact]
    public void GetValues_NonConvertibleStrategy_CountMatchesNameCount()
    {
        Strategy<object> strategy = Strategy.Just(new object());
        StrategyCustomMemberProvider<object> provider = new(strategy);

        List<string> names = provider.GetNames().ToList();
        List<object?> values = provider.GetValues().ToList();

        Assert.Equal(names.Count, values.Count);
    }

    // --- GetValues() wraps non-empty rendered HTML ---

    [Fact]
    public void GetValues_NumericStrategy_EachValueWrapsNonEmptyString()
    {
        Strategy<int> strategy = Strategy.Integers<int>(1, 100);
        StrategyCustomMemberProvider<int> provider = new(strategy);

        IEnumerable<object?> values = provider.GetValues();

        Assert.All(values, v =>
        {
            string? text = v!.ToString();
            Assert.False(string.IsNullOrEmpty(text));
        });
    }

    [Fact]
    public void GetValues_NonConvertibleStrategy_EachValueWrapsNonEmptyString()
    {
        Strategy<object> strategy = Strategy.Just(new object());
        StrategyCustomMemberProvider<object> provider = new(strategy);

        IEnumerable<object?> values = provider.GetValues();

        Assert.All(values, v =>
        {
            string? text = v!.ToString();
            Assert.False(string.IsNullOrEmpty(text));
        });
    }

    // --- Implements ICustomMemberProvider ---

    [Fact]
    public void StrategyCustomMemberProvider_ImplementsICustomMemberProvider()
    {
        Strategy<int> strategy = Strategy.Integers<int>(1, 100);
        StrategyCustomMemberProvider<int> provider = new(strategy);

        bool implementsInterface = provider
            .GetType()
            .GetInterfaces()
            .Any(static i => i.FullName == "LINQPad.ICustomMemberProvider");

        Assert.True(implementsInterface);
    }
}