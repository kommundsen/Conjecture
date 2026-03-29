namespace Conjecture.Core;

/// <summary>Provides an explicit example to run before generated examples in a property test.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class ExampleAttribute : Attribute
{
    private readonly object?[] arguments;

    /// <summary>Initializes the attribute with the given argument values.</summary>
    public ExampleAttribute(params object?[] arguments)
    {
        this.arguments = arguments;
    }

    /// <summary>The argument values to pass to the test method.</summary>
    public object?[] Arguments => (object?[])arguments.Clone();
}
