using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Conjecture.Generators;

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
