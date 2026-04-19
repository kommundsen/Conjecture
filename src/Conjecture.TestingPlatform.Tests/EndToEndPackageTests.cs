// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace Conjecture.TestingPlatform.Tests;

[Trait("Category", "Integration")]
public sealed class EndToEndPackageTests : IDisposable
{
    private readonly string tempDirectory;
    private readonly string nupkgDirectory;

    public EndToEndPackageTests()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "Conjecture.E2E." + Guid.NewGuid().ToString("N"));
        nupkgDirectory = Path.Combine(tempDirectory, "packages");
        Directory.CreateDirectory(tempDirectory);
        Directory.CreateDirectory(nupkgDirectory);
    }

    public void Dispose()
    {
        if (!Directory.Exists(tempDirectory))
        {
            return;
        }

        // NuGet extracts packages as read-only on Windows — clear attributes before deleting.
        // Some MSBuild/dotnet server DLLs may remain locked; suppress those errors (best-effort cleanup).
        try
        {
            foreach (string file in Directory.EnumerateFiles(tempDirectory, "*", SearchOption.AllDirectories))
            {
                try
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                }
                catch (UnauthorizedAccessException) { }
            }

            Directory.Delete(tempDirectory, true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private static string FindRepoRoot()
    {
        string current = AppContext.BaseDirectory;
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current, "src", "Conjecture.TestingPlatform", "Conjecture.TestingPlatform.csproj")))
            {
                return current;
            }

            string? parent = Path.GetDirectoryName(current);
            if (parent is null || parent == current)
            {
                break;
            }

            current = parent;
        }

        throw new FileNotFoundException("Could not locate repo root by walking up from AppContext.BaseDirectory.");
    }

    private static string FindProductionProjectPath() =>
        Path.Combine(FindRepoRoot(), "src", "Conjecture.TestingPlatform", "Conjecture.TestingPlatform.csproj");

    private (int ExitCode, string Output, string Error) RunDotnet(string arguments, string workingDirectory)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = "dotnet",
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        startInfo.Environment["NUGET_PACKAGES"] = Path.Combine(tempDirectory, "nuget-cache");

        using Process process = new() { StartInfo = startInfo };
        process.Start();
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        return (process.ExitCode, output, error);
    }

    private string PackProject()
    {
        string repoRoot = FindRepoRoot();

        string corePath = Path.Combine(repoRoot, "src", "Conjecture.Core", "Conjecture.Core.csproj");
        (int coreExit, string coreOut, string coreErr) = RunDotnet(
            $"pack \"{corePath}\" --output \"{nupkgDirectory}\" --configuration Release",
            Path.GetDirectoryName(corePath)!);
        Assert.True(coreExit == 0, $"dotnet pack Conjecture.Core failed (exit {coreExit}).\nOutput: {coreOut}\nError: {coreErr}");

        string projectPath = FindProductionProjectPath();
        (int exitCode, string output, string error) = RunDotnet(
            $"pack \"{projectPath}\" --output \"{nupkgDirectory}\" --configuration Release",
            Path.GetDirectoryName(projectPath)!);
        Assert.True(exitCode == 0, $"dotnet pack failed (exit {exitCode}).\nOutput: {output}\nError: {error}");

        string[] nupkgFiles = Directory.GetFiles(nupkgDirectory, "Conjecture.TestingPlatform.*.nupkg");
        Assert.True(nupkgFiles.Length > 0, $"No Conjecture.TestingPlatform .nupkg produced in {nupkgDirectory}.\nOutput: {output}");

        return nupkgFiles[0];
    }

    [Fact]
    public void Nupkg_ContainsPropsFile()
    {
        string nupkgPath = PackProject();

        using ZipArchive archive = ZipFile.OpenRead(nupkgPath);
        bool found = archive.Entries.Any(static e =>
            string.Equals(e.FullName, "build/Conjecture.TestingPlatform.props", StringComparison.OrdinalIgnoreCase));

        Assert.True(
            found,
            $"Expected 'build/Conjecture.TestingPlatform.props' inside {Path.GetFileName(nupkgPath)}.\nEntries: {string.Join(", ", archive.Entries.Select(static e => e.FullName))}");
    }

    [Fact]
    public void Nupkg_ContainsTargetsFile()
    {
        string nupkgPath = PackProject();

        using ZipArchive archive = ZipFile.OpenRead(nupkgPath);
        bool found = archive.Entries.Any(static e =>
            string.Equals(e.FullName, "build/Conjecture.TestingPlatform.targets", StringComparison.OrdinalIgnoreCase));

        Assert.True(
            found,
            $"Expected 'build/Conjecture.TestingPlatform.targets' inside {Path.GetFileName(nupkgPath)}.\nEntries: {string.Join(", ", archive.Entries.Select(static e => e.FullName))}");
    }
}
