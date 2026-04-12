// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.IO;

using Conjecture.Interactive;

using Microsoft.DotNet.Interactive;

namespace Conjecture.Interactive.Tests;

public class ConjectureKernelExtensionTests
{
    [Fact]
    public void ConjectureKernelExtension_ImplementsIKernelExtension()
    {
        Assert.True(typeof(IKernelExtension).IsAssignableFrom(typeof(ConjectureKernelExtension)));
    }

    [Fact]
    public void ExtensionDib_ExistsInOutputDirectory()
    {
        string dibPath = Path.Combine(AppContext.BaseDirectory, "extension.dib");
        Assert.True(File.Exists(dibPath), $"extension.dib not found at {dibPath}");
    }

    [Fact]
    public async Task OnLoadAsync_CompletesWithoutThrowing()
    {
        ConjectureKernelExtension extension = new();
        using CompositeKernel kernel = new();

        await extension.OnLoadAsync(kernel);
    }
}
