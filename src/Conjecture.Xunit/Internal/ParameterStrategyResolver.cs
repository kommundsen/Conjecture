using System.Reflection;
using Conjecture.Core.Generation;
using Conjecture.Core.Internal;

namespace Conjecture.Xunit.Internal;

internal static class ParameterStrategyResolver
{
    internal static object[] Resolve(ParameterInfo[] parameters, ConjectureData data)
    {
        var args = new object[parameters.Length];
        for (var i = 0; i < parameters.Length; i++)
            args[i] = DrawValue(parameters[i].ParameterType, data);
        return args;
    }

    private static object DrawValue(Type type, ConjectureData data)
    {
        if (type == typeof(int))  return new IntegerStrategy<int>(int.MinValue, int.MaxValue).Next(data);
        if (type == typeof(long)) return new IntegerStrategy<long>(long.MinValue, long.MaxValue).Next(data);
        if (type == typeof(byte)) return new IntegerStrategy<byte>(byte.MinValue, byte.MaxValue).Next(data);
        if (type == typeof(bool)) return new BooleanStrategy().Next(data);
        throw new NotSupportedException($"No strategy registered for parameter type '{type.FullName}'.");
    }
}
