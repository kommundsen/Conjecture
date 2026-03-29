using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Conjecture.Core;
using Conjecture.Core.Generation;
using Conjecture.Core.Internal;

namespace Conjecture.Xunit.Internal;

internal static class ParameterStrategyResolver
{
    private static readonly Type OpenFromAttribute = typeof(FromAttribute<>).GetGenericTypeDefinition();
    private static readonly Type OpenStrategyProvider = typeof(IStrategyProvider<>);

    [RequiresUnreferencedCode("Accesses parameter type metadata via reflection; not trim-safe.")]
    internal static object[] Resolve(ParameterInfo[] parameters, ConjectureData data)
    {
        object[] args = new object[parameters.Length];
        for (int i = 0; i < parameters.Length; i++)
        {
            args[i] = TryDrawFromAttribute(parameters[i], data)
                ?? TryDrawFromFactory(parameters[i], data)
                ?? DrawValue(parameters[i].ParameterType, data);
        }
        return args;
    }

    private static object? TryDrawFromAttribute(ParameterInfo parameter, ConjectureData data)
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

            MethodInfo drawMethod = typeof(ParameterStrategyResolver)
                .GetMethod(nameof(DrawFromProvider), BindingFlags.NonPublic | BindingFlags.Static)!
                .MakeGenericMethod(providerType, strategyValueType);

            return drawMethod.Invoke(null, [data])!;
        }

        return null;
    }

    private static object? TryDrawFromFactory(ParameterInfo parameter, ConjectureData data)
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

        MethodInfo drawMethod = typeof(ParameterStrategyResolver)
            .GetMethod(nameof(DrawFromFactory), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(parameter.ParameterType);

        return drawMethod.Invoke(null, [method, data])!;
    }

    private static object DrawFromFactory<T>(MethodInfo factory, ConjectureData data)
    {
        Strategy<T> strategy = (Strategy<T>)factory.Invoke(null, [])!;
        return strategy.Next(data)!;
    }

    private static object DrawFromProvider<TProvider, T>(ConjectureData data)
        where TProvider : IStrategyProvider<T>, new()
    {
        TProvider provider = new();
        Strategy<T> strategy = provider.Create();
        return strategy.Next(data)!;
    }

    private static object DrawValue(Type type, ConjectureData data)
    {
        return type switch
        {
            _ when type == typeof(int)       => Gen.Integers<int>().Next(data),
            _ when type == typeof(long)      => Gen.Integers<long>().Next(data),
            _ when type == typeof(byte)      => Gen.Integers<byte>().Next(data),
            _ when type == typeof(bool)      => Gen.Booleans().Next(data),
            _ when type == typeof(string)    => Gen.Strings().Next(data),
            _ when type == typeof(float)     => Gen.Floats().Next(data),
            _ when type == typeof(double)    => Gen.Doubles().Next(data),
            _ when type == typeof(List<int>) => Gen.Lists(Gen.Integers<int>()).Next(data),
            { IsEnum: true }                 => DrawEnum(type, data),
            _ when Nullable.GetUnderlyingType(type) is { } u
                                             => data.DrawInteger(0, 9) == 0 ? null! : DrawValue(u, data),
            _                                => throw new NotSupportedException($"No strategy registered for parameter type '{type.FullName}'.")
        };
    }

    private static object DrawEnum(Type type, ConjectureData data)
    {
        Array values = Enum.GetValues(type);
        int idx = new IntegerStrategy<int>(0, values.Length - 1).Next(data);
        return values.GetValue(idx)!;
    }
}
