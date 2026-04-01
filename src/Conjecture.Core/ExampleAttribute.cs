// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

namespace Conjecture.Core;

/// <summary>Provides an explicit example to run before generated examples in a property test.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class ExampleAttribute(params object?[] arguments) : Attribute
{
    /// <summary>The argument values to pass to the test method.</summary>
    public object?[] Arguments { get; } = arguments;
}