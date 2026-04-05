// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core;

namespace Conjecture.Core.Tests;

public class ArbitraryAttributeTests
{
    [Arbitrary]
    private partial record AnnotatedRecord(int X, string Y);

    [Arbitrary]
    private partial class AnnotatedClass { }

    [Arbitrary]
    private partial struct AnnotatedStruct { }

    [Fact]
    public void ArbitraryAttribute_IsSealed()
    {
        Assert.True(typeof(ArbitraryAttribute).IsSealed);
    }

    [Fact]
    public void ArbitraryAttribute_IsAttribute()
    {
        Assert.True(typeof(ArbitraryAttribute).IsSubclassOf(typeof(Attribute)));
    }

    [Fact]
    public void ArbitraryAttribute_TargetsClassAndStruct()
    {
        AttributeUsageAttribute usage = typeof(ArbitraryAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), inherit: false)
            .Cast<AttributeUsageAttribute>()
            .Single();

        Assert.True(usage.ValidOn.HasFlag(AttributeTargets.Class));
        Assert.True(usage.ValidOn.HasFlag(AttributeTargets.Struct));
    }

    [Fact]
    public void ArbitraryAttribute_DoesNotAllowMultiple()
    {
        AttributeUsageAttribute usage = typeof(ArbitraryAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), inherit: false)
            .Cast<AttributeUsageAttribute>()
            .Single();

        Assert.False(usage.AllowMultiple);
    }

    [Fact]
    public void ArbitraryAttribute_IsMarker_NoConstructorParameters()
    {
        System.Reflection.ConstructorInfo[] ctors = typeof(ArbitraryAttribute)
            .GetConstructors();

        Assert.Single(ctors);
        Assert.Empty(ctors[0].GetParameters());
    }

    [Fact]
    public void ArbitraryAttribute_CanApplyToRecord()
    {
        ArbitraryAttribute[] attrs = typeof(AnnotatedRecord)
            .GetCustomAttributes(typeof(ArbitraryAttribute), inherit: false)
            .Cast<ArbitraryAttribute>()
            .ToArray();

        Assert.Single(attrs);
    }

    [Fact]
    public void ArbitraryAttribute_CanApplyToClass()
    {
        ArbitraryAttribute[] attrs = typeof(AnnotatedClass)
            .GetCustomAttributes(typeof(ArbitraryAttribute), inherit: false)
            .Cast<ArbitraryAttribute>()
            .ToArray();

        Assert.Single(attrs);
    }

    [Fact]
    public void ArbitraryAttribute_CanApplyToStruct()
    {
        ArbitraryAttribute[] attrs = typeof(AnnotatedStruct)
            .GetCustomAttributes(typeof(ArbitraryAttribute), inherit: false)
            .Cast<ArbitraryAttribute>()
            .ToArray();

        Assert.Single(attrs);
    }
}