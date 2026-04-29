// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.ComponentModel;
using System.Text;

using ModelContextProtocol.Server;

namespace Conjecture.Mcp.Tools;

[McpServerToolType]
internal static class TestScaffoldingTools
{
    [McpServerTool(Name = "scaffold-property-test")]
    [Description(
        "Generates a Conjecture [Property] test skeleton for a given C# method signature. " +
        "Infers Strategy.* strategies from the parameter types and emits a ready-to-fill " +
        "test class with the correct using directives for the chosen framework.")]
    public static string ScaffoldPropertyTest(
        [Description("The C# method signature to generate a property test for, e.g. 'public static int Add(int a, int b)'")] string methodSignature,
        [Description("Test framework: 'xunit' (default), 'xunit-v3', 'nunit', or 'mstest'")] string framework = "xunit")
    {
        var parameters = ParseParameters(methodSignature);
        var methodName = ParseMethodName(methodSignature);
        return Build(methodName, parameters, framework.ToLowerInvariant());
    }

    internal static List<(string Type, string Name)> ParseParameters(string signature)
    {
        var parenStart = signature.IndexOf('(');
        var parenEnd = signature.LastIndexOf(')');
        if (parenStart < 0 || parenEnd < 0 || parenEnd <= parenStart)
        {
            return [];
        }

        var paramSection = signature[(parenStart + 1)..parenEnd].Trim();
        if (string.IsNullOrWhiteSpace(paramSection))
        {
            return [];
        }

        var results = new List<(string, string)>();
        foreach (var chunk in SplitParams(paramSection))
        {
            var part = chunk.Trim();
            // Attributes like [From<T>] — strip them
            if (part.StartsWith('['))
            {
                var closeAttr = part.IndexOf(']');
                if (closeAttr >= 0)
                {
                    part = part[(closeAttr + 1)..].Trim();
                }
            }

            var lastSpace = part.LastIndexOf(' ');
            if (lastSpace < 0)
            {
                continue;
            }

            var type = part[..lastSpace].Trim();
            var name = part[(lastSpace + 1)..].Trim();
            if (!string.IsNullOrEmpty(type) && !string.IsNullOrEmpty(name))
            {
                results.Add((type, name));
            }
        }

        return results;
    }

    // Split on commas that are not inside <> angle brackets.
    private static IEnumerable<string> SplitParams(string paramSection)
    {
        int depth = 0;
        int start = 0;
        for (int i = 0; i < paramSection.Length; i++)
        {
            switch (paramSection[i])
            {
                case '<': depth++; break;
                case '>': depth--; break;
                case ',' when depth == 0:
                    yield return paramSection[start..i];
                    start = i + 1;
                    break;
            }
        }

        yield return paramSection[start..];
    }

    internal static string ParseMethodName(string signature)
    {
        var parenIdx = signature.IndexOf('(');
        if (parenIdx < 0)
        {
            return "Method";
        }

        var beforeParen = signature[..parenIdx];
        var last = beforeParen.Split(' ', StringSplitOptions.RemoveEmptyEntries).Last();
        return last;
    }

    private static string Build(string methodName, List<(string Type, string Name)> parameters, string framework)
    {
        var sb = new StringBuilder();

        AppendUsings(sb, framework);
        sb.AppendLine();
        sb.AppendLine($"public class {methodName}PropertyTests");
        sb.AppendLine("{");

        var attrLine = framework switch
        {
            "nunit" => "    [Conjecture.NUnit.Property]",
            "mstest" => "    [Conjecture.MSTest.Property]",
            _ => "    [Property]"
        };

        sb.AppendLine(attrLine);

        var paramList = string.Join(", ", parameters.Select(p => $"{p.Type} {p.Name}"));
        sb.AppendLine($"    public void {methodName}_SatisfiesProperty({paramList})");
        sb.AppendLine("    {");
        sb.AppendLine("        // Act");
        sb.AppendLine($"        // var result = {methodName}({string.Join(", ", parameters.Select(p => p.Name))});");
        sb.AppendLine();
        sb.AppendLine("        // Assert");
        sb.AppendLine("        // Add your property assertion here, e.g.:");
        sb.AppendLine("        // Assert.True(result >= 0);");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        if (parameters.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("// Suggested strategies for the parameters above:");
            foreach (var (type, name) in parameters)
            {
                var suggestion = StrategyTools.SuggestForType(type).Split('\n')[0].TrimEnd();
                sb.AppendLine($"// {name} ({type}): {suggestion}");
            }

            sb.AppendLine();
            sb.AppendLine("// If you need to control the strategies, use [From<T>] or [ConjectureSettings]:");
            sb.AppendLine($"// [ConjectureSettings(MaxExamples = 500)]");
            sb.AppendLine($"// [Property]");
            sb.AppendLine($"// public void {methodName}_SatisfiesProperty({paramList}) {{ ... }}");
        }

        return sb.ToString();
    }

    private static void AppendUsings(StringBuilder sb, string framework)
    {
        sb.AppendLine("using Conjecture.Core;");
        switch (framework)
        {
            case "xunit-v3":
                sb.AppendLine("using Conjecture.Xunit.V3;");
                sb.AppendLine("using Xunit;");
                break;
            case "nunit":
                sb.AppendLine("using Conjecture.NUnit;");
                sb.AppendLine("using NUnit.Framework;");
                break;
            case "mstest":
                sb.AppendLine("using Conjecture.MSTest;");
                sb.AppendLine("using Microsoft.VisualStudio.TestTools.UnitTesting;");
                break;
            default:
                sb.AppendLine("using Conjecture.Xunit;");
                sb.AppendLine("using Xunit;");
                break;
        }
    }
}