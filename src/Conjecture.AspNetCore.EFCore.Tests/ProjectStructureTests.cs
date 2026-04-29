// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Linq;
using System.Reflection;

using Conjecture.AspNetCore.EFCore;

namespace Conjecture.AspNetCore.EFCore.Tests;

public sealed class ProjectStructureTests
{
    [Fact]
    public void Package_MarkerType_Exists()
    {
        Type markerType = typeof(AspNetCoreEFCorePackage);
        Assert.NotNull(markerType);
    }

    [Fact]
    public void Package_References_Microsoft_AspNetCore_Mvc_Testing()
    {
        AssemblyName[] referenced = typeof(AspNetCoreEFCorePackage).Assembly.GetReferencedAssemblies();
        bool found = referenced.Any(static a => a.Name == "Microsoft.AspNetCore.Mvc.Testing");
        Assert.True(found);
    }

    [Fact]
    public void Package_References_Microsoft_EntityFrameworkCore()
    {
        AssemblyName[] referenced = typeof(AspNetCoreEFCorePackage).Assembly.GetReferencedAssemblies();
        bool found = referenced.Any(static a => a.Name == "Microsoft.EntityFrameworkCore");
        Assert.True(found);
    }
}