// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Generators;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Conjecture.Generators.Tests;

public sealed class GeneratorInfrastructureTests
{
    [Fact]
    public void ArbitraryGenerator_ImplementsIIncrementalGenerator()
    {
        ArbitraryGenerator generator = new();
        Assert.IsAssignableFrom<IIncrementalGenerator>(generator);
    }

    [Fact]
    public void ArbitraryGenerator_EmptyCompilation_ProducesNoGeneratedSource()
    {
        CSharpCompilation compilation = CreateEmptyCompilation();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new ArbitraryGenerator());

        GeneratorDriverRunResult result = driver.RunGenerators(compilation).GetRunResult();

        Assert.Empty(result.GeneratedTrees);
    }

    [Fact]
    public void ArbitraryGenerator_EmptyCompilation_DoesNotThrow()
    {
        CSharpCompilation compilation = CreateEmptyCompilation();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new ArbitraryGenerator());

        Exception? exception = Record.Exception(() => driver.RunGenerators(compilation));

        Assert.Null(exception);
    }

    private static CSharpCompilation CreateEmptyCompilation() =>
        CSharpCompilation.Create(
            assemblyName: "TestAssembly",
            syntaxTrees: [],
            references: [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
}