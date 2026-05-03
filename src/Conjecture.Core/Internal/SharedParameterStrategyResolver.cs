// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;

namespace Conjecture.Core.Internal;

internal static class SharedParameterStrategyResolver
{
    private static readonly Type OpenFromAttribute = typeof(FromAttribute<>).GetGenericTypeDefinition();
    private static readonly Type OpenStrategyProvider = typeof(IStrategyProvider<>);
    private static readonly MethodInfo GenerateFromProviderOpenMethod =
        typeof(SharedParameterStrategyResolver)
            .GetMethod(nameof(GenerateFromProvider), BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly MethodInfo GenerateBinaryIntegerOpenMethod =
        typeof(SharedParameterStrategyResolver)
            .GetMethod(nameof(GenerateBinaryInteger), BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly ConcurrentDictionary<Type, MethodInfo?> ArbitraryProviderCache = new();
    private static readonly ConcurrentDictionary<Type, MethodInfo?> BinaryIntegerCache = new();

    [RequiresUnreferencedCode("Accesses parameter type metadata via reflection; not trim-safe.")]
    [RequiresDynamicCode("Uses MakeGenericMethod for typed strategy dispatch; not NativeAOT-safe.")]
    internal static object[] Resolve(ParameterInfo[] parameters, ConjectureData data)
    {
        object[] args = new object[parameters.Length];
        for (int i = 0; i < parameters.Length; i++)
        {
            args[i] = TryGenerateFromAttribute(parameters[i], data)
                ?? TryGenerateFromMethod(parameters[i], data)
                ?? TryGenerateFromArbitraryProvider(parameters[i], data)
                ?? GenerateValue(parameters[i].ParameterType, data);
        }
        return args;
    }

    [RequiresUnreferencedCode("Accesses provider type interfaces via reflection.")]
    [RequiresDynamicCode("Calls MakeGenericMethod to construct typed generate helper.")]
    private static object? TryGenerateFromAttribute(ParameterInfo parameter, ConjectureData data)
    {
        foreach (Attribute attr in parameter.GetCustomAttributes())
        {
            Type attrType = attr.GetType();
            if (!attrType.IsGenericType || attrType.GetGenericTypeDefinition() != OpenFromAttribute)
            {
                continue;
            }

            Type providerType = attrType.GetGenericArguments()[0];
            Type? providerInterface = providerType.GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == OpenStrategyProvider);

            if (providerInterface is null)
            {
                throw new InvalidOperationException(
                    $"Provider '{providerType.Name}' does not implement IStrategyProvider<T>.");
            }

            Type strategyValueType = providerInterface.GetGenericArguments()[0];
            if (strategyValueType != parameter.ParameterType)
            {
                throw new InvalidOperationException(
                    $"Provider '{providerType.Name}' generates '{strategyValueType.Name}' " +
                    $"but parameter '{parameter.Name}' has type '{parameter.ParameterType.Name}'.");
            }

            MethodInfo drawMethod = GenerateFromProviderOpenMethod.MakeGenericMethod(providerType, strategyValueType);
            return drawMethod.Invoke(null, [data])!;
        }

        return null;
    }

    [RequiresUnreferencedCode("Accesses declaring type methods via reflection.")]
    [RequiresDynamicCode("Calls MakeGenericMethod to construct typed generate helper.")]
    private static object? TryGenerateFromMethod(ParameterInfo parameter, ConjectureData data)
    {
        FromMethodAttribute? attr = parameter.GetCustomAttribute<FromMethodAttribute>();
        if (attr is null)
        {
            return null;
        }

        Type declaringType = parameter.Member.DeclaringType
            ?? throw new InvalidOperationException(
                $"[FromMethod] on parameter '{parameter.Name}' could not resolve its declaring type.");

        MethodInfo? method = declaringType.GetMethod(
            attr.MethodName,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);

        if (method is null)
        {
            throw new InvalidOperationException(
                $"Method '{attr.MethodName}' not found on '{declaringType.Name}'.");
        }

        if (!method.IsStatic)
        {
            throw new InvalidOperationException(
                $"Method '{attr.MethodName}' on '{declaringType.Name}' must be static.");
        }

        Type expectedStrategyType = typeof(Strategy<>).MakeGenericType(parameter.ParameterType);
        if (!method.ReturnType.IsAssignableTo(expectedStrategyType))
        {
            throw new InvalidOperationException(
                $"Method '{attr.MethodName}' returns '{method.ReturnType.Name}' " +
                $"but must return Strategy<{parameter.ParameterType.Name}>.");
        }

        MethodInfo drawMethod = typeof(SharedParameterStrategyResolver)
            .GetMethod(nameof(GenerateFromMethod), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(parameter.ParameterType);

        return drawMethod.Invoke(null, [method, data])!;
    }

    [RequiresUnreferencedCode("Scans loaded assemblies for provider types by name; not trim-safe.")]
    [RequiresDynamicCode("Calls MakeGenericMethod to construct typed generate helper.")]
    private static object? TryGenerateFromArbitraryProvider(ParameterInfo parameter, ConjectureData data)
    {
        // Check the source-generated AOT-safe registry first.
        object? registered = ConjectureStrategyRegistrar.TryResolve(parameter.ParameterType, data);
        if (registered is not null)
        {
            return registered;
        }

        Type paramType = parameter.ParameterType;
        MethodInfo? drawMethod = ArbitraryProviderCache.GetOrAdd(paramType, FindArbitraryGenerateMethod);
        if (drawMethod is null)
        {
            return null;
        }

        try
        {
            return drawMethod.Invoke(null, [data]);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            return null; // unreachable
        }
    }

    [RequiresUnreferencedCode("Scans loaded assemblies for provider types by name; not trim-safe.")]
    [RequiresDynamicCode("Calls MakeGenericMethod to construct typed generate helper.")]
    private static MethodInfo? FindArbitraryGenerateMethod(Type paramType) =>
        ArbitraryProviderScanner.FindGenerateMethod(paramType, GenerateFromProviderOpenMethod);

    private static object GenerateFromMethod<T>(MethodInfo method, ConjectureData data)
    {
        Strategy<T> strategy = (Strategy<T>)method.Invoke(null, [])!;
        return strategy.Generate(data)!;
    }

    private static object GenerateFromProvider<TProvider, T>(ConjectureData data)
        where TProvider : IStrategyProvider<T>, new()
    {
        TProvider provider = new();
        Strategy<T> strategy = provider.Create();
        return strategy.Generate(data)!;
    }

    [RequiresUnreferencedCode("Accesses parameter type metadata via reflection; not trim-safe.")]
    [RequiresDynamicCode("Uses MakeGenericMethod for typed strategy dispatch; not NativeAOT-safe.")]
    internal static object GenerateValueForDefault(Type type, ConjectureData data) =>
        GenerateValue(type, data);

    private static object GenerateValue(Type type, ConjectureData data)
    {
        return type switch
        {
            _ when type == typeof(int) => Strategy.Integers<int>().Generate(data),
            _ when type == typeof(long) => Strategy.Integers<long>().Generate(data),
            _ when type == typeof(byte) => Strategy.Integers<byte>().Generate(data),
            _ when type == typeof(uint) => Strategy.Integers<uint>().Generate(data),
            _ when type == typeof(ulong) => Strategy.Integers<ulong>().Generate(data),
            _ when type == typeof(ushort) => Strategy.Integers<ushort>().Generate(data),
            _ when type == typeof(short) => Strategy.Integers<short>().Generate(data),
            _ when type == typeof(sbyte) => Strategy.Integers<sbyte>().Generate(data),
            _ when type == typeof(bool) => Strategy.Booleans().Generate(data),
            _ when type == typeof(string) => Strategy.Strings().Generate(data),
            _ when type == typeof(float) => Strategy.Floats().Generate(data),
            _ when type == typeof(double) => Strategy.Doubles().Generate(data),
            _ when type == typeof(Half) => Strategy.Halves().Generate(data),
            _ when type == typeof(List<int>) => Strategy.Lists(Strategy.Integers<int>()).Generate(data),
            _ when type == typeof(byte[]) => Strategy.Arrays(Strategy.Integers<byte>(), 0, 32).Generate(data),
            _ when type == typeof(int[]) => Strategy.Arrays(Strategy.Integers<int>(), 0, 32).Generate(data),
            _ when type == typeof(string[]) => Strategy.Arrays(Strategy.Strings(), 0, 16).Generate(data),
            _ when type == typeof(DateTimeOffset) => Strategy.DateTimeOffsets().Generate(data),
            _ when type == typeof(TimeSpan) => Strategy.TimeSpans().Generate(data),
            _ when type == typeof(DateOnly) => Strategy.DateOnlyValues().Generate(data),
            _ when type == typeof(TimeOnly) => Strategy.TimeOnlyValues().Generate(data),
            _ when type == typeof(Rune) => Strategy.Runes().Generate(data),
            { IsEnum: true } => GenerateEnum(type, data),
            _ when Nullable.GetUnderlyingType(type) is { } u
                                             => data.NextInteger(0, 9) == 0 ? null! : GenerateValue(u, data),
            _ when TryGenerateBinaryInteger(type, data) is { } v => v,
            _ => throw new NotSupportedException($"No strategy registered for parameter type '{type.FullName}'.")
        };
    }

    private static object? TryGenerateBinaryInteger(Type type, ConjectureData data)
    {
        MethodInfo? method = BinaryIntegerCache.GetOrAdd(type, FindBinaryIntegerGenerator);
        if (method is null)
        {
            return null;
        }

        try
        {
            return method.Invoke(null, [data]);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            return null;
        }
    }

    [UnconditionalSuppressMessage("Trimming", "IL2055", Justification = "Reflective IBinaryInteger fallback is annotated as not trim-safe at the entry point.")]
    [UnconditionalSuppressMessage("Trimming", "IL2060", Justification = "Reflective IBinaryInteger fallback is annotated as not trim-safe at the entry point.")]
    [UnconditionalSuppressMessage("Trimming", "IL2070", Justification = "Reflective IBinaryInteger fallback is annotated as not trim-safe at the entry point.")]
    [UnconditionalSuppressMessage("Trimming", "IL3050", Justification = "Reflective IBinaryInteger fallback is annotated as not AOT-safe at the entry point.")]
    private static MethodInfo? FindBinaryIntegerGenerator(Type type)
    {
        if (!type.IsValueType)
        {
            return null;
        }

        bool implementsBinaryInteger = false;
        bool implementsMinMaxValue = false;
        foreach (Type iface in type.GetInterfaces())
        {
            if (!iface.IsGenericType) { continue; }
            Type def = iface.GetGenericTypeDefinition();
            if (def == typeof(IBinaryInteger<>) && iface.GetGenericArguments()[0] == type)
            {
                implementsBinaryInteger = true;
            }
            else if (def == typeof(IMinMaxValue<>) && iface.GetGenericArguments()[0] == type)
            {
                implementsMinMaxValue = true;
            }
        }

        if (!implementsBinaryInteger || !implementsMinMaxValue)
        {
            return null;
        }

        return GenerateBinaryIntegerOpenMethod.MakeGenericMethod(type);
    }

    private static object GenerateBinaryInteger<T>(ConjectureData data)
        where T : IBinaryInteger<T>, IMinMaxValue<T>
    {
        return Strategy.Integers<T>().Generate(data)!;
    }

    private static object GenerateEnum(Type type, ConjectureData data)
    {
        Array values = Enum.GetValues(type);
        int idx = (int)data.NextInteger(0, (ulong)(values.Length - 1));
        return values.GetValue(idx)!;
    }

    internal static Strategy<T> GetDefault<T>() => DefaultStrategyCache<T>.Instance;

    private static class DefaultStrategyCache<T>
    {
        internal static readonly DefaultStrategy<T> Instance = new();
    }
}