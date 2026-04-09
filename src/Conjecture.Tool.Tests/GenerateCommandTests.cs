// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Text.Json;

using Conjecture.Tool;

namespace Conjecture.Tool.Tests;

public class GenerateCommandTests
{
    private static string TestAssemblyPath => typeof(AssemblyLoaderTests).Assembly.Location;

    // ── JSON output ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Execute_GenerateInts_ProducesValidJsonArray()
    {
        string output = await GenerateCommand.ExecuteAsync(
            assemblyPath: TestAssemblyPath,
            typeName: "Int32",
            count: 10,
            seed: 42UL,
            format: "json");

        using JsonDocument doc = JsonDocument.Parse(output);
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
    }

    [Fact]
    public async Task Execute_GenerateInts_ProducesExactCount()
    {
        string output = await GenerateCommand.ExecuteAsync(
            assemblyPath: TestAssemblyPath,
            typeName: "Int32",
            count: 10,
            seed: 42UL,
            format: "json");

        using JsonDocument doc = JsonDocument.Parse(output);
        Assert.Equal(10, doc.RootElement.GetArrayLength());
    }

    [Fact]
    public async Task Execute_GenerateInts_ArrayElementsAreIntegers()
    {
        string output = await GenerateCommand.ExecuteAsync(
            assemblyPath: TestAssemblyPath,
            typeName: "Int32",
            count: 5,
            seed: 1UL,
            format: "json");

        using JsonDocument doc = JsonDocument.Parse(output);
        foreach (JsonElement element in doc.RootElement.EnumerateArray())
        {
            Assert.Equal(JsonValueKind.Number, element.ValueKind);
            Assert.True(element.TryGetInt32(out _));
        }
    }

    // ── Full name resolution ──────────────────────────────────────────────────

    [Fact]
    public async Task Execute_TypeByFullName_ProducesSameResultAsSimpleName()
    {
        string bySimple = await GenerateCommand.ExecuteAsync(
            assemblyPath: TestAssemblyPath,
            typeName: "Int32",
            count: 5,
            seed: 7UL,
            format: "json");

        string byFull = await GenerateCommand.ExecuteAsync(
            assemblyPath: TestAssemblyPath,
            typeName: "System.Int32",
            count: 5,
            seed: 7UL,
            format: "json");

        Assert.Equal(bySimple, byFull);
    }

    // ── Seed determinism ─────────────────────────────────────────────────────

    [Fact]
    public async Task Execute_SameSeed_ProducesIdenticalOutput()
    {
        string first = await GenerateCommand.ExecuteAsync(
            assemblyPath: TestAssemblyPath,
            typeName: "Int32",
            count: 10,
            seed: 99UL,
            format: "json");

        string second = await GenerateCommand.ExecuteAsync(
            assemblyPath: TestAssemblyPath,
            typeName: "Int32",
            count: 10,
            seed: 99UL,
            format: "json");

        Assert.Equal(first, second);
    }

    [Fact]
    public async Task Execute_DifferentSeeds_ProduceDifferentOutput()
    {
        string first = await GenerateCommand.ExecuteAsync(
            assemblyPath: TestAssemblyPath,
            typeName: "Int32",
            count: 10,
            seed: 1UL,
            format: "json");

        string second = await GenerateCommand.ExecuteAsync(
            assemblyPath: TestAssemblyPath,
            typeName: "Int32",
            count: 10,
            seed: 2UL,
            format: "json");

        Assert.NotEqual(first, second);
    }

    // ── Unknown type ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Execute_UnknownType_ThrowsInvalidOperationException()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            GenerateCommand.ExecuteAsync(
                assemblyPath: TestAssemblyPath,
                typeName: "NoSuchType",
                count: 5,
                seed: 1UL,
                format: "json"));
    }
}