// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Reflection;
using Conjecture.Core;
using Conjecture.Core.Internal;

namespace Conjecture.Core.Tests.Internal;

public class SharedParameterStrategyResolverTests
{
    private sealed class PositiveIntsProvider : IStrategyProvider<int>
    {
        public Strategy<int> Create() => Generate.Integers<int>(1, int.MaxValue);
    }

    private sealed class StringProvider : IStrategyProvider<string>
    {
        public Strategy<string> Create() => Generate.Strings();
    }

    private static ConjectureData MakeData(ulong seed = 42UL) =>
        ConjectureData.ForGeneration(new SplittableRandom(seed));

    // ─── Factory methods used as [FromFactory] targets ───────────────────────

    public static Strategy<int> EvenInts() =>
        Generate.Integers<int>(0, 25).Select(n => n * 2);

    public Strategy<int> NonStaticFactory() => Generate.Integers<int>();

    public static string WrongReturnTypeFactory() => "not a strategy";

    // ─── Helper stubs for ParameterInfo extraction ───────────────────────────

#pragma warning disable IDE0060
    private static void IntParamMethod(int n) { }
    private static void BoolParamMethod(bool b) { }
    private static void StringParamMethod(string s) { }
    private static void FloatParamMethod(float f) { }
    private static void DoubleParamMethod(double d) { }
    private static void ListIntParamMethod(List<int> xs) { }
    private static void DayOfWeekParamMethod(DayOfWeek day) { }
    private static void NullableIntParamMethod(int? x) { }
    private static void GuidParamMethod(Guid g) { }

    private static void FromPositiveInts([From<PositiveIntsProvider>] int n) { }
    private static void WrongProviderType([From<StringProvider>] int n) { }
    private static void MixedParams([From<PositiveIntsProvider>] int n, string s) { }

    private static void WithEvenInts([FromFactory(nameof(EvenInts))] int n) { }
    private static void WithMissingMethod([FromFactory("NoSuchMethod")] int n) { }
    private static void WithNonStaticFactory([FromFactory(nameof(NonStaticFactory))] int n) { }
    private static void WithWrongReturnType([FromFactory(nameof(WrongReturnTypeFactory))] int n) { }
#pragma warning restore IDE0060

    private static ParameterInfo[] ParamsOf(string methodName) =>
        typeof(SharedParameterStrategyResolverTests)
            .GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static)!
            .GetParameters();

    // ─── Type-inference fallback ──────────────────────────────────────────────

    [Fact]
    public void Resolve_IntParam_ReturnsInt()
    {
        object[] args = SharedParameterStrategyResolver.Resolve(ParamsOf(nameof(IntParamMethod)), MakeData());
        Assert.IsType<int>(args[0]);
    }

    [Fact]
    public void Resolve_BoolParam_ReturnsBool()
    {
        object[] args = SharedParameterStrategyResolver.Resolve(ParamsOf(nameof(BoolParamMethod)), MakeData());
        Assert.IsType<bool>(args[0]);
    }

    [Fact]
    public void Resolve_StringParam_ReturnsString()
    {
        object[] args = SharedParameterStrategyResolver.Resolve(ParamsOf(nameof(StringParamMethod)), MakeData());
        Assert.IsType<string>(args[0]);
    }

    [Fact]
    public void Resolve_FloatParam_ReturnsFloat()
    {
        object[] args = SharedParameterStrategyResolver.Resolve(ParamsOf(nameof(FloatParamMethod)), MakeData());
        Assert.IsType<float>(args[0]);
    }

    [Fact]
    public void Resolve_DoubleParam_ReturnsDouble()
    {
        object[] args = SharedParameterStrategyResolver.Resolve(ParamsOf(nameof(DoubleParamMethod)), MakeData());
        Assert.IsType<double>(args[0]);
    }

    [Fact]
    public void Resolve_ListIntParam_ReturnsListOfInt()
    {
        object[] args = SharedParameterStrategyResolver.Resolve(ParamsOf(nameof(ListIntParamMethod)), MakeData());
        Assert.IsType<List<int>>(args[0]);
    }

    [Fact]
    public void Resolve_EnumParam_ReturnsValidEnumValue()
    {
        object[] args = SharedParameterStrategyResolver.Resolve(ParamsOf(nameof(DayOfWeekParamMethod)), MakeData());
        Assert.IsType<DayOfWeek>(args[0]);
        Assert.Contains((DayOfWeek)args[0], Enum.GetValues<DayOfWeek>());
    }

    [Fact]
    public void Resolve_NullableIntParam_CanReturnNull()
    {
        ParameterInfo[] parameters = ParamsOf(nameof(NullableIntParamMethod));
        bool seenNull = false;

        for (int i = 0; i < 200 && !seenNull; i++)
        {
            object[] args = SharedParameterStrategyResolver.Resolve(parameters, MakeData((ulong)i));
            if (args[0] is null) { seenNull = true; }
        }

        Assert.True(seenNull, "Expected at least one null value from nullable int? strategy");
    }

    [Fact]
    public void Resolve_NullableIntParam_CanReturnNonNullInt()
    {
        ParameterInfo[] parameters = ParamsOf(nameof(NullableIntParamMethod));
        bool seenNonNull = false;

        for (int i = 0; i < 200 && !seenNonNull; i++)
        {
            object[] args = SharedParameterStrategyResolver.Resolve(parameters, MakeData((ulong)i));
            if (args[0] is not null)
            {
                Assert.IsType<int>(args[0]);
                seenNonNull = true;
            }
        }

        Assert.True(seenNonNull, "Expected at least one non-null int value from nullable int? strategy");
    }

    [Fact]
    public void Resolve_UnsupportedType_ThrowsNotSupportedExceptionWithTypeName()
    {
        NotSupportedException ex = Assert.Throws<NotSupportedException>(
            () => SharedParameterStrategyResolver.Resolve(ParamsOf(nameof(GuidParamMethod)), MakeData()));
        Assert.Contains("Guid", ex.Message);
    }

    // ─── [From<T>] resolution ─────────────────────────────────────────────────

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
    public void Resolve_FromAttribute_TypeMismatch_ThrowsInvalidOperationException()
    {
        ParameterInfo[] parameters = ParamsOf(nameof(WrongProviderType));

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => SharedParameterStrategyResolver.Resolve(parameters, MakeData()));

        Assert.Contains("string", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("int", ex.Message, StringComparison.OrdinalIgnoreCase);
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

    // ─── [FromFactory] resolution ─────────────────────────────────────────────

    [Fact]
    public void Resolve_FromFactory_DrawsFromNamedStaticMethod()
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

    [Fact]
    public void Resolve_FromFactory_MissingMethod_ThrowsWithMethodNameInMessage()
    {
        ParameterInfo[] parameters = ParamsOf(nameof(WithMissingMethod));

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => SharedParameterStrategyResolver.Resolve(parameters, MakeData()));

        Assert.Contains("NoSuchMethod", ex.Message);
    }

    [Fact]
    public void Resolve_FromFactory_NonStaticMethod_ThrowsClearError()
    {
        ParameterInfo[] parameters = ParamsOf(nameof(WithNonStaticFactory));

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => SharedParameterStrategyResolver.Resolve(parameters, MakeData()));

        Assert.Contains("static", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Resolve_FromFactory_WrongReturnType_ThrowsClearError()
    {
        ParameterInfo[] parameters = ParamsOf(nameof(WithWrongReturnType));

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => SharedParameterStrategyResolver.Resolve(parameters, MakeData()));

        Assert.Contains("Strategy<", ex.Message);
    }
}