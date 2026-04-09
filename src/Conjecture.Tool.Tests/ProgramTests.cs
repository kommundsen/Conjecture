// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Text.Json;

namespace Conjecture.Tool.Tests;

public class ProgramTests
{
    private static string TestAssemblyPath => typeof(AssemblyLoaderTests).Assembly.Location;

    // ── generate: basic JSON output ──────────────────────────────────────────

    [Fact]
    public async Task RunAsync_GenerateInts_ReturnsExitCodeZero()
    {
        int exitCode = await Program.RunAsync([
            "generate",
            "--assembly", TestAssemblyPath,
            "--type", "Int32",
            "--count", "10",
            "--seed", "42",
            "--format", "json",
        ]);

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task RunAsync_GenerateInts_WritesValidJsonArrayToStdout()
    {
        using StringWriter writer = new();
        Console.SetOut(writer);

        try
        {
            await Program.RunAsync([
                "generate",
                "--assembly", TestAssemblyPath,
                "--type", "Int32",
                "--count", "10",
                "--seed", "42",
                "--format", "json",
            ]);
        }
        finally
        {
            Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
        }

        string output = writer.ToString().Trim();
        using JsonDocument doc = JsonDocument.Parse(output);
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
        Assert.Equal(10, doc.RootElement.GetArrayLength());
    }

    // ── generate: seed determinism end-to-end ────────────────────────────────

    [Fact]
    public async Task RunAsync_GenerateSameSeed_ProducesIdenticalStdout()
    {
        static string CaptureOutput(string[] args)
        {
            using StringWriter writer = new();
            Console.SetOut(writer);
            try
            {
                Program.RunAsync(args).GetAwaiter().GetResult();
            }
            finally
            {
                Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
            }

            return writer.ToString().Trim();
        }

        string[] sharedArgs =
        [
            "generate",
            "--assembly", TestAssemblyPath,
            "--type", "Int32",
            "--count", "10",
            "--seed", "77",
            "--format", "json",
        ];

        string first = CaptureOutput(sharedArgs);
        string second = CaptureOutput(sharedArgs);

        Assert.Equal(first, second);
    }

    // ── generate: --output flag writes to file ───────────────────────────────

    [Fact]
    public async Task RunAsync_GenerateWithOutputFlag_WritesJsonToFile()
    {
        string outputFile = Path.Combine(Path.GetTempPath(), $"conjecture-test-{Guid.NewGuid()}.json");
        try
        {
            int exitCode = await Program.RunAsync([
                "generate",
                "--assembly", TestAssemblyPath,
                "--type", "Int32",
                "--count", "5",
                "--seed", "1",
                "--format", "json",
                "--output", outputFile,
            ]);

            Assert.Equal(0, exitCode);
            Assert.True(File.Exists(outputFile));

            string content = await File.ReadAllTextAsync(outputFile);
            using JsonDocument doc = JsonDocument.Parse(content);
            Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
            Assert.Equal(5, doc.RootElement.GetArrayLength());
        }
        finally
        {
            if (File.Exists(outputFile))
            {
                File.Delete(outputFile);
            }
        }
    }

    [Fact]
    public async Task RunAsync_GenerateWithOutputFlag_DoesNotWriteToStdout()
    {
        string outputFile = Path.Combine(Path.GetTempPath(), $"conjecture-test-{Guid.NewGuid()}.json");
        using StringWriter writer = new();
        Console.SetOut(writer);
        try
        {
            await Program.RunAsync([
                "generate",
                "--assembly", TestAssemblyPath,
                "--type", "Int32",
                "--count", "5",
                "--seed", "1",
                "--format", "json",
                "--output", outputFile,
            ]);
        }
        finally
        {
            Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
            if (File.Exists(outputFile))
            {
                File.Delete(outputFile);
            }
        }

        string stdout = writer.ToString().Trim();
        Assert.Empty(stdout);
    }

    // ── error: assembly not found ────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_AssemblyNotFound_ReturnsExitCodeOne()
    {
        int exitCode = await Program.RunAsync([
            "generate",
            "--assembly", "/nonexistent/path/assembly.dll",
            "--type", "Int32",
            "--count", "5",
            "--seed", "1",
            "--format", "json",
        ]);

        Assert.Equal(1, exitCode);
    }

    [Fact]
    public async Task RunAsync_AssemblyNotFound_WritesClearErrorToStderr()
    {
        using StringWriter errorWriter = new();
        Console.SetError(errorWriter);
        try
        {
            await Program.RunAsync([
                "generate",
                "--assembly", "/nonexistent/path/assembly.dll",
                "--type", "Int32",
                "--count", "5",
                "--seed", "1",
                "--format", "json",
            ]);
        }
        finally
        {
            Console.SetError(new StreamWriter(Console.OpenStandardError()) { AutoFlush = true });
        }

        string error = errorWriter.ToString();
        Assert.Contains("assembly", error, StringComparison.OrdinalIgnoreCase);
    }

    // ── error: type not found ────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_TypeNotFound_ReturnsExitCodeOne()
    {
        int exitCode = await Program.RunAsync([
            "generate",
            "--assembly", TestAssemblyPath,
            "--type", "NoSuchType",
            "--count", "5",
            "--seed", "1",
            "--format", "json",
        ]);

        Assert.Equal(1, exitCode);
    }

    [Fact]
    public async Task RunAsync_TypeNotFound_ListsDiscoveredTypesInStderr()
    {
        using StringWriter errorWriter = new();
        Console.SetError(errorWriter);
        try
        {
            await Program.RunAsync([
                "generate",
                "--assembly", TestAssemblyPath,
                "--type", "NoSuchType",
                "--count", "5",
                "--seed", "1",
                "--format", "json",
            ]);
        }
        finally
        {
            Console.SetError(new StreamWriter(Console.OpenStandardError()) { AutoFlush = true });
        }

        string error = errorWriter.ToString();
        // Should mention discovered providers so user knows what is available
        Assert.Contains("Int32", error, StringComparison.OrdinalIgnoreCase);
    }

    // ── error: format not supported ──────────────────────────────────────────

    [Fact]
    public async Task RunAsync_UnsupportedFormat_ReturnsExitCodeOne()
    {
        int exitCode = await Program.RunAsync([
            "generate",
            "--assembly", TestAssemblyPath,
            "--type", "Int32",
            "--count", "5",
            "--seed", "1",
            "--format", "csv",
        ]);

        Assert.Equal(1, exitCode);
    }

    [Fact]
    public async Task RunAsync_UnsupportedFormat_ListsAvailableFormatsInStderr()
    {
        using StringWriter errorWriter = new();
        Console.SetError(errorWriter);
        try
        {
            await Program.RunAsync([
                "generate",
                "--assembly", TestAssemblyPath,
                "--type", "Int32",
                "--count", "5",
                "--seed", "1",
                "--format", "csv",
            ]);
        }
        finally
        {
            Console.SetError(new StreamWriter(Console.OpenStandardError()) { AutoFlush = true });
        }

        string error = errorWriter.ToString();
        // Should list at least the known formats
        Assert.Contains("json", error, StringComparison.OrdinalIgnoreCase);
    }

    // ── plan: execute plan file ──────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_PlanCommand_ValidPlanFile_ReturnsExitCodeZero()
    {
        string planJson = $$"""
            {
              "assembly": "{{TestAssemblyPath.Replace("\\", "\\\\")}}",
              "steps": [
                { "name": "ids", "type": "Int32", "count": 5, "seed": 1 }
              ],
              "output": { "format": "json" }
            }
            """;

        string planFile = Path.Combine(Path.GetTempPath(), $"conjecture-plan-{Guid.NewGuid()}.json");
        try
        {
            await File.WriteAllTextAsync(planFile, planJson);

            int exitCode = await Program.RunAsync(["plan", planFile]);

            Assert.Equal(0, exitCode);
        }
        finally
        {
            if (File.Exists(planFile))
            {
                File.Delete(planFile);
            }
        }
    }

    [Fact]
    public async Task RunAsync_PlanCommand_WithStepDependency_ReturnsExitCodeZero()
    {
        string planJson = $$"""
            {
              "assembly": "{{TestAssemblyPath.Replace("\\", "\\\\")}}",
              "steps": [
                { "name": "ids", "type": "Int32", "count": 3, "seed": 10 },
                {
                  "name": "sampled",
                  "type": "Int32",
                  "count": 3,
                  "seed": 20,
                  "bindings": { "Value": { "$ref": "ids[*]" } }
                }
              ],
              "output": { "format": "json" }
            }
            """;

        string planFile = Path.Combine(Path.GetTempPath(), $"conjecture-plan-dep-{Guid.NewGuid()}.json");
        try
        {
            await File.WriteAllTextAsync(planFile, planJson);

            int exitCode = await Program.RunAsync(["plan", planFile]);

            Assert.Equal(0, exitCode);
        }
        finally
        {
            if (File.Exists(planFile))
            {
                File.Delete(planFile);
            }
        }
    }

    [Fact]
    public async Task RunAsync_PlanCommand_PlanFileNotFound_ReturnsExitCodeOne()
    {
        int exitCode = await Program.RunAsync(["plan", "/nonexistent/plan.json"]);

        Assert.Equal(1, exitCode);
    }

    [Fact]
    public async Task RunAsync_PlanCommand_AssemblyNotFound_ReturnsExitCodeOne()
    {
        string planJson = """
            {
              "assembly": "/nonexistent/assembly.dll",
              "steps": [
                { "name": "ids", "type": "Int32", "count": 3, "seed": 1 }
              ],
              "output": { "format": "json" }
            }
            """;

        string planFile = Path.Combine(Path.GetTempPath(), $"conjecture-plan-badasm-{Guid.NewGuid()}.json");
        try
        {
            await File.WriteAllTextAsync(planFile, planJson);

            int exitCode = await Program.RunAsync(["plan", planFile]);

            Assert.Equal(1, exitCode);
        }
        finally
        {
            if (File.Exists(planFile))
            {
                File.Delete(planFile);
            }
        }
    }
}
