using Conjecture.Core;

namespace Conjecture.Tests;

public class ExampleAttributeTests
{
    private enum Color { Red, Green, Blue }

    [Fact]
    public void ExampleAttribute_IsSealed()
    {
        Assert.True(typeof(ExampleAttribute).IsSealed);
    }

    [Fact]
    public void ExampleAttribute_IsAttribute()
    {
        Assert.True(typeof(ExampleAttribute).IsSubclassOf(typeof(Attribute)));
    }

    [Fact]
    public void ExampleAttribute_AllowsMultipleOnMethod()
    {
        AttributeUsageAttribute usage = typeof(ExampleAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), inherit: false)
            .Cast<AttributeUsageAttribute>()
            .Single();

        Assert.True(usage.AllowMultiple);
        Assert.True(usage.ValidOn.HasFlag(AttributeTargets.Method));
    }

    [Fact]
    public void Constructor_TwoInts_ArgumentsStored()
    {
        ExampleAttribute attr = new(1, 2);

        Assert.Equal(new object?[] { 1, 2 }, attr.Arguments);
    }

    [Fact]
    public void Constructor_SingleString_ArgumentStored()
    {
        ExampleAttribute attr = new("hello");

        Assert.Equal(new object?[] { "hello" }, attr.Arguments);
    }

    [Fact]
    public void Constructor_Bool_ArgumentStored()
    {
        ExampleAttribute attr = new(true);

        Assert.Equal(new object?[] { true }, attr.Arguments);
    }

    [Fact]
    public void Constructor_Null_ArgumentStored()
    {
        ExampleAttribute attr = new((object?)null);

        Assert.Equal(new object?[] { null }, attr.Arguments);
    }

    [Fact]
    public void Constructor_Enum_ArgumentStored()
    {
        ExampleAttribute attr = new(Color.Green);

        Assert.Equal(new object?[] { Color.Green }, attr.Arguments);
    }

    [Fact]
    public void Constructor_Type_ArgumentStored()
    {
        ExampleAttribute attr = new(typeof(int));

        Assert.Equal(new object?[] { typeof(int) }, attr.Arguments);
    }

    [Fact]
    public void MultipleAttributes_OnMethod_AllRecorded()
    {
        System.Reflection.MethodInfo method = typeof(ExampleAttributeTests)
            .GetMethod(nameof(AnnotatedMethod), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;

        ExampleAttribute[] attrs = method
            .GetCustomAttributes(typeof(ExampleAttribute), inherit: false)
            .Cast<ExampleAttribute>()
            .ToArray();

        Assert.Equal(3, attrs.Length);
    }

    [Fact]
    public void MultipleAttributes_OnMethod_ArgumentsMatchDeclarationOrder()
    {
        System.Reflection.MethodInfo method = typeof(ExampleAttributeTests)
            .GetMethod(nameof(AnnotatedMethod), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;

        ExampleAttribute[] attrs = method
            .GetCustomAttributes(typeof(ExampleAttribute), inherit: false)
            .Cast<ExampleAttribute>()
            .OrderBy(a => (int)a.Arguments[0]!)
            .ToArray();

        Assert.Equal(new object?[] { 1, 2 }, attrs[0].Arguments);
        Assert.Equal(new object?[] { 3, 4 }, attrs[1].Arguments);
        Assert.Equal(new object?[] { 5, 6 }, attrs[2].Arguments);
    }

    [Fact]
    public void Constructor_NoArgs_ArgumentsIsEmpty()
    {
        ExampleAttribute attr = new();

        Assert.Empty(attr.Arguments);
    }

    [Fact]
    public void Arguments_ReturnsSameInstance()
    {
        ExampleAttribute attr = new(1, 2);

        Assert.Same(attr.Arguments, attr.Arguments);
    }

    [Example(1, 2)]
    [Example(3, 4)]
    [Example(5, 6)]
    private static void AnnotatedMethod() { }
}
