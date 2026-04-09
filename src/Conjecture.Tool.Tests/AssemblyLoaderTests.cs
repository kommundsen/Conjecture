// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Reflection;

using Conjecture.Core;
using Conjecture.Tool;

namespace Conjecture.Tool.Tests;

// Providers defined in the test assembly so AssemblyLoader can discover them.
public sealed class IntStrategyProvider : IStrategyProvider<int>
{
    public Strategy<int> Create() => Generate.Integers<int>(0, 100);
}

public sealed class StringStrategyProvider : IStrategyProvider<string>
{
    public Strategy<string> Create() => Generate.Strings();
}

public class AssemblyLoaderTests
{
    private static string TestAssemblyPath => typeof(AssemblyLoaderTests).Assembly.Location;

    // ── Discovery ────────────────────────────────────────────────────────────

    [Fact]
    public void FindProviders_LoadsAssembly_ReturnsImplementations()
    {
        IReadOnlyList<Type> providers = AssemblyLoader.FindProviders(TestAssemblyPath);

        Assert.NotEmpty(providers);
    }

    [Fact]
    public void FindProviders_ReturnsType_ImplementingIStrategyProviderOfT()
    {
        IReadOnlyList<Type> providers = AssemblyLoader.FindProviders(TestAssemblyPath);

        Assert.Contains(typeof(IntStrategyProvider), providers);
    }

    [Fact]
    public void FindProviders_ReturnsAllProviders_FromAssembly()
    {
        IReadOnlyList<Type> providers = AssemblyLoader.FindProviders(TestAssemblyPath);

        Assert.Contains(typeof(IntStrategyProvider), providers);
        Assert.Contains(typeof(StringStrategyProvider), providers);
    }

    [Fact]
    public void FindProviders_NonExistentAssembly_ThrowsFileNotFoundException()
    {
        Assert.Throws<FileNotFoundException>(() =>
            AssemblyLoader.FindProviders("/nonexistent/path/to/assembly.dll"));
    }

    // ── Type resolution by full name ─────────────────────────────────────────

    [Fact]
    public void ResolveByTargetType_FullName_ReturnsMatchingProvider()
    {
        Assembly assembly = Assembly.LoadFrom(TestAssemblyPath);

        Type? provider = AssemblyLoader.ResolveByTargetType(assembly, "System.Int32");

        Assert.Equal(typeof(IntStrategyProvider), provider);
    }

    [Fact]
    public void ResolveByTargetType_SimpleName_ReturnsMatchingProvider()
    {
        Assembly assembly = Assembly.LoadFrom(TestAssemblyPath);

        Type? provider = AssemblyLoader.ResolveByTargetType(assembly, "Int32");

        Assert.Equal(typeof(IntStrategyProvider), provider);
    }

    [Fact]
    public void ResolveByTargetType_String_FullName_ReturnsStringProvider()
    {
        Assembly assembly = Assembly.LoadFrom(TestAssemblyPath);

        Type? provider = AssemblyLoader.ResolveByTargetType(assembly, "System.String");

        Assert.Equal(typeof(StringStrategyProvider), provider);
    }

    [Fact]
    public void ResolveByTargetType_UnknownType_ReturnsNull()
    {
        Assembly assembly = Assembly.LoadFrom(TestAssemblyPath);

        Type? provider = AssemblyLoader.ResolveByTargetType(assembly, "SomeUnknownType");

        Assert.Null(provider);
    }
}