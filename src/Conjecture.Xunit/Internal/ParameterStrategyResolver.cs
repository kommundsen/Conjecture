using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Conjecture.Core;
using Conjecture.Core.Internal;

namespace Conjecture.Xunit.Internal;

internal static class ParameterStrategyResolver
{
    [RequiresUnreferencedCode("Accesses parameter type metadata via reflection; not trim-safe.")]
    [RequiresDynamicCode("Uses MakeGenericMethod for typed strategy dispatch; not NativeAOT-safe.")]
    internal static object[] Resolve(ParameterInfo[] parameters, ConjectureData data)
    {
        return SharedParameterStrategyResolver.Resolve(parameters, data);
    }
}
