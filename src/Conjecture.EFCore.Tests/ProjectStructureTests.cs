// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Reflection;

using Conjecture.EFCore;

namespace Conjecture.EFCore.Tests;

public class ProjectStructureTests
{
    [Fact]
    public void EFCorePackage_TypeExists_InConjectureEFCoreNamespace()
    {
        System.Type type = typeof(EFCorePackage);
        Assert.Equal("Conjecture.EFCore", type.Namespace);
    }

    [Fact]
    public void EFCorePackage_Assembly_ReferencesEntityFrameworkCore()
    {
        Assembly assembly = typeof(EFCorePackage).Assembly;
        bool referencesEFCore = System.Linq.Enumerable.Any(
            assembly.GetReferencedAssemblies(),
            static a => a.Name == "Microsoft.EntityFrameworkCore");

        Assert.True(referencesEFCore, "Conjecture.EFCore assembly must reference Microsoft.EntityFrameworkCore");
    }

    [Fact]
    public void EFCorePackage_IsInternalStaticClass()
    {
        System.Type type = typeof(EFCorePackage);
        Assert.True(type.IsAbstract && type.IsSealed, "EFCorePackage should be a static class (abstract + sealed)");
        Assert.False(type.IsPublic, "EFCorePackage should be internal");
    }
}