// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Reflection;

using Conjecture.Core;
using Conjecture.Core.Internal;

using Xunit;

namespace Conjecture.Xunit.V3.Tests;

/// <summary>
/// Integration tests verifying the xUnit v3 adapter exposes the repro-export
/// surface and that the underlying ReproFileBuilder + TestRunner pipeline writes
/// a .cs file when the property fails and ExportReproductionOnFailure is true.
/// </summary>
public class ReproExportIntegrationTests
{
#pragma warning disable IDE0060
    private static void IntProperty(int x) { }
#pragma warning restore IDE0060

    private static ParameterInfo[] Params(string methodName)
    {
        return typeof(ReproExportIntegrationTests)
            .GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static)!
            .GetParameters();
    }

    [Fact]
    public void PropertyAttribute_ExportReproductionOnFailure_DefaultIsFalse()
    {
        PropertyAttribute attr = new();
        Assert.False(attr.ExportReproductionOnFailure);
    }

    [Fact]
    public void PropertyAttribute_ReproductionOutputPath_DefaultIsConjecturePath()
    {
        PropertyAttribute attr = new();
        Assert.Equal(".conjecture/repros/", attr.ReproductionOutputPath);
    }

    [Fact]
    public async Task ExportReproductionOnFailureTrue_FailingProperty_WritesCsFile()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        try
        {
            ConjectureSettings settings = new()
            {
                MaxExamples = 50,
                Seed = 1UL,
                ExportReproductionOnFailure = true,
                ReproductionOutputPath = tempDir,
            };
            ParameterInfo[] parameters = Params(nameof(IntProperty));

            TestRunResult result = await TestRunner.Run(settings, data =>
            {
                object[] args = SharedParameterStrategyResolver.Resolve(parameters, data);
                if ((int)args[0] > 5) { throw new Exception("fail"); }
            });

            Assert.False(result.Passed);

            ConjectureData replay = ConjectureData.ForRecord(result.Counterexample!);
            object[] shrunkArgs = SharedParameterStrategyResolver.Resolve(parameters, replay);
            IEnumerable<(string Name, object? Value, Type Type)> paramTuples =
                parameters.Select((p, i) => (p.Name!, (object?)shrunkArgs[i], p.ParameterType));
            ReproContext context = new(
                nameof(ReproExportIntegrationTests),
                nameof(IntProperty),
                false,
                paramTuples,
                result.Seed!.Value,
                result.ExampleCount,
                result.ShrinkCount,
                TestFramework.Xunit,
                DateTimeOffset.UtcNow);

            ReproFileBuilder.WriteToFile(context, settings.ReproductionOutputPath);
            string[] files = Directory.GetFiles(tempDir, "*.cs");
            Assert.NotEmpty(files);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}
