// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core;

namespace Conjecture.Core.Tests;

public class SampleAttributeTests
{
    private enum Color { Red, Green, Blue }

    [Fact]
    public void SampleAttribute_IsSealed()
    {
        Assert.True(typeof(SampleAttribute).IsSealed);
    }

    [Fact]
    public void SampleAttribute_IsAttribute()
    {
        Assert.True(typeof(SampleAttribute).IsSubclassOf(typeof(Attribute)));
    }

    [Fact]
    public void SampleAttribute_AllowsMultipleOnMethod()
    {
        AttributeUsageAttribute usage = typeof(SampleAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), inherit: false)
            .Cast<AttributeUsageAttribute>()
            .Single();

        Assert.True(usage.AllowMultiple);
        Assert.True(usage.ValidOn.HasFlag(AttributeTargets.Method));
    }

    [Fact]
    public void Constructor_TwoInts_ArgumentsStored()
    {
        SampleAttribute attr = new(1, 2);

        Assert.Equal(new object?[] { 1, 2 }, attr.Arguments);
    }

    [Fact]
    public void Constructor_SingleString_ArgumentStored()
    {
        SampleAttribute attr = new("hello");

        Assert.Equal(new object?[] { "hello" }, attr.Arguments);
    }

    [Fact]
    public void Constructor_Bool_ArgumentStored()
    {
        SampleAttribute attr = new(true);

        Assert.Equal(new object?[] { true }, attr.Arguments);
    }

    [Fact]
    public void Constructor_Null_ArgumentStored()
    {
        SampleAttribute attr = new((object?)null);

        Assert.Equal(new object?[] { null }, attr.Arguments);
    }

    [Fact]
    public void Constructor_Enum_ArgumentStored()
    {
        SampleAttribute attr = new(Color.Green);

        Assert.Equal(new object?[] { Color.Green }, attr.Arguments);
    }

    [Fact]
    public void Constructor_Type_ArgumentStored()
    {
        SampleAttribute attr = new(typeof(int));

        Assert.Equal(new object?[] { typeof(int) }, attr.Arguments);
    }

    [Fact]
    public void MultipleAttributes_OnMethod_AllRecorded()
    {
        System.Reflection.MethodInfo method = typeof(SampleAttributeTests)
            .GetMethod(nameof(AnnotatedMethod), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;

        SampleAttribute[] attrs = method
            .GetCustomAttributes(typeof(SampleAttribute), inherit: false)
            .Cast<SampleAttribute>()
            .ToArray();

        Assert.Equal(3, attrs.Length);
    }

    [Fact]
    public void MultipleAttributes_OnMethod_ArgumentsMatchDeclarationOrder()
    {
        System.Reflection.MethodInfo method = typeof(SampleAttributeTests)
            .GetMethod(nameof(AnnotatedMethod), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;

        SampleAttribute[] attrs = method
            .GetCustomAttributes(typeof(SampleAttribute), inherit: false)
            .Cast<SampleAttribute>()
            .OrderBy(a => (int)a.Arguments[0]!)
            .ToArray();

        Assert.Equal(new object?[] { 1, 2 }, attrs[0].Arguments);
        Assert.Equal(new object?[] { 3, 4 }, attrs[1].Arguments);
        Assert.Equal(new object?[] { 5, 6 }, attrs[2].Arguments);
    }

    [Fact]
    public void Constructor_NoArgs_ArgumentsIsEmpty()
    {
        SampleAttribute attr = new();

        Assert.Empty(attr.Arguments);
    }

    [Fact]
    public void Arguments_ReturnsSameInstance()
    {
        SampleAttribute attr = new(1, 2);

        Assert.Same(attr.Arguments, attr.Arguments);
    }

    [Sample(1, 2)]
    [Sample(3, 4)]
    [Sample(5, 6)]
    private static void AnnotatedMethod() { }
}