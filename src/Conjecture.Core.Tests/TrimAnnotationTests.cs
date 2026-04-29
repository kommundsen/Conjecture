// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using Conjecture.Core;

namespace Conjecture.Core.Tests;

public class TrimAnnotationTests
{
    [Fact]
    public void ConjectureCore_Assembly_HasIsTrimmableMetadata()
    {
        AssemblyMetadataAttribute? attr = typeof(Strategy).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "IsTrimmable");

        Assert.NotNull(attr);
        Assert.Equal("True", attr.Value);
    }

    [Fact]
    public void Strategy_PublicMethods_HaveNoTrimUnsafeAnnotations()
    {
        MethodInfo[] methods = typeof(Strategy).GetMethods(BindingFlags.Public | BindingFlags.Static);
        MethodInfo[] trimUnsafe = methods
            .Where(m =>
                m.GetCustomAttribute<RequiresUnreferencedCodeAttribute>() is not null ||
                m.GetCustomAttribute<RequiresDynamicCodeAttribute>() is not null)
            .ToArray();

        Assert.Empty(trimUnsafe);
    }
}