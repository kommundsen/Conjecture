// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.IO;
using System.Xml.Linq;

namespace Conjecture.TestingPlatform.Tests;

public sealed class BuildAssetsTests
{
    private static string FindBuildDirectory()
    {
        string current = AppContext.BaseDirectory;
        while (current is not null)
        {
            string candidate = Path.Combine(current, "src", "Conjecture.TestingPlatform", "build");
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

        throw new DirectoryNotFoundException("Could not locate 'src/Conjecture.TestingPlatform/build/' by walking up from AppContext.BaseDirectory.");
    }

    [Fact]
    public void PropsFile_Exists()
    {
        string path = Path.Combine(FindBuildDirectory(), "Conjecture.TestingPlatform.props");
        Assert.True(File.Exists(path), $"Expected props file at: {path}");
    }

    [Fact]
    public void TargetsFile_Exists()
    {
        string path = Path.Combine(FindBuildDirectory(), "Conjecture.TestingPlatform.targets");
        Assert.True(File.Exists(path), $"Expected targets file at: {path}");
    }

    [Fact]
    public void PropsFile_IsValidXml()
    {
        string path = Path.Combine(FindBuildDirectory(), "Conjecture.TestingPlatform.props");
        XDocument.Load(path);
    }

    [Fact]
    public void TargetsFile_IsValidXml()
    {
        string path = Path.Combine(FindBuildDirectory(), "Conjecture.TestingPlatform.targets");
        XDocument.Load(path);
    }

    [Fact]
    public void PropsFile_SetsIsTestingPlatformApplicationToTrue()
    {
        string path = Path.Combine(FindBuildDirectory(), "Conjecture.TestingPlatform.props");
        XDocument doc = XDocument.Load(path);
        bool found = false;
        foreach (XElement element in doc.Descendants("IsTestingPlatformApplication"))
        {
            if (string.Equals(element.Value.Trim(), "true", StringComparison.OrdinalIgnoreCase))
            {
                found = true;
                break;
            }
        }

        Assert.True(found, $"Expected <IsTestingPlatformApplication>true</IsTestingPlatformApplication> in {path}");
    }
}
