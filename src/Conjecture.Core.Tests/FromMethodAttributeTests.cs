// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Reflection;

using Conjecture.Core;

namespace Conjecture.Core.Tests;

public class FromMethodAttributeTests
{
    [Fact]
    public void FromMethodAttribute_IsSealed()
    {
        Assert.True(typeof(FromMethodAttribute).IsSealed);
    }

    [Fact]
    public void FromMethodAttribute_IsAttribute()
    {
        Assert.True(typeof(FromMethodAttribute).IsSubclassOf(typeof(Attribute)));
    }

    [Fact]
    public void FromMethodAttribute_TargetsParameterOnly()
    {
        AttributeUsageAttribute usage = typeof(FromMethodAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), inherit: false)
            .Cast<AttributeUsageAttribute>()
            .Single();

        Assert.True(usage.ValidOn.HasFlag(AttributeTargets.Parameter));
        Assert.False(usage.ValidOn.HasFlag(AttributeTargets.Method));
        Assert.False(usage.ValidOn.HasFlag(AttributeTargets.Class));
    }

    [Fact]
    public void FromMethodAttribute_DoesNotAllowMultiple()
    {
        AttributeUsageAttribute usage = typeof(FromMethodAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), inherit: false)
            .Cast<AttributeUsageAttribute>()
            .Single();

        Assert.False(usage.AllowMultiple);
    }

    [Fact]
    public void FromMethodAttribute_StoresMethodName()
    {
        FromMethodAttribute attr = new("MyFactory");

        Assert.Equal("MyFactory", attr.MethodName);
    }
}