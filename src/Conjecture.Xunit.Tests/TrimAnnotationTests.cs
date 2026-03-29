using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Conjecture.Xunit.Internal;

namespace Conjecture.Xunit.Tests;

public class TrimAnnotationTests
{
    [Fact]
    public void ParameterStrategyResolver_Resolve_HasRequiresUnreferencedCode()
    {
        MethodInfo? method = typeof(ParameterStrategyResolver)
            .GetMethod("Resolve", BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);
        RequiresUnreferencedCodeAttribute? attr =
            method.GetCustomAttribute<RequiresUnreferencedCodeAttribute>();

        Assert.NotNull(attr);
    }

    [Fact]
    public void PropertyTestCaseRunner_RunTestAsync_HasRequiresDynamicCode()
    {
        MethodInfo? method = typeof(PropertyTestCaseRunner)
            .GetMethod("RunTestAsync", BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.NotNull(method);
        RequiresDynamicCodeAttribute? attr =
            method.GetCustomAttribute<RequiresDynamicCodeAttribute>();

        Assert.NotNull(attr);
    }
}
