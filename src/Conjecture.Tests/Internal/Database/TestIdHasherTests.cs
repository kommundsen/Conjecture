// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core.Internal;

namespace Conjecture.Tests.Internal.Database;

public sealed class TestIdHasherTests
{
    [Fact]
    public void Hash_SameFullyQualifiedName_ReturnsSameHash()
    {
        string hash1 = TestIdHasher.Hash("My.Namespace.MyClass.MyMethod()");
        string hash2 = TestIdHasher.Hash("My.Namespace.MyClass.MyMethod()");

        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void Hash_DifferentNames_ReturnsDifferentHashes()
    {
        string hash1 = TestIdHasher.Hash("My.Namespace.MyClass.MethodA()");
        string hash2 = TestIdHasher.Hash("My.Namespace.MyClass.MethodB()");

        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void Hash_IsDeterministicAcrossInvocations()
    {
        string name = "My.Namespace.MyClass.MyMethod(System.Int32,System.String)";

        string first = TestIdHasher.Hash(name);
        string second = TestIdHasher.Hash(name);

        Assert.Equal(first, second);
    }

    [Fact]
    public void Hash_WithParameterTypes_DifferentFromNoParameters()
    {
        string withoutParams = TestIdHasher.Hash("My.Namespace.MyClass.MyMethod()");
        string withParams = TestIdHasher.Hash("My.Namespace.MyClass.MyMethod(System.Int32,System.Boolean)");

        Assert.NotEqual(withoutParams, withParams);
    }

    [Fact]
    public void Hash_DifferentParameterTypes_ReturnsDifferentHashes()
    {
        string intParam = TestIdHasher.Hash("My.Namespace.MyClass.MyMethod(System.Int32)");
        string stringParam = TestIdHasher.Hash("My.Namespace.MyClass.MyMethod(System.String)");

        Assert.NotEqual(intParam, stringParam);
    }

    [Fact]
    public void Hash_ReturnsNonEmptyString()
    {
        string hash = TestIdHasher.Hash("My.Namespace.MyClass.MyMethod()");

        Assert.False(string.IsNullOrEmpty(hash));
    }
}