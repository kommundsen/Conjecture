// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Reflection;

using Conjecture.Core;
using Conjecture.Core.Internal;


namespace Conjecture.Xunit.Tests;

public class FromMethodAttributeResolverTests
{
    private static ConjectureData MakeData(ulong seed = 42UL) =>
        ConjectureData.ForGeneration(new SplittableRandom(seed));

    // ─── Factory methods used by helper params below ──────────────────────────

    public static Strategy<int> EvenInts() =>
        Strategy.Integers<int>(0, 25).Select(n => n * 2);

    public Strategy<int> NonStaticFactory() => Strategy.Integers<int>();

    public static string WrongReturnType() => "not a strategy";

    // ─── Helper stubs with [FromMethod] on parameters ────────────────────────

#pragma warning disable IDE0060
    private static void WithEvenInts([FromMethod(nameof(EvenInts))] int n) { }

    private static void WithMissingMethod([FromMethod("NoSuchMethod")] int n) { }

    private static void WithNonStaticFactory([FromMethod(nameof(NonStaticFactory))] int n) { }

    private static void WithWrongReturnType([FromMethod(nameof(WrongReturnType))] int n) { }
#pragma warning restore IDE0060

    private static ParameterInfo[] ParamsOf(string methodName) =>
        typeof(FromMethodAttributeResolverTests)
            .GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static)!
            .GetParameters();

    // ─── Happy path ───────────────────────────────────────────────────────────

    [Fact]
    public void Resolve_StaticFactoryMethod_DrawsFromMethod()
    {
        ParameterInfo[] parameters = ParamsOf(nameof(WithEvenInts));

        for (int i = 0; i < 20; i++)
        {
            object[] args = SharedParameterStrategyResolver.Resolve(parameters, MakeData((ulong)i));
            int value = Assert.IsType<int>(args[0]);
            Assert.Equal(0, value % 2);
            Assert.InRange(value, 0, 50);
        }
    }

    // ─── Missing method ───────────────────────────────────────────────────────

    [Fact]
    public void Resolve_MissingMethod_ThrowsWithMethodNameInMessage()
    {
        ParameterInfo[] parameters = ParamsOf(nameof(WithMissingMethod));

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => SharedParameterStrategyResolver.Resolve(parameters, MakeData()));

        Assert.Contains("NoSuchMethod", ex.Message);
    }

    // ─── Non-static method ────────────────────────────────────────────────────

    [Fact]
    public void Resolve_NonStaticMethod_ThrowsClearError()
    {
        ParameterInfo[] parameters = ParamsOf(nameof(WithNonStaticFactory));

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => SharedParameterStrategyResolver.Resolve(parameters, MakeData()));

        Assert.Contains("static", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ─── Wrong return type ────────────────────────────────────────────────────

    [Fact]
    public void Resolve_WrongReturnType_ThrowsClearError()
    {
        ParameterInfo[] parameters = ParamsOf(nameof(WithWrongReturnType));

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => SharedParameterStrategyResolver.Resolve(parameters, MakeData()));

        Assert.Contains("Strategy<", ex.Message);
    }
}