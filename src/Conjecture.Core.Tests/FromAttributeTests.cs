// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Reflection;

using Conjecture.Core;

namespace Conjecture.Core.Tests;

public class FromAttributeTests
{
    private sealed class PositiveIntsProvider : IStrategyProvider<int>
    {
        public Strategy<int> Create() => Generate.Integers<int>(1, int.MaxValue);
    }

    [Fact]
    public void FromAttribute_IsSealed()
    {
        Assert.True(typeof(FromAttribute<PositiveIntsProvider>).IsSealed);
    }

    [Fact]
    public void FromAttribute_IsAttribute()
    {
        Assert.True(typeof(FromAttribute<PositiveIntsProvider>).IsSubclassOf(typeof(Attribute)));
    }

    [Fact]
    public void FromAttribute_TargetsParameterOnly()
    {
        AttributeUsageAttribute usage = typeof(FromAttribute<PositiveIntsProvider>)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), inherit: false)
            .Cast<AttributeUsageAttribute>()
            .Single();

        Assert.True(usage.ValidOn.HasFlag(AttributeTargets.Parameter));
        Assert.False(usage.ValidOn.HasFlag(AttributeTargets.Method));
        Assert.False(usage.ValidOn.HasFlag(AttributeTargets.Class));
    }

    [Fact]
    public void FromAttribute_DoesNotAllowMultiple()
    {
        AttributeUsageAttribute usage = typeof(FromAttribute<PositiveIntsProvider>)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), inherit: false)
            .Cast<AttributeUsageAttribute>()
            .Single();

        Assert.False(usage.AllowMultiple);
    }

    [Fact]
    public void FromAttribute_ProviderType_MatchesGenericArg()
    {
        FromAttribute<PositiveIntsProvider> attr = new();

        Assert.Equal(typeof(PositiveIntsProvider), attr.ProviderType);
    }

    [Fact]
    public void IStrategyProvider_NonGenericMarker_ExistsAsInterface()
    {
        Assert.True(typeof(IStrategyProvider).IsInterface);
    }

    [Fact]
    public void IStrategyProviderGeneric_ImplementsMarkerInterface()
    {
        Assert.True(typeof(IStrategyProvider<int>).IsAssignableTo(typeof(IStrategyProvider)));
    }

    [Fact]
    public void ProviderImplementation_ImplementsMarkerInterface()
    {
        Assert.True(typeof(PositiveIntsProvider).IsAssignableTo(typeof(IStrategyProvider)));
    }
}