using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Conjecture.Core.Internal;

internal static class ArbitraryProviderScanner
{
    private static readonly Type OpenStrategyProvider = typeof(IStrategyProvider<>);

    /// <summary>
    /// Scans all loaded assemblies for a type named <c>{paramType.Name}Arbitrary</c> that
    /// implements <see cref="IStrategyProvider{T}"/> for <paramref name="paramType"/>, and
    /// returns a closed <paramref name="generateFromProviderOpenMethod"/> ready to invoke.
    /// Returns <see langword="null"/> when no matching provider is found.
    /// The caller is responsible for caching the result.
    /// </summary>
    [RequiresUnreferencedCode("Scans loaded assemblies for provider types by name; not trim-safe.")]
    [RequiresDynamicCode("Calls MakeGenericMethod to construct typed generate helper.")]
    internal static MethodInfo? FindGenerateMethod(Type paramType, MethodInfo generateFromProviderOpenMethod)
    {
        string candidateName = paramType.Name + "Arbitrary";
        // Source-generated providers put [Arbitrary] on the record, not the provider class.
        bool paramTypeMarked = paramType.GetCustomAttribute<ArbitraryAttribute>() is not null;

        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types.Where(t => t is not null).ToArray()!;
            }

            foreach (Type type in types)
            {
                if (type.Name != candidateName)
                {
                    continue;
                }

                if (!paramTypeMarked && type.GetCustomAttribute<ArbitraryAttribute>() is null)
                {
                    continue;
                }

                if (!type.GetInterfaces().Any(i => i.IsGenericType
                        && i.GetGenericTypeDefinition() == OpenStrategyProvider
                        && i.GetGenericArguments()[0] == paramType))
                {
                    continue;
                }

                return generateFromProviderOpenMethod.MakeGenericMethod(type, paramType);
            }
        }

        return null;
    }
}
