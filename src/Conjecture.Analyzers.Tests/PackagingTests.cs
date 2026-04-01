using System.IO;
using System.Reflection;
using System.Xml.Linq;

namespace Conjecture.Analyzers.Tests;

public sealed class PackagingTests
{
    // test assembly lives at bin/Debug/net10.0; src/ is four levels up
    private static readonly string SrcDir = Path.GetFullPath(
        Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "..", "..", "..", ".."));

    private static string AnalyzersCsproj() =>
        Path.Combine(SrcDir, "Conjecture.Analyzers", "Conjecture.Analyzers.csproj");

    private static string AdapterCsproj(string projectName) =>
        Path.Combine(SrcDir, projectName, $"{projectName}.csproj");

    private static XDocument LoadCsproj(string path)
    {
        Assert.True(File.Exists(path), $"csproj not found: {path}");
        return XDocument.Load(path);
    }

    private static string? GetProperty(XDocument doc, string propertyName)
    {
        return doc.Descendants(propertyName).FirstOrDefault()?.Value;
    }

    [Fact]
    public void AnalyzersCsproj_TargetsNetstandard20()
    {
        XDocument doc = LoadCsproj(AnalyzersCsproj());

        string? tfm = GetProperty(doc, "TargetFramework");

        Assert.Equal("netstandard2.0", tfm);
    }

    [Fact]
    public void AnalyzersCsproj_IsRoslynComponentTrue()
    {
        XDocument doc = LoadCsproj(AnalyzersCsproj());

        string? value = GetProperty(doc, "IsRoslynComponent");

        Assert.Equal("true", value, ignoreCase: true);
    }

    [Fact]
    public void AnalyzersCsproj_IncludeBuildOutputFalse()
    {
        XDocument doc = LoadCsproj(AnalyzersCsproj());

        string? value = GetProperty(doc, "IncludeBuildOutput");

        Assert.Equal("false", value, ignoreCase: true);
    }

    [Theory]
    [InlineData("Conjecture.Xunit")]
    [InlineData("Conjecture.Xunit.V3")]
    [InlineData("Conjecture.NUnit")]
    [InlineData("Conjecture.MSTest")]
    public void AdapterProject_DoesNotReferenceAnalyzersWithoutPrivateAssets(string projectName)
    {
        XDocument doc = LoadCsproj(AdapterCsproj(projectName));

        // Any ProjectReference to Conjecture.Analyzers must have PrivateAssets="all"
        IEnumerable<XElement> analyzerRefs = doc
            .Descendants("ProjectReference")
            .Where(e => (string?)e.Attribute("Include") is string inc &&
                        inc.Contains("Conjecture.Analyzers"));

        foreach (XElement refEl in analyzerRefs)
        {
            string? privateAssets = (string?)refEl.Attribute("PrivateAssets")
                ?? refEl.Element("PrivateAssets")?.Value;
            Assert.Equal("all", privateAssets, ignoreCase: true);
        }
    }
}
