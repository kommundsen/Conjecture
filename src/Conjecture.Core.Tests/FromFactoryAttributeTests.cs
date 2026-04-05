// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Reflection;
using Conjecture.Core;

namespace Conjecture.Core.Tests;

public class FromFactoryAttributeTests
{
    [Fact]
    public void FromFactoryAttribute_IsSealed()
    {
        Assert.True(typeof(FromFactoryAttribute).IsSealed);
    }

    [Fact]
    public void FromFactoryAttribute_IsAttribute()
    {
        Assert.True(typeof(FromFactoryAttribute).IsSubclassOf(typeof(Attribute)));
    }

    [Fact]
    public void FromFactoryAttribute_TargetsParameterOnly()
    {
        AttributeUsageAttribute usage = typeof(FromFactoryAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), inherit: false)
            .Cast<AttributeUsageAttribute>()
            .Single();

        Assert.True(usage.ValidOn.HasFlag(AttributeTargets.Parameter));
        Assert.False(usage.ValidOn.HasFlag(AttributeTargets.Method));
        Assert.False(usage.ValidOn.HasFlag(AttributeTargets.Class));
    }

    [Fact]
    public void FromFactoryAttribute_DoesNotAllowMultiple()
    {
        AttributeUsageAttribute usage = typeof(FromFactoryAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), inherit: false)
            .Cast<AttributeUsageAttribute>()
            .Single();

        Assert.False(usage.AllowMultiple);
    }

    [Fact]
    public void FromFactoryAttribute_StoresMethodName()
    {
        FromFactoryAttribute attr = new("MyFactory");

        Assert.Equal("MyFactory", attr.MethodName);
    }
}