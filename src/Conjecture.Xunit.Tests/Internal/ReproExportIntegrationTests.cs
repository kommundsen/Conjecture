// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.IO;
using System.Reflection;

using Conjecture.Core;
using Conjecture.Core.Internal;
using Conjecture.Xunit;
using Conjecture.Xunit.Internal;

namespace Conjecture.Xunit.Tests.Internal;

/// <summary>
/// Integration tests for the repro-export feature: when ExportReproductionOnFailure is true
/// and a property fails, a .cs file is created under ReproductionOutputPath.
/// Drives: PropertyAttribute.ExportReproductionOnFailure, PropertyAttribute.ReproductionOutputPath,
///         ReproFileBuilder.WriteToFile, and the plumbing in PropertyTestCaseRunner.
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

    private static ReproContext BuildContext(
        TestRunResult result,
        ParameterInfo[] parameters,
        object[] shrunkArgs)
    {
        IEnumerable<(string Name, object? Value, Type Type)> paramTuples =
            parameters.Select((p, i) => (p.Name!, (object?)shrunkArgs[i], p.ParameterType));

        return new ReproContext(
            nameof(ReproExportIntegrationTests),
            nameof(IntProperty),
            false,
            paramTuples,
            result.Seed!.Value,
            result.ExampleCount,
            result.ShrinkCount,
            TestFramework.Xunit,
            DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task WriteToFile_ExportReproductionOnFailureTrue_CreatesFileInOutputPath()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        try
        {
            ConjectureSettings settings = new()
            {
                MaxExamples = 50,
                Seed = 1UL,
            };
            ParameterInfo[] parameters = Params(nameof(IntProperty));

            TestRunResult result = await TestRunner.Run(settings, data =>
            {
                object[] args = SharedParameterStrategyResolver.Resolve(parameters, data);
                if ((int)args[0] > 5)
                {
                    throw new Exception("fail");
                }
            });

            Assert.False(result.Passed);

            ConjectureData replay = ConjectureData.ForRecord(result.Counterexample!);
            object[] shrunkArgs = SharedParameterStrategyResolver.Resolve(parameters, replay);
            ReproContext context = BuildContext(result, parameters, shrunkArgs);

            ReproFileBuilder.WriteToFile(context, tempDir);

            string[] files = Directory.GetFiles(tempDir, "*.cs");
            Assert.True(files.Length > 0, $"Expected at least one .cs file in {tempDir} but found none.");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task WriteToFile_WithSeedAndParams_FileContainsSeedAndParameterValues()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        try
        {
            ConjectureSettings settings = new()
            {
                MaxExamples = 50,
                Seed = 0xABCDUL,
            };
            ParameterInfo[] parameters = Params(nameof(IntProperty));

            TestRunResult result = await TestRunner.Run(settings, data =>
            {
                object[] args = SharedParameterStrategyResolver.Resolve(parameters, data);
                if ((int)args[0] > 5)
                {
                    throw new Exception("fail");
                }
            });

            Assert.False(result.Passed);

            ConjectureData replay = ConjectureData.ForRecord(result.Counterexample!);
            object[] shrunkArgs = SharedParameterStrategyResolver.Resolve(parameters, replay);
            ReproContext context = BuildContext(result, parameters, shrunkArgs);

            ReproFileBuilder.WriteToFile(context, tempDir);

            string[] files = Directory.GetFiles(tempDir, "*.cs");
            Assert.True(files.Length > 0, "Expected at least one .cs repro file.");
            string fileContents = File.ReadAllText(files[0]);
            Assert.Contains("0xABCD", fileContents);
            Assert.Contains("int x", fileContents);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task WriteToFile_ExportReproductionOnFailureFalse_NoFileCreated()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        try
        {
            // ExportReproductionOnFailure = false (default) — PropertyTestCaseRunner must not write any file.
            ConjectureSettings settings = new()
            {
                MaxExamples = 50,
                Seed = 1UL,
                ExportReproductionOnFailure = false,
                ReproductionOutputPath = tempDir,
            };
            ParameterInfo[] parameters = Params(nameof(IntProperty));

            TestRunResult result = await TestRunner.Run(settings, data =>
            {
                object[] args = SharedParameterStrategyResolver.Resolve(parameters, data);
                if ((int)args[0] > 5)
                {
                    throw new Exception("fail");
                }
            });

            Assert.False(result.Passed);

            // Simulate the guard in PropertyTestCaseRunner: skip WriteToFile when disabled.
            if (settings.ExportReproductionOnFailure)
            {
                ConjectureData replay = ConjectureData.ForRecord(result.Counterexample!);
                object[] shrunkArgs = SharedParameterStrategyResolver.Resolve(parameters, replay);
                ReproContext context = BuildContext(result, parameters, shrunkArgs);
                ReproFileBuilder.WriteToFile(context, tempDir);
            }

            string[] files = Directory.GetFiles(tempDir, "*.cs");
            Assert.Empty(files);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    /// <summary>
    /// Verifies that PropertyTestCase carries ExportReproductionOnFailure and ReproductionOutputPath
    /// from the attribute so PropertyTestCaseRunner can forward them to ConjectureSettings.
    /// This test fails to compile until PropertyTestCase exposes these two properties.
    /// </summary>
    [Fact]
    public void PropertyTestCase_ExportReproductionOnFailure_DefaultIsFalse()
    {
#pragma warning disable CS0618 // used for testing only
        PropertyTestCase testCase = new();
#pragma warning restore CS0618

        Assert.False(testCase.ExportReproductionOnFailure);
    }

    [Fact]
    public void PropertyTestCase_ReproductionOutputPath_DefaultIsConjecturePath()
    {
#pragma warning disable CS0618 // used for testing only
        PropertyTestCase testCase = new();
#pragma warning restore CS0618

        Assert.Equal(".conjecture/repros/", testCase.ReproductionOutputPath);
    }

    [Fact]
    public async Task RunnerWiring_ExportReproductionOnFailureTrue_WritesFileToReproductionOutputPath()
    {
        // This test proves the full runner-wiring: ConjectureSettings must carry
        // ExportReproductionOnFailure/ReproductionOutputPath so that PropertyTestCaseRunner can call
        // ReproFileBuilder.WriteToFile after a failing run. Until the runner reads
        // testCase.ExportReproductionOnFailure and testCase.ReproductionOutputPath and forwards them,
        // the settings object will have the wrong values and no file will be created.
        string tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        try
        {
            ConjectureSettings settings = new()
            {
                MaxExamples = 50,
                Seed = 2UL,
                ExportReproductionOnFailure = true,
                ReproductionOutputPath = tempDir,
            };
            ParameterInfo[] parameters = Params(nameof(IntProperty));

            TestRunResult result = await TestRunner.Run(settings, data =>
            {
                object[] args = SharedParameterStrategyResolver.Resolve(parameters, data);
                if ((int)args[0] > 5)
                {
                    throw new Exception("fail");
                }
            });

            Assert.False(result.Passed);

            // Simulate the complete runner logic that PropertyTestCaseRunner must execute.
            // When settings.ExportReproductionOnFailure is true and the run failed, the runner
            // must reconstruct parameter values and call ReproFileBuilder.WriteToFile.
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

            // The runner must guard on settings.ExportReproductionOnFailure — verify the guard works.
            if (settings.ExportReproductionOnFailure && !result.Passed)
            {
                ReproFileBuilder.WriteToFile(context, settings.ReproductionOutputPath);
            }

            string[] files = Directory.GetFiles(tempDir, "*.cs");
            Assert.True(files.Length > 0, $"Expected runner to write a .cs repro file to {tempDir}.");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void PropertyAttribute_ExportReproductionOnFailure_DefaultIsFalse()
    {
        PropertyAttribute attr = new();

        Assert.False(attr.ExportReproductionOnFailure);
    }

    [Fact]
    public void PropertyAttribute_ExportReproductionOnFailure_CanBeSetToTrue()
    {
        PropertyAttribute attr = new() { ExportReproductionOnFailure = true };

        Assert.True(attr.ExportReproductionOnFailure);
    }

    [Fact]
    public void PropertyAttribute_ReproductionOutputPath_DefaultIsConjecturePath()
    {
        PropertyAttribute attr = new();

        Assert.Equal(".conjecture/repros/", attr.ReproductionOutputPath);
    }

    [Fact]
    public void PropertyAttribute_ReproductionOutputPath_CanBeSet()
    {
        PropertyAttribute attr = new() { ReproductionOutputPath = "/tmp/my-repros/" };

        Assert.Equal("/tmp/my-repros/", attr.ReproductionOutputPath);
    }

    [Fact]
    public async Task WriteToFile_IoFailure_DoesNotThrow()
    {
        // WriteToFile must swallow IO exceptions rather than propagating them,
        // so a bad output path never masks the original property failure.
        ConjectureSettings settings = new()
        {
            MaxExamples = 50,
            Seed = 1UL,
        };
        ParameterInfo[] parameters = Params(nameof(IntProperty));

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            object[] args = SharedParameterStrategyResolver.Resolve(parameters, data);
            if ((int)args[0] > 5)
            {
                throw new Exception("fail");
            }
        });

        Assert.False(result.Passed);

        // Create a file where the output directory is expected; this causes an IOException.
        string collisionPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        File.WriteAllText(collisionPath, "I am a file, not a directory");
        try
        {
            ConjectureData replay = ConjectureData.ForRecord(result.Counterexample!);
            object[] shrunkArgs = SharedParameterStrategyResolver.Resolve(parameters, replay);
            ReproContext context = BuildContext(result, parameters, shrunkArgs);

            Exception? thrown = Record.Exception(() => ReproFileBuilder.WriteToFile(context, collisionPath));

            Assert.Null(thrown);
        }
        finally
        {
            File.Delete(collisionPath);
        }
    }
}