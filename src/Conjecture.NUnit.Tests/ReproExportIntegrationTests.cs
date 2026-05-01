// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Reflection;

using Conjecture.Core;
using Conjecture.Core.Internal;

using NUnit.Framework;

using ConjecturePropertyAttribute = Conjecture.NUnit.PropertyAttribute;

namespace Conjecture.NUnit.Tests;

[TestFixture]
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

    [Test]
    public void PropertyAttribute_ExportReproductionOnFailure_DefaultIsFalse()
    {
        ConjecturePropertyAttribute attr = new();
        Assert.That(attr.ExportReproductionOnFailure, Is.False);
    }

    [Test]
    public void PropertyAttribute_ReproductionOutputPath_DefaultIsConjecturePath()
    {
        ConjecturePropertyAttribute attr = new();
        Assert.That(attr.ReproductionOutputPath, Is.EqualTo(".conjecture/repros/"));
    }

    [Test]
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

            Assert.That(result.Passed, Is.False);

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
                TestFramework.NUnit,
                DateTimeOffset.UtcNow);

            ReproFileBuilder.WriteToFile(context, settings.ReproductionOutputPath);
            string[] files = Directory.GetFiles(tempDir, "*.cs");
            Assert.That(files, Is.Not.Empty);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}
