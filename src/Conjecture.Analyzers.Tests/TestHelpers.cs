// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Immutable;
using System.IO;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;

namespace Conjecture.Analyzers.Tests;

/// <summary>
/// Shared reference helpers for tests that add Conjecture.Core as an additional reference.
/// Uses runtime assemblies instead of the framework's NuGet-sourced reference assemblies to
/// avoid CS1705 version mismatches when Conjecture.Core targets a newer TFM.
/// </summary>
internal static class TestHelpers
{
    /// <summary>
    /// A <see cref="ReferenceAssemblies" /> with no NuGet packages — callers must supply all
    /// needed assemblies via <see cref="AddRuntimeReferences" />.
    /// </summary>
    internal static readonly ReferenceAssemblies EmptyNet10 = new("net10.0");

    /// <summary>Adds BCL runtime assemblies and Conjecture.Core to the given reference list.</summary>
    internal static void AddRuntimeReferences(IList<MetadataReference> refs)
    {
        string runtimeDir = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        refs.Add(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));
        refs.Add(MetadataReference.CreateFromFile(Path.Combine(runtimeDir, "System.Runtime.dll")));
        refs.Add(MetadataReference.CreateFromFile(Path.Combine(runtimeDir, "System.Collections.dll")));
        refs.Add(MetadataReference.CreateFromFile(Path.Combine(runtimeDir, "System.Numerics.dll")));
        refs.Add(MetadataReference.CreateFromFile(
            typeof(Conjecture.Core.Strategy).Assembly.Location));
    }
}