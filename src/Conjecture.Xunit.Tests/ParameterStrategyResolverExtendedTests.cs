// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Reflection;
using Conjecture.Core;
using Conjecture.Core.Internal;


namespace Conjecture.Xunit.Tests;

public class ParameterStrategyResolverExtendedTests
{
    private static ConjectureData MakeData(ulong seed = 42UL) =>
        ConjectureData.ForGeneration(new SplittableRandom(seed));

#pragma warning disable IDE0060
    private static void StringParamMethod(string s) { }
    private static void FloatParamMethod(float f) { }
    private static void DoubleParamMethod(double d) { }
    private static void ListIntParamMethod(List<int> xs) { }
    private static void DayOfWeekParamMethod(DayOfWeek day) { }
    private static void NullableIntParamMethod(int? x) { }
    private static void GuidParamMethod(Guid g) { }
#pragma warning restore IDE0060

    private static ParameterInfo[] ParamsOf(string methodName) =>
        typeof(ParameterStrategyResolverExtendedTests)
            .GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static)!
            .GetParameters();

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

        Assert.True(seenNonNull, "Expected at least one non-null value from nullable int? strategy");
    }

    [Fact]
    public void Resolve_UnsupportedType_ThrowsNotSupportedExceptionWithTypeName()
    {
        NotSupportedException ex = Assert.Throws<NotSupportedException>(
            () => SharedParameterStrategyResolver.Resolve(ParamsOf(nameof(GuidParamMethod)), MakeData()));
        Assert.Contains("Guid", ex.Message);
    }
}