// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.ExceptionServices;

namespace Conjecture.Core.Internal;

internal static class SharedParameterStrategyResolver
{
    private static readonly Type OpenFromAttribute = typeof(FromAttribute<>).GetGenericTypeDefinition();
    private static readonly Type OpenStrategyProvider = typeof(IStrategyProvider<>);
    private static readonly MethodInfo GenerateFromProviderOpenMethod =
        typeof(SharedParameterStrategyResolver)
            .GetMethod(nameof(GenerateFromProvider), BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly ConcurrentDictionary<Type, MethodInfo?> ArbitraryProviderCache = new();

    [RequiresUnreferencedCode("Accesses parameter type metadata via reflection; not trim-safe.")]
    [RequiresDynamicCode("Uses MakeGenericMethod for typed strategy dispatch; not NativeAOT-safe.")]
    internal static object[] Resolve(ParameterInfo[] parameters, ConjectureData data)
    {
        object[] args = new object[parameters.Length];
        for (int i = 0; i < parameters.Length; i++)
        {
            args[i] = TryGenerateFromAttribute(parameters[i], data)
                ?? TryGenerateFromFactory(parameters[i], data)
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
    private static object? TryGenerateFromFactory(ParameterInfo parameter, ConjectureData data)
    {
        FromFactoryAttribute? attr = parameter.GetCustomAttribute<FromFactoryAttribute>();
        if (attr is null)
        {
            return null;
        }

        Type declaringType = parameter.Member.DeclaringType
            ?? throw new InvalidOperationException(
                $"[FromFactory] on parameter '{parameter.Name}' could not resolve its declaring type.");

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
            .GetMethod(nameof(GenerateFromFactory), BindingFlags.NonPublic | BindingFlags.Static)!
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

    private static object GenerateFromFactory<T>(MethodInfo factory, ConjectureData data)
    {
        Strategy<T> strategy = (Strategy<T>)factory.Invoke(null, [])!;
        return strategy.Generate(data)!;
    }

    private static object GenerateFromProvider<TProvider, T>(ConjectureData data)
        where TProvider : IStrategyProvider<T>, new()
    {
        TProvider provider = new();
        Strategy<T> strategy = provider.Create();
        return strategy.Generate(data)!;
    }

    private static object GenerateValue(Type type, ConjectureData data)
    {
        return type switch
        {
            _ when type == typeof(int) => Generate.Integers<int>().Generate(data),
            _ when type == typeof(long) => Generate.Integers<long>().Generate(data),
            _ when type == typeof(byte) => Generate.Integers<byte>().Generate(data),
            _ when type == typeof(bool) => Generate.Booleans().Generate(data),
            _ when type == typeof(string) => Generate.Strings().Generate(data),
            _ when type == typeof(float) => Generate.Floats().Generate(data),
            _ when type == typeof(double) => Generate.Doubles().Generate(data),
            _ when type == typeof(List<int>) => Generate.Lists(Generate.Integers<int>()).Generate(data),
            _ when type == typeof(DateTimeOffset) => Generate.DateTimeOffsets().Generate(data),
            _ when type == typeof(TimeSpan) => Generate.TimeSpans().Generate(data),
            _ when type == typeof(DateOnly) => Generate.DateOnlyValues().Generate(data),
            _ when type == typeof(TimeOnly) => Generate.TimeOnlyValues().Generate(data),
            { IsEnum: true } => GenerateEnum(type, data),
            _ when Nullable.GetUnderlyingType(type) is { } u
                                             => data.NextInteger(0, 9) == 0 ? null! : GenerateValue(u, data),
            _ => throw new NotSupportedException($"No strategy registered for parameter type '{type.FullName}'.")
        };
    }

    private static object GenerateEnum(Type type, ConjectureData data)
    {
        Array values = Enum.GetValues(type);
        int idx = (int)data.NextInteger(0, (ulong)(values.Length - 1));
        return values.GetValue(idx)!;
    }
}