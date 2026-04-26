// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Diagnostics.CodeAnalysis;

using Conjecture.TestingPlatform.Internal;

using Microsoft.Testing.Platform.Builder;

namespace Conjecture.TestingPlatform;

/// <summary>Extension methods for registering Conjecture with the Microsoft Testing Platform.</summary>
public static class ConjectureTestingPlatformExtensions
{
    extension(ITestApplicationBuilder builder)
    {
        /// <summary>Registers the Conjecture property-based test framework with the test application builder.</summary>
        /// <returns>The same builder instance for chaining.</returns>
        public ITestApplicationBuilder RegisterConjectureFramework()
        {
            builder.CommandLine.AddProvider(static () => new ConjectureCommandLineOptions());
            builder.RegisterTestFramework(
                _ => new PropertyTestFrameworkCapabilities(),
                (_, services) => new PropertyTestFramework(services));
            return builder;
        }
    }

    /// <summary>
    /// Required by <c>Microsoft.Testing.Platform.MSBuild</c>: the generated <c>SelfRegisteredExtensions.cs</c>
    /// always calls <c>TypeName.AddExtensions(builder, args)</c> — <c>MethodName</c> on the
    /// <c>TestingPlatformBuilderHook</c> item is ignored. The <paramref name="args"/> parameter is
    /// part of the mandated signature but is not used here.
    /// </summary>
    [SuppressMessage("Style", "IDE0060:RemoveUnusedParameter",
        Justification = "Required by Microsoft.Testing.Platform MSBuild-generated code.")]
    public static void AddExtensions(ITestApplicationBuilder builder, string[] args)
    {
        builder.RegisterConjectureFramework();
    }
}