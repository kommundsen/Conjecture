// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;

using Conjecture.Core;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Conjecture.EFCore;

/// <summary>Builds a <see cref="Strategy{T}"/> for a single EF Core property, respecting type-system constraints.</summary>
public static class PropertyStrategyBuilder
{
    /// <summary>Returns a <see cref="Strategy{T}">Strategy&lt;object?&gt;</see> that generates values compatible with <paramref name="property"/>.</summary>
    public static Strategy<object?> Build(IProperty property)
    {
        ArgumentNullException.ThrowIfNull(property);

        if (property.ValueGenerated == ValueGenerated.OnAdd || property.ValueGenerated == ValueGenerated.OnAddOrUpdate)
        {
            object? clrDefault = property.ClrType.IsValueType ? Activator.CreateInstance(property.ClrType) : null;
            return Generate.Constant<object?>(clrDefault);
        }

        Type clrType = property.ClrType;
        Type underlying = Nullable.GetUnderlyingType(clrType) ?? clrType;

        Strategy<object?> inner = BuildInner(property, underlying);

        return property.IsNullable
            ? Generate.OneOf(inner, Generate.Constant<object?>(null))
            : inner;
    }

    private static Strategy<object?> BuildInner(IProperty property, Type underlying)
    {
        if (underlying == typeof(int))
        {
            return Generate.Integers<int>().Select(static v => (object?)v);
        }

        if (underlying == typeof(long))
        {
            return Generate.Integers<long>().Select(static v => (object?)v);
        }

        if (underlying == typeof(short))
        {
            return Generate.Integers<short>().Select(static v => (object?)v);
        }

        if (underlying == typeof(byte))
        {
            return Generate.Integers<byte>().Select(static v => (object?)v);
        }

        if (underlying == typeof(sbyte))
        {
            return Generate.Integers<sbyte>().Select(static v => (object?)v);
        }

        if (underlying == typeof(uint))
        {
            return Generate.Integers<uint>().Select(static v => (object?)v);
        }

        if (underlying == typeof(ulong))
        {
            return Generate.Integers<ulong>().Select(static v => (object?)v);
        }

        if (underlying == typeof(ushort))
        {
            return Generate.Integers<ushort>().Select(static v => (object?)v);
        }

        if (underlying == typeof(bool))
        {
            return Generate.Booleans().Select(static v => (object?)v);
        }

        if (underlying == typeof(decimal))
        {
            return BuildDecimalStrategy(property);
        }

        if (underlying == typeof(double))
        {
            return Generate.Doubles().Select(static v => (object?)v);
        }

        if (underlying == typeof(float))
        {
            return Generate.Floats().Select(static v => (object?)v);
        }

        if (underlying == typeof(string))
        {
            return BuildStringStrategy(property);
        }

        if (underlying == typeof(Guid))
        {
            return Generate.Guids().Select(static v => (object?)v);
        }

        if (underlying == typeof(DateTime))
        {
            return Generate.DateTimes().Select(static v => (object?)v);
        }

        if (underlying == typeof(DateTimeOffset))
        {
            return Generate.DateTimeOffsets().Select(static v => (object?)v);
        }

        if (underlying == typeof(byte[]))
        {
            int maxLength = property.GetMaxLength() ?? 256;
            return Generate.Integers<int>(0, maxLength)
                .SelectMany(len => Generate.Bytes(len))
                .Select(static b => (object?)b);
        }

        if (underlying.IsEnum)
        {
            object[] values = Enum.GetValues(underlying) is object[] arr ? arr : Array.Empty<object>();
            return Generate.SampledFrom(values).Select(v => (object?)v);
        }

        return Generate.Constant<object?>(null);
    }

    private static Strategy<object?> BuildDecimalStrategy(IProperty property)
    {
        int? precision = property.GetPrecision();
        int? scale = property.GetScale();

        if (precision is null || scale is null)
        {
            return Generate.Decimals().Select(static v => (object?)v);
        }

        int integerDigits = precision.Value - scale.Value;
        decimal maxMagnitude = 1m;
        for (int i = 0; i < integerDigits; i++)
        {
            maxMagnitude *= 10m;
        }

        decimal scaleMultiplier = 1m;
        for (int i = 0; i < scale.Value; i++)
        {
            scaleMultiplier *= 10m;
        }

        decimal mag = maxMagnitude;
        decimal mult = scaleMultiplier;

        return Generate.Decimals(-mag, mag).Select(v =>
        {
            decimal rounded = Math.Truncate(v * mult) / mult;
            return (object?)rounded;
        });
    }

    private static Strategy<object?> BuildStringStrategy(IProperty property)
    {
        int maxLength = property.GetMaxLength() ?? 4096;
        return Generate.Strings(minLength: 0, maxLength: maxLength).Select(static s => (object?)s);
    }
}