using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Conjecture.Core;

namespace Conjecture.Tests;

public class TrimAnnotationTests
{
    [Fact]
    public void ConjectureCore_Assembly_HasIsTrimmableMetadata()
    {
        AssemblyMetadataAttribute? attr = typeof(Gen).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "IsTrimmable");

        Assert.NotNull(attr);
        Assert.Equal("True", attr.Value);
    }

    [Fact]
    public void Gen_PublicMethods_HaveNoTrimUnsafeAnnotations()
    {
        MethodInfo[] methods = typeof(Gen).GetMethods(BindingFlags.Public | BindingFlags.Static);
        MethodInfo[] trimUnsafe = methods
            .Where(m =>
                m.GetCustomAttribute<RequiresUnreferencedCodeAttribute>() is not null ||
                m.GetCustomAttribute<RequiresDynamicCodeAttribute>() is not null)
            .ToArray();

        Assert.Empty(trimUnsafe);
    }
}
