using System.Reflection;
using Conjecture.Core;
using Conjecture.Core.Generation;
using Conjecture.Core.Internal;

namespace Conjecture.Xunit.Internal;

internal static class ParameterStrategyResolver
{
    internal static object[] Resolve(ParameterInfo[] parameters, ConjectureData data)
    {
        object[] args = new object[parameters.Length];
        for (int i = 0; i < parameters.Length; i++)
        {
            args[i] = DrawValue(parameters[i].ParameterType, data);
        }
        return args;
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
