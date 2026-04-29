// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Reflection;

using Conjecture.Core;
using Conjecture.Core.Internal;


namespace Conjecture.Xunit.Tests;

public class XunitV2SharedResolverTests
{
    // ── [Arbitrary] type for auto-discovery test ──────────────────────────────

    public sealed record Point(int X, int Y);

    // CON201 suppressed: test exercises manual [Arbitrary] discovery, not source generation
#pragma warning disable CON201
    [Arbitrary]
    public sealed class PointArbitrary : IStrategyProvider<Point>
    {
        public Strategy<Point> Create()
        {
            return Strategy.Integers<int>(-100, 100).SelectMany(
                x => Strategy.Integers<int>(-100, 100).Select(y => new Point(x, y)));
        }
    }
#pragma warning restore CON201

    // ── Provider for [From<T>] test ───────────────────────────────────────────

    private sealed class EvenIntProvider : IStrategyProvider<int>
    {
        public Strategy<int> Create()
        {
            return Strategy.Integers<int>(0, 50).Select(n => n * 2);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static ConjectureData MakeData(ulong seed = 42UL)
    {
        return ConjectureData.ForGeneration(new SplittableRandom(seed));
    }

    private static ParameterInfo[] ParamsOf(string methodName)
    {
        return typeof(XunitV2SharedResolverTests)
            .GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static)!
            .GetParameters();
    }

    // ── Stub methods ──────────────────────────────────────────────────────────

#pragma warning disable IDE0060
    private static void TakesInt(int n) { }
    private static void TakesBool(bool b) { }
    private static void TakesString(string s) { }
    private static void TakesFloat(float f) { }
    private static void TakesDouble(double d) { }
    private static void TakesFromEven([From<EvenIntProvider>] int n) { }
    private static void TakesFromFactory([FromFactory(nameof(DoubleFactory))] int n) { }
    private static void TakesPoint(Point p) { }
#pragma warning restore IDE0060

    public static Strategy<int> DoubleFactory()
    {
        return Strategy.Integers<int>(0, 10).Select(n => n * 2);
    }

    // ── Type inference ────────────────────────────────────────────────────────

    [Theory]
    [InlineData(nameof(TakesInt))]
    [InlineData(nameof(TakesBool))]
    [InlineData(nameof(TakesString))]
    [InlineData(nameof(TakesFloat))]
    [InlineData(nameof(TakesDouble))]
    public void Resolve_PrimitiveType_ReturnsValue(string methodName)
    {
        ParameterInfo[] parameters = ParamsOf(methodName);

        object[] args = SharedParameterStrategyResolver.Resolve(parameters, MakeData());

        Assert.Single(args);
        Assert.NotNull(args[0]);
    }

    // ── [From<T>] ─────────────────────────────────────────────────────────────

    [Fact]
    public void Resolve_FromAttribute_DrawsFromProvider()
    {
        ParameterInfo[] parameters = ParamsOf(nameof(TakesFromEven));

        for (int i = 0; i < 20; i++)
        {
            object[] args = SharedParameterStrategyResolver.Resolve(parameters, MakeData((ulong)i));
            int value = Assert.IsType<int>(args[0]);
            Assert.Equal(0, value % 2);
        }
    }

    // ── [FromFactory] ─────────────────────────────────────────────────────────

    [Fact]
    public void Resolve_FromFactory_DrawsFromStaticMethod()
    {
        ParameterInfo[] parameters = ParamsOf(nameof(TakesFromFactory));

        for (int i = 0; i < 20; i++)
        {
            object[] args = SharedParameterStrategyResolver.Resolve(parameters, MakeData((ulong)i));
            int value = Assert.IsType<int>(args[0]);
            Assert.Equal(0, value % 2);
            Assert.InRange(value, 0, 20);
        }
    }

    // ── [Arbitrary] auto-discovery ────────────────────────────────────────────

    [Fact]
    public void Resolve_ArbitraryProvider_AutoDiscoveredByConvention()
    {
        ParameterInfo[] parameters = ParamsOf(nameof(TakesPoint));

        object[] args = SharedParameterStrategyResolver.Resolve(parameters, MakeData());

        Point p = Assert.IsType<Point>(args[0]);
        Assert.InRange(p.X, -100, 100);
        Assert.InRange(p.Y, -100, 100);
    }
}