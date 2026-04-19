// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.IO;
using System.Xml.Linq;

namespace Conjecture.TestingPlatform.Tests;

public sealed class PackMetadataTests
{
    private static string FindSrcDirectory()
    {
        string current = AppContext.BaseDirectory;
        while (current is not null)
        {
            string candidate = Path.Combine(current, "src");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            string? parent = Path.GetDirectoryName(current);
            if (parent is null || parent == current)
            {
                break;
            }

            current = parent;
        }

        throw new DirectoryNotFoundException("Could not locate 'src/' directory by walking up from AppContext.BaseDirectory.");
    }

    private static string ProductionProjectPath()
    {
        string src = FindSrcDirectory();
        return Path.Combine(src, "Conjecture.TestingPlatform");
    }

    [Fact]
    public void ReadmeMd_Exists()
    {
        string readmePath = Path.Combine(ProductionProjectPath(), "README.md");
        Assert.True(File.Exists(readmePath), $"Expected README.md at: {readmePath}");
    }

    [Fact]
    public void Csproj_HasIsPackableTrue()
    {
        string projectDir = ProductionProjectPath();
        string csprojPath = Path.Combine(projectDir, "Conjecture.TestingPlatform.csproj");
        Assert.True(File.Exists(csprojPath), $"Expected csproj at: {csprojPath}");

        XDocument doc = XDocument.Load(csprojPath);
        bool found = false;
        foreach (XElement element in doc.Descendants("IsPackable"))
        {
            if (string.Equals(element.Value.Trim(), "true", StringComparison.OrdinalIgnoreCase))
            {
                found = true;
                break;
            }
        }

        Assert.True(found, $"Expected <IsPackable>true</IsPackable> in {csprojPath}");
    }
}