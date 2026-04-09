// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Reflection;
using System.Text.Json;

using Conjecture.Tool.Plan;

namespace Conjecture.Tool;

internal static class Program
{
    internal static async Task<int> Main(string[] args)
    {
        return await RunAsync(args);
    }

    internal static async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0)
        {
            await Console.Error.WriteLineAsync("Usage: conjecture generate|plan ...");
            return 1;
        }

        return args[0] switch
        {
            "generate" => await RunGenerateAsync(args[1..]),
            "plan" => await RunPlanAsync(args[1..]),
            _ => await WriteErrorAsync($"Unknown command: {args[0]}"),
        };
    }

    private static async Task<int> RunGenerateAsync(string[] args)
    {
        string? assembly = null;
        string? type = null;
        int count = 100;
        ulong? seed = null;
        string format = "json";
        string? output = null;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--assembly":
                    assembly = args[++i];
                    break;
                case "--type":
                    type = args[++i];
                    break;
                case "--count":
                    count = int.Parse(args[++i]);
                    break;
                case "--seed":
                    seed = ulong.Parse(args[++i]);
                    break;
                case "--format":
                    format = args[++i];
                    break;
                case "--output":
                    output = args[++i];
                    break;
            }
        }

        if (assembly is null || type is null)
        {
            return await WriteErrorAsync("--assembly and --type are required");
        }

        if (!File.Exists(assembly))
        {
            return await WriteErrorAsync($"Assembly not found: {assembly}");
        }

        Assembly loadedAssembly;
        try
        {
            loadedAssembly = Assembly.LoadFrom(assembly);
        }
        catch (Exception ex)
        {
            return await WriteErrorAsync($"Failed to load assembly: {ex.Message}");
        }

        IReadOnlyList<Type> providers = AssemblyLoader.FindProvidersInAssembly(loadedAssembly);
        Type? providerType = AssemblyLoader.ResolveByTargetType(loadedAssembly, type);
        if (providerType is null)
        {
            IEnumerable<string> discovered = providers
                .Select(p => AssemblyLoader.GetProviderTargetType(p)?.Name)
                .Where(n => n is not null)
                .Select(n => n!);
            return await WriteErrorAsync(
                $"Type provider not found for: {type}. Discovered types: {string.Join(", ", discovered)}");
        }

        string[] supportedFormats = ["json", "jsonl"];
        if (!supportedFormats.Contains(format, StringComparer.OrdinalIgnoreCase))
        {
            return await WriteErrorAsync(
                $"Unsupported format: {format}. Available formats: {string.Join(", ", supportedFormats)}");
        }

        string result;
        try
        {
            result = await GenerateCommand.ExecuteAsync(assembly, type, count, seed, format);
        }
        catch (Exception ex)
        {
            return await WriteErrorAsync(ex.Message);
        }

        if (output is not null)
        {
            await File.WriteAllTextAsync(output, result);
        }
        else
        {
            Console.Write(result);
        }

        return 0;
    }

    private static async Task<int> RunPlanAsync(string[] args)
    {
        if (args.Length == 0)
        {
            return await WriteErrorAsync("plan requires a plan file path");
        }

        string planFile = args[0];

        if (!File.Exists(planFile))
        {
            return await WriteErrorAsync($"Plan file not found: {planFile}");
        }

        GenerationPlan plan;
        try
        {
            string json = await File.ReadAllTextAsync(planFile);
            plan = JsonSerializer.Deserialize<GenerationPlan>(json)
                ?? throw new InvalidOperationException("Failed to deserialize plan");
        }
        catch (Exception ex)
        {
            return await WriteErrorAsync($"Failed to read plan file: {ex.Message}");
        }

        PlanRunner runner = new();
        try
        {
            runner.Run(plan);
        }
        catch (PlanException ex)
        {
            return await WriteErrorAsync(ex.Message);
        }
        catch (Exception ex)
        {
            return await WriteErrorAsync(ex.Message);
        }

        return 0;
    }

    private static async Task<int> WriteErrorAsync(string message)
    {
        await Console.Error.WriteLineAsync(message);
        return 1;
    }
}
