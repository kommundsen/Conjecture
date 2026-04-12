// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.Loader;

namespace Conjecture.Core.Internal;

internal static class ArbitraryProviderScanner
{
    private static readonly Type OpenStrategyProvider = typeof(IStrategyProvider<>);

    private static volatile bool assembliesLoaded;
    private static readonly object LoadLock = new();

    /// <summary>
    /// Scans all loaded assemblies (and probes the app's base directory for any unloaded assemblies)
    /// for a type named <c>{paramType.Name}Arbitrary</c> that implements
    /// <see cref="IStrategyProvider{T}"/> for <paramref name="paramType"/>, and returns a closed
    /// <paramref name="generateFromProviderOpenMethod"/> ready to invoke.
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

        EnsureAssembliesLoaded();

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

    /// <summary>
    /// Probes all <c>*.dll</c> files in the application's base directory and loads any that
    /// have not yet been loaded into the default <see cref="AssemblyLoadContext"/>.
    /// This ensures that assemblies containing <c>[Arbitrary]</c> providers are visible to the
    /// scanner even if no type in those assemblies has been JIT-referenced yet.
    /// Guaranteed to run at most once per process via double-checked locking.
    /// </summary>
    [RequiresUnreferencedCode("Loads assemblies from disk by path; not trim-safe.")]
    private static void EnsureAssembliesLoaded()
    {
        if (assembliesLoaded)
        {
            return;
        }

        lock (LoadLock)
        {
            if (assembliesLoaded)
            {
                return;
            }

            string? baseDir = AppDomain.CurrentDomain.BaseDirectory;
            if (baseDir is null)
            {
                assembliesLoaded = true;
                return;
            }

            HashSet<string> loaded = new(StringComparer.OrdinalIgnoreCase);
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (!assembly.IsDynamic && assembly.Location is { Length: > 0 } loc)
                {
                    loaded.Add(Path.GetFullPath(loc));
                }
            }

            foreach (string dll in Directory.EnumerateFiles(baseDir, "*.dll"))
            {
                if (loaded.Contains(Path.GetFullPath(dll)))
                {
                    continue;
                }

                try
                {
                    AssemblyLoadContext.Default.LoadFromAssemblyPath(dll);
                }
                catch (BadImageFormatException) { }
                catch (FileLoadException) { }
                catch (IOException) { }
            }

            assembliesLoaded = true;
        }
    }
}