// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;

using Conjecture.Core;
using Conjecture.Core.Internal;

namespace Conjecture.Core.Tests;

/// <summary>
/// Tests for <see cref="ConjectureStrategyRegistrar"/>, the static dispatch hook that allows
/// generated code to register an AOT-safe resolver for <c>[Property]</c> parameter types.
/// </summary>
[Collection("StrategyRegistrar")]
public sealed class ConjectureStrategyRegistrarTests
{
    [Fact]
    public void TryResolve_WithNoRegistration_ReturnsNull()
    {
        ConjectureStrategyRegistrar.Register(static (_, _) => null);

        object? result = ConjectureStrategyRegistrar.TryResolve(typeof(string), null!);

        Assert.Null(result);
    }

    [Fact]
    public void Register_ThenTryResolve_WithMatchingType_ReturnsResolvedValue()
    {
        object expected = new();
        ConjectureStrategyRegistrar.Register((type, _) => type == typeof(int) ? expected : null);

        object? result = ConjectureStrategyRegistrar.TryResolve(typeof(int), null!);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void Register_ThenTryResolve_WithNonMatchingType_ReturnsNull()
    {
        ConjectureStrategyRegistrar.Register(static (type, _) => type == typeof(int) ? new object() : null);

        object? result = ConjectureStrategyRegistrar.TryResolve(typeof(string), null!);

        Assert.Null(result);
    }
}

[CollectionDefinition("StrategyRegistrar")]
public sealed class StrategyRegistrarCollection : ICollectionFixture<StrategyRegistrarFixture>;

/// <summary>
/// Resets the global <see cref="ConjectureStrategyRegistrar"/> state before and after
/// each test in the <c>StrategyRegistrar</c> collection.
/// </summary>
public sealed class StrategyRegistrarFixture : IDisposable
{
    public StrategyRegistrarFixture()
    {
        ConjectureStrategyRegistrar.Register(static (_, _) => null);
    }

    public void Dispose()
    {
        ConjectureStrategyRegistrar.Register(static (_, _) => null);
    }
}
