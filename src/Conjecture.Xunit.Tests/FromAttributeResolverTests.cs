// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Reflection;

using Conjecture.Core;
using Conjecture.Core.Internal;


namespace Conjecture.Xunit.Tests;

public class FromAttributeResolverTests
{
    private sealed class PositiveIntsProvider : IStrategyProvider<int>
    {
        public Strategy<int> Create() => Strategy.Integers<int>(1, int.MaxValue);
    }

    private sealed class StringProvider : IStrategyProvider<string>
    {
        public Strategy<string> Create() => Strategy.Strings();
    }

    private static ConjectureData MakeData(ulong seed = 42UL) =>
        ConjectureData.ForGeneration(new SplittableRandom(seed));

#pragma warning disable IDE0060
    private static void FromPositiveInts([From<PositiveIntsProvider>] int n) { }
    private static void MixedParams([From<PositiveIntsProvider>] int n, string s) { }
    private static void WrongType([From<StringProvider>] int n) { }
    private static void PlainInt(int n) { }
#pragma warning restore IDE0060

    private static ParameterInfo[] ParamsOf(string methodName) =>
        typeof(FromAttributeResolverTests)
            .GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static)!
            .GetParameters();

    [Fact]
    public void Resolve_FromAttribute_UsesProviderStrategy()
    {
        ParameterInfo[] parameters = ParamsOf(nameof(FromPositiveInts));

        for (int i = 0; i < 50; i++)
        {
            object[] args = SharedParameterStrategyResolver.Resolve(parameters, MakeData((ulong)i));
            int value = Assert.IsType<int>(args[0]);
            Assert.True(value >= 1, $"Expected positive int from provider, got {value}");
        }
    }

    [Fact]
    public void Resolve_FromAttribute_InstantiatesProviderAndDrawsValue()
    {
        ParameterInfo[] parameters = ParamsOf(nameof(FromPositiveInts));
        object[] args = SharedParameterStrategyResolver.Resolve(parameters, MakeData());

        Assert.Single(args);
        Assert.IsType<int>(args[0]);
    }

    [Fact]
    public void Resolve_TypeMismatch_ThrowsInvalidOperationException()
    {
        ParameterInfo[] parameters = ParamsOf(nameof(WrongType));

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => SharedParameterStrategyResolver.Resolve(parameters, MakeData()));

        Assert.Contains("string", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("int", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Resolve_NoFromAttribute_FallsBackToTypeInference()
    {
        ParameterInfo[] parameters = ParamsOf(nameof(PlainInt));
        object[] args = SharedParameterStrategyResolver.Resolve(parameters, MakeData());

        Assert.Single(args);
        Assert.IsType<int>(args[0]);
    }

    [Fact]
    public void Resolve_MixedParams_FromAndInferred_BothResolved()
    {
        ParameterInfo[] parameters = ParamsOf(nameof(MixedParams));
        object[] args = SharedParameterStrategyResolver.Resolve(parameters, MakeData());

        Assert.Equal(2, args.Length);
        int n = Assert.IsType<int>(args[0]);
        Assert.IsType<string>(args[1]);
        Assert.True(n >= 1, $"Expected positive int from [From<>], got {n}");
    }
}