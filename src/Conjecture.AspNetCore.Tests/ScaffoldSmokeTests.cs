// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

namespace Conjecture.AspNetCore.Tests;

public class ScaffoldSmokeTests
{
    [Fact]
    public void AssemblyLoads()
    {
        System.Reflection.Assembly assembly = typeof(AssemblyMarker).Assembly;
        Assert.Equal("Conjecture.AspNetCore", assembly.GetName().Name);
    }
}
