// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Text.Json;

using Conjecture.Tool.Plan;

namespace Conjecture.Tool.Tests.Plan;


public class GenerationPlanTests
{
    // ── Deserialization ───────────────────────────────────────────────────────

    [Fact]
    public void Deserialize_MinimalPlan_ReturnsAssemblyPath()
    {
        string json = """
            {
              "assembly": "path/to/MyApp.dll",
              "steps": [
                { "name": "customers", "type": "MyApp.Customer", "count": 50, "seed": 42 }
              ],
              "output": { "format": "json", "file": "seed-data.json" }
            }
            """;

        GenerationPlan plan = JsonSerializer.Deserialize<GenerationPlan>(json)!;

        Assert.Equal("path/to/MyApp.dll", plan.Assembly);
    }

    [Fact]
    public void Deserialize_MinimalPlan_ReturnsOutputFormat()
    {
        string json = """
            {
              "assembly": "path/to/MyApp.dll",
              "steps": [
                { "name": "customers", "type": "MyApp.Customer", "count": 50, "seed": 42 }
              ],
              "output": { "format": "json", "file": "seed-data.json" }
            }
            """;

        GenerationPlan plan = JsonSerializer.Deserialize<GenerationPlan>(json)!;

        Assert.Equal("json", plan.Output.Format);
    }

    [Fact]
    public void Deserialize_MinimalPlan_ReturnsOutputFile()
    {
        string json = """
            {
              "assembly": "path/to/MyApp.dll",
              "steps": [
                { "name": "customers", "type": "MyApp.Customer", "count": 50, "seed": 42 }
              ],
              "output": { "format": "json", "file": "seed-data.json" }
            }
            """;

        GenerationPlan plan = JsonSerializer.Deserialize<GenerationPlan>(json)!;

        Assert.Equal("seed-data.json", plan.Output.File);
    }

    [Fact]
    public void Deserialize_SingleStep_ParsesStepName()
    {
        string json = """
            {
              "assembly": "MyApp.dll",
              "steps": [
                { "name": "customers", "type": "MyApp.Customer", "count": 50, "seed": 42 }
              ],
              "output": { "format": "json", "file": "out.json" }
            }
            """;

        GenerationPlan plan = JsonSerializer.Deserialize<GenerationPlan>(json)!;

        Assert.Equal("customers", plan.Steps[0].Name);
    }

    [Fact]
    public void Deserialize_SingleStep_ParsesStepType()
    {
        string json = """
            {
              "assembly": "MyApp.dll",
              "steps": [
                { "name": "customers", "type": "MyApp.Customer", "count": 50, "seed": 42 }
              ],
              "output": { "format": "json", "file": "out.json" }
            }
            """;

        GenerationPlan plan = JsonSerializer.Deserialize<GenerationPlan>(json)!;

        Assert.Equal("MyApp.Customer", plan.Steps[0].Type);
    }

    [Fact]
    public void Deserialize_SingleStep_ParsesCount()
    {
        string json = """
            {
              "assembly": "MyApp.dll",
              "steps": [
                { "name": "customers", "type": "MyApp.Customer", "count": 50, "seed": 42 }
              ],
              "output": { "format": "json", "file": "out.json" }
            }
            """;

        GenerationPlan plan = JsonSerializer.Deserialize<GenerationPlan>(json)!;

        Assert.Equal(50, plan.Steps[0].Count);
    }

    [Fact]
    public void Deserialize_SingleStep_ParsesSeed()
    {
        string json = """
            {
              "assembly": "MyApp.dll",
              "steps": [
                { "name": "customers", "type": "MyApp.Customer", "count": 50, "seed": 42 }
              ],
              "output": { "format": "json", "file": "out.json" }
            }
            """;

        GenerationPlan plan = JsonSerializer.Deserialize<GenerationPlan>(json)!;

        Assert.Equal(42UL, plan.Steps[0].Seed);
    }

    [Fact]
    public void Deserialize_TwoSteps_ReturnsBothSteps()
    {
        string json = """
            {
              "assembly": "MyApp.dll",
              "steps": [
                { "name": "customers", "type": "MyApp.Customer", "count": 10 },
                { "name": "orders", "type": "MyApp.Order", "count": 20 }
              ],
              "output": { "format": "json", "file": "out.json" }
            }
            """;

        GenerationPlan plan = JsonSerializer.Deserialize<GenerationPlan>(json)!;

        Assert.Equal(2, plan.Steps.Count);
    }

    [Fact]
    public void Deserialize_StepWithBindings_ParsesRefExpression()
    {
        string json = """
            {
              "assembly": "MyApp.dll",
              "steps": [
                { "name": "customers", "type": "MyApp.Customer", "count": 5 },
                {
                  "name": "orders",
                  "type": "MyApp.Order",
                  "count": 10,
                  "bindings": {
                    "CustomerId": { "$ref": "customers[*].Id" }
                  }
                }
              ],
              "output": { "format": "json", "file": "out.json" }
            }
            """;

        GenerationPlan plan = JsonSerializer.Deserialize<GenerationPlan>(json)!;

        string? refExpression = plan.Steps[1].Bindings?["CustomerId"].Ref;
        Assert.Equal("customers[*].Id", refExpression);
    }

    [Fact]
    public void Deserialize_StepWithoutSeed_SeedIsNull()
    {
        string json = """
            {
              "assembly": "MyApp.dll",
              "steps": [
                { "name": "customers", "type": "MyApp.Customer", "count": 5 }
              ],
              "output": { "format": "json", "file": "out.json" }
            }
            """;

        GenerationPlan plan = JsonSerializer.Deserialize<GenerationPlan>(json)!;

        Assert.Null(plan.Steps[0].Seed);
    }

    [Fact]
    public void Deserialize_StepWithoutBindings_BindingsIsNullOrEmpty()
    {
        string json = """
            {
              "assembly": "MyApp.dll",
              "steps": [
                { "name": "customers", "type": "MyApp.Customer", "count": 5 }
              ],
              "output": { "format": "json", "file": "out.json" }
            }
            """;

        GenerationPlan plan = JsonSerializer.Deserialize<GenerationPlan>(json)!;

#pragma warning disable CS8602
        Assert.True(plan.Steps[0].Bindings is null || plan.Steps[0].Bindings.Count == 0);
#pragma warning restore CS8602
    }
}

public class RefResolverTests
{
    // ── Scalar property resolution ─────────────────────────────────────────────

    [Fact]
    public void Resolve_ScalarProperty_ReturnsValue()
    {
        JsonDocument doc = JsonDocument.Parse("""[{"Id": 1}, {"Id": 2}, {"Id": 3}]""");
        IReadOnlyList<JsonElement> results = new[] { doc.RootElement };
        IDictionary<string, IReadOnlyList<JsonElement>> priorResults = new Dictionary<string, IReadOnlyList<JsonElement>>
        {
            ["customers"] = results[0].EnumerateArray().Select(e => e).ToList(),
        };

        IReadOnlyList<JsonElement> resolved = RefResolver.Resolve("customers[*].Id", priorResults);

        Assert.Equal(3, resolved.Count);
    }

    [Fact]
    public void Resolve_ArrayExpansion_ExpandsAllElements()
    {
        JsonDocument doc = JsonDocument.Parse("""[{"Id": 10}, {"Id": 20}]""");
        IDictionary<string, IReadOnlyList<JsonElement>> priorResults = new Dictionary<string, IReadOnlyList<JsonElement>>
        {
            ["customers"] = doc.RootElement.EnumerateArray().ToList(),
        };

        IReadOnlyList<JsonElement> resolved = RefResolver.Resolve("customers[*].Id", priorResults);

        Assert.Equal(2, resolved.Count);
    }

    [Fact]
    public void Resolve_ArrayExpansion_PreservesValues()
    {
        JsonDocument doc = JsonDocument.Parse("""[{"Id": 10}, {"Id": 20}]""");
        IDictionary<string, IReadOnlyList<JsonElement>> priorResults = new Dictionary<string, IReadOnlyList<JsonElement>>
        {
            ["customers"] = doc.RootElement.EnumerateArray().ToList(),
        };

        IReadOnlyList<JsonElement> resolved = RefResolver.Resolve("customers[*].Id", priorResults);

        Assert.Equal(10, resolved[0].GetInt32());
        Assert.Equal(20, resolved[1].GetInt32());
    }

    [Fact]
    public void Resolve_ScalarRef_NoArrayExpansion_ReturnsSingleValue()
    {
        JsonDocument doc = JsonDocument.Parse("""{"Name": "Alice"}""");
        IDictionary<string, IReadOnlyList<JsonElement>> priorResults = new Dictionary<string, IReadOnlyList<JsonElement>>
        {
            ["config"] = new[] { doc.RootElement },
        };

        IReadOnlyList<JsonElement> resolved = RefResolver.Resolve("config.Name", priorResults);

#pragma warning disable xUnit2013
        Assert.Equal(1, resolved.Count);
#pragma warning restore xUnit2013
        Assert.Equal("Alice", resolved[0].GetString());
    }

    // ── Missing $ref target ────────────────────────────────────────────────────

    [Fact]
    public void Resolve_MissingStepName_ThrowsWithClearMessage()
    {
        IDictionary<string, IReadOnlyList<JsonElement>> priorResults = new Dictionary<string, IReadOnlyList<JsonElement>>();

        Exception ex = Assert.Throws<InvalidOperationException>(() =>
            RefResolver.Resolve("nonexistent[*].Id", priorResults));

        Assert.Contains("nonexistent", ex.Message);
    }

    [Fact]
    public void Resolve_MissingProperty_ThrowsWithClearMessage()
    {
        JsonDocument doc = JsonDocument.Parse("""[{"Id": 1}]""");
        IDictionary<string, IReadOnlyList<JsonElement>> priorResults = new Dictionary<string, IReadOnlyList<JsonElement>>
        {
            ["customers"] = doc.RootElement.EnumerateArray().ToList(),
        };

        Exception ex = Assert.Throws<InvalidOperationException>(() =>
            RefResolver.Resolve("customers[*].NonExistentProp", priorResults));

        Assert.Contains("NonExistentProp", ex.Message);
    }

    // ── Edge cases ─────────────────────────────────────────────────────────────

    [Fact]
    public void Resolve_EmptyArray_ReturnsEmptyList()
    {
        JsonDocument doc = JsonDocument.Parse("[]");
        IDictionary<string, IReadOnlyList<JsonElement>> priorResults = new Dictionary<string, IReadOnlyList<JsonElement>>
        {
            ["customers"] = doc.RootElement.EnumerateArray().ToList(),
        };

        IReadOnlyList<JsonElement> resolved = RefResolver.Resolve("customers[*].Id", priorResults);

        Assert.Empty(resolved);
    }

    [Fact]
    public void Resolve_UnclosedBracket_ThrowsInvalidOperationException()
    {
        IDictionary<string, IReadOnlyList<JsonElement>> priorResults = new Dictionary<string, IReadOnlyList<JsonElement>>
        {
            ["customers"] = [],
        };

        Exception ex = Assert.Throws<InvalidOperationException>(() =>
            RefResolver.Resolve("customers[", priorResults));

        Assert.Contains("customers[", ex.Message);
    }

    [Fact]
    public void Resolve_PropertyOnNonObject_ThrowsInvalidOperationExceptionWithPropertyName()
    {
        JsonDocument doc = JsonDocument.Parse("""[42]""");
        IDictionary<string, IReadOnlyList<JsonElement>> priorResults = new Dictionary<string, IReadOnlyList<JsonElement>>
        {
            ["items"] = doc.RootElement.EnumerateArray().ToList(),
        };

        Exception ex = Assert.Throws<InvalidOperationException>(() =>
            RefResolver.Resolve("items[*].Value", priorResults));

        Assert.Contains("Value", ex.Message);
    }

    [Fact]
    public void Resolve_MissingScalarProperty_ThrowsInvalidOperationExceptionWithPropertyName()
    {
        JsonDocument doc = JsonDocument.Parse("""{"Name": "Alice"}""");
        IDictionary<string, IReadOnlyList<JsonElement>> priorResults = new Dictionary<string, IReadOnlyList<JsonElement>>
        {
            ["config"] = new[] { doc.RootElement },
        };

        Exception ex = Assert.Throws<InvalidOperationException>(() =>
            RefResolver.Resolve("config.Missing", priorResults));

        Assert.Contains("Missing", ex.Message);
    }
}

public class PlanRunnerTests
{
    // ── Step ordering ─────────────────────────────────────────────────────────

    [Fact]
    public void RunAsync_SingleStep_ProducesNamedResult()
    {
        string json = """
            {
              "assembly": "__TEST_ASSEMBLY__",
              "steps": [
                { "name": "items", "type": "System.Int32", "count": 5, "seed": 1 }
              ],
              "output": { "format": "json", "file": null }
            }
            """.Replace("__TEST_ASSEMBLY__", typeof(PlanRunnerTests).Assembly.Location.Replace("\\", "/"));

        GenerationPlan plan = JsonSerializer.Deserialize<GenerationPlan>(json)!;
        PlanRunner runner = new();

        PlanResult result = runner.Run(plan);

        Assert.True(result.StepResults.ContainsKey("items"));
    }

    [Fact]
    public void RunAsync_SingleStep_ProducesCorrectCount()
    {
        string json = """
            {
              "assembly": "__TEST_ASSEMBLY__",
              "steps": [
                { "name": "items", "type": "System.Int32", "count": 5, "seed": 1 }
              ],
              "output": { "format": "json", "file": null }
            }
            """.Replace("__TEST_ASSEMBLY__", typeof(PlanRunnerTests).Assembly.Location.Replace("\\", "/"));

        GenerationPlan plan = JsonSerializer.Deserialize<GenerationPlan>(json)!;
        PlanRunner runner = new();

        PlanResult result = runner.Run(plan);

        Assert.Equal(5, result.StepResults["items"].Count);
    }

    [Fact]
    public void RunAsync_MultipleSteps_ExecutesInOrder()
    {
        string assemblyPath = typeof(PlanRunnerTests).Assembly.Location.Replace("\\", "/");
        string json = $$"""
            {
              "assembly": "{{assemblyPath}}",
              "steps": [
                { "name": "first", "type": "System.Int32", "count": 3, "seed": 1 },
                { "name": "second", "type": "System.Int32", "count": 3, "seed": 2 }
              ],
              "output": { "format": "json", "file": null }
            }
            """;

        GenerationPlan plan = JsonSerializer.Deserialize<GenerationPlan>(json)!;
        PlanRunner runner = new();

        PlanResult result = runner.Run(plan);

        Assert.True(result.StepResults.ContainsKey("first"));
        Assert.True(result.StepResults.ContainsKey("second"));
    }

    [Fact]
    public void RunAsync_LaterStepReferencesEarlierResult_ResolvesBinding()
    {
        string assemblyPath = typeof(PlanRunnerTests).Assembly.Location.Replace("\\", "/");
        string json = $$"""
            {
              "assembly": "{{assemblyPath}}",
              "steps": [
                { "name": "source", "type": "System.Int32", "count": 4, "seed": 7 },
                {
                  "name": "consumer",
                  "type": "System.Int32",
                  "count": 4,
                  "seed": 8,
                  "bindings": {
                    "Value": { "$ref": "source[*]" }
                  }
                }
              ],
              "output": { "format": "json", "file": null }
            }
            """;

        GenerationPlan plan = JsonSerializer.Deserialize<GenerationPlan>(json)!;
        PlanRunner runner = new();

        PlanResult result = runner.Run(plan);

        Assert.True(result.StepResults.ContainsKey("consumer"));
    }

    [Fact]
    public void RunAsync_SameSeed_ProducesDeterministicResults()
    {
        string assemblyPath = typeof(PlanRunnerTests).Assembly.Location.Replace("\\", "/");
        string json = $$"""
            {
              "assembly": "{{assemblyPath}}",
              "steps": [
                { "name": "items", "type": "System.Int32", "count": 5, "seed": 42 }
              ],
              "output": { "format": "json", "file": null }
            }
            """;

        GenerationPlan plan = JsonSerializer.Deserialize<GenerationPlan>(json)!;
        PlanRunner runner = new();

        PlanResult first = runner.Run(plan);
        PlanResult second = runner.Run(plan);

        IReadOnlyList<JsonElement> firstItems = first.StepResults["items"];
        IReadOnlyList<JsonElement> secondItems = second.StepResults["items"];
        Assert.Equal(
            firstItems.Select(e => e.GetRawText()),
            secondItems.Select(e => e.GetRawText()));
    }

    // ── Missing $ref target → clear error, exit 1 ────────────────────────────

    [Fact]
    public void RunAsync_RefToNonExistentStep_ThrowsPlanException()
    {
        string assemblyPath = typeof(PlanRunnerTests).Assembly.Location.Replace("\\", "/");
        string json = $$"""
            {
              "assembly": "{{assemblyPath}}",
              "steps": [
                {
                  "name": "orders",
                  "type": "System.Int32",
                  "count": 5,
                  "bindings": {
                    "CustomerId": { "$ref": "customers[*].Id" }
                  }
                }
              ],
              "output": { "format": "json", "file": null }
            }
            """;

        GenerationPlan plan = JsonSerializer.Deserialize<GenerationPlan>(json)!;
        PlanRunner runner = new();

        PlanException ex = Assert.Throws<PlanException>(() => runner.Run(plan));

        Assert.Contains("customers", ex.Message);
    }

    [Fact]
    public void RunAsync_UnsupportedType_ThrowsNotImplementedException()
    {
        string assemblyPath = typeof(PlanRunnerTests).Assembly.Location.Replace("\\", "/");
        string json = $$"""
            {
              "assembly": "{{assemblyPath}}",
              "steps": [
                { "name": "items", "type": "System.Guid", "count": 3, "seed": 1 }
              ],
              "output": { "format": "json", "file": null }
            }
            """;

        GenerationPlan plan = JsonSerializer.Deserialize<GenerationPlan>(json)!;
        PlanRunner runner = new();

        NotImplementedException ex = Assert.Throws<NotImplementedException>(() => runner.Run(plan));

        Assert.Contains("System.Guid", ex.Message);
    }

    [Fact]
    public void RunAsync_RefToNonExistentStep_ExitCodeIsOne()
    {
        string assemblyPath = typeof(PlanRunnerTests).Assembly.Location.Replace("\\", "/");
        string json = $$"""
            {
              "assembly": "{{assemblyPath}}",
              "steps": [
                {
                  "name": "orders",
                  "type": "System.Int32",
                  "count": 5,
                  "bindings": {
                    "CustomerId": { "$ref": "missing[*].Id" }
                  }
                }
              ],
              "output": { "format": "json", "file": null }
            }
            """;

        GenerationPlan plan = JsonSerializer.Deserialize<GenerationPlan>(json)!;
        PlanRunner runner = new();

        PlanException ex = Assert.Throws<PlanException>(() => runner.Run(plan));

        Assert.Equal(1, ex.ExitCode);
    }
}

public class PlanRunnerAdvancedTests
{
    private static string AssemblyPath => typeof(PlanRunnerAdvancedTests).Assembly.Location.Replace("\\", "/");

    // ── Zero-count steps ──────────────────────────────────────────────────────

    [Fact]
    public void RunAsync_ZeroCountStep_ProducesEmptyResultList()
    {
        string json = $$"""
            {
              "assembly": "{{AssemblyPath}}",
              "steps": [
                { "name": "items", "type": "System.Int32", "count": 0, "seed": 1 }
              ],
              "output": { "format": "json", "file": null }
            }
            """;

        GenerationPlan plan = JsonSerializer.Deserialize<GenerationPlan>(json)!;
        PlanRunner runner = new();

        PlanResult result = runner.Run(plan);

        Assert.Empty(result.StepResults["items"]);
    }

    // ── Invalid assembly path ─────────────────────────────────────────────────

    [Fact]
    public void RunAsync_NonExistentAssemblyPath_ThrowsPlanException()
    {
        string json = """
            {
              "assembly": "/nonexistent/path/to/Missing.dll",
              "steps": [
                { "name": "items", "type": "System.Int32", "count": 1, "seed": 1 }
              ],
              "output": { "format": "json", "file": null }
            }
            """;

        GenerationPlan plan = JsonSerializer.Deserialize<GenerationPlan>(json)!;
        PlanRunner runner = new();

        PlanException ex = Assert.Throws<PlanException>(() => runner.Run(plan));

        Assert.Contains("Missing.dll", ex.Message);
    }

    [Fact]
    public void RunAsync_NonExistentAssemblyPath_ExitCodeIsOne()
    {
        string json = """
            {
              "assembly": "/nonexistent/path/to/Missing.dll",
              "steps": [
                { "name": "items", "type": "System.Int32", "count": 1, "seed": 1 }
              ],
              "output": { "format": "json", "file": null }
            }
            """;

        GenerationPlan plan = JsonSerializer.Deserialize<GenerationPlan>(json)!;
        PlanRunner runner = new();

        PlanException ex = Assert.Throws<PlanException>(() => runner.Run(plan));

        Assert.Equal(1, ex.ExitCode);
    }

    // ── Type mismatch in binding resolution ───────────────────────────────────

    [Fact]
    public void RunAsync_BindingResolvesToStringElement_GetInt32Fails_ThrowsPlanExceptionNotRawJsonException()
    {
        // When a ref resolves to string-typed JSON values and the generator calls GetInt32(),
        // PlanRunner must catch the resulting InvalidOperationException and surface it as a
        // PlanException with a message that mentions the type mismatch — not an unhandled
        // raw exception.
        //
        // We create a two-step plan:
        // 1. First step: System.String type that produces strings
        // 2. Second step: System.Int32 type that binds to the string output
        //
        // This causes GetInt32() to be called on string JsonElements, which throws
        // InvalidOperationException. PlanRunner should wrap this as PlanException.
        string json = $$"""
            {
              "assembly": "{{AssemblyPath}}",
              "steps": [
                { "name": "source", "type": "System.String", "count": 2, "seed": 1 },
                {
                  "name": "consumer",
                  "type": "System.Int32",
                  "count": 3,
                  "seed": 2,
                  "bindings": {
                    "Value": { "$ref": "source[*]" }
                  }
                }
              ],
              "output": { "format": "json" }
            }
            """;

        GenerationPlan plan = JsonSerializer.Deserialize<GenerationPlan>(json)!;
        PlanRunner runner = new();

        PlanException ex = Assert.Throws<PlanException>(() => runner.Run(plan));

        Assert.Equal(1, ex.ExitCode);
        Assert.Contains("mismatch", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ── Empty array binding ───────────────────────────────────────────────────

    [Fact]
    public void RunAsync_BindingResolvesToEmptyArray_ThrowsPlanException()
    {
        // When a prior step produced 0 results and a later step binds to it via [*],
        // SampledFrom([]) would throw ArgumentException. PlanRunner should wrap this
        // as a PlanException so the caller gets a meaningful exit code.
        string json = $$"""
            {
              "assembly": "{{AssemblyPath}}",
              "steps": [
                { "name": "source", "type": "System.Int32", "count": 0, "seed": 1 },
                {
                  "name": "consumer",
                  "type": "System.Int32",
                  "count": 3,
                  "seed": 2,
                  "bindings": {
                    "Value": { "$ref": "source[*]" }
                  }
                }
              ],
              "output": { "format": "json", "file": null }
            }
            """;

        GenerationPlan plan = JsonSerializer.Deserialize<GenerationPlan>(json)!;
        PlanRunner runner = new();

        PlanException ex = Assert.Throws<PlanException>(() => runner.Run(plan));

        Assert.Equal(1, ex.ExitCode);
    }

    // ── Multiple bindings on same step ────────────────────────────────────────

    [Fact]
    public void RunAsync_StepWithUnrecognizedBindingKey_ThrowsPlanException()
    {
        // When a System.Int32 step declares a binding with a key other than "Value"
        // (e.g. "CustomerId"), PlanRunner must reject it with a PlanException naming the
        // unrecognized key — not silently merge all pools together.
        // Currently PlanRunner ignores the key entirely and treats any binding as "Value",
        // so this test FAILS (no exception is thrown today).

        string json = $$"""
            {
              "assembly": "{{AssemblyPath}}",
              "steps": [
                { "name": "customers", "type": "System.Int32", "count": 3, "seed": 77 },
                {
                  "name": "orders",
                  "type": "System.Int32",
                  "count": 5,
                  "seed": 88,
                  "bindings": {
                    "CustomerId": { "$ref": "customers[*]" }
                  }
                }
              ],
              "output": { "format": "json", "file": null }
            }
            """;

        GenerationPlan plan = JsonSerializer.Deserialize<GenerationPlan>(json)!;
        PlanRunner runner = new();

        PlanException ex = Assert.Throws<PlanException>(() => runner.Run(plan));

        Assert.Equal(1, ex.ExitCode);
        Assert.Contains("CustomerId", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RunAsync_StepWithTwoUnrecognizedDistinctBindingKeys_ThrowsPlanExceptionNamingKey()
    {
        // A System.Int32 step with two named bindings using unrecognized keys ("First", "Second")
        // must throw PlanException (exit 1) and name at least one of the unrecognized keys.
        // Currently PlanRunner merges both pools without checking keys, so this FAILS.

        string json = $$"""
            {
              "assembly": "{{AssemblyPath}}",
              "steps": [
                { "name": "pool1", "type": "System.Int32", "count": 2, "seed": 200 },
                { "name": "pool2", "type": "System.Int32", "count": 2, "seed": 201 },
                {
                  "name": "derived",
                  "type": "System.Int32",
                  "count": 4,
                  "seed": 202,
                  "bindings": {
                    "First":  { "$ref": "pool1[*]" },
                    "Second": { "$ref": "pool2[*]" }
                  }
                }
              ],
              "output": { "format": "json", "file": null }
            }
            """;

        GenerationPlan plan = JsonSerializer.Deserialize<GenerationPlan>(json)!;
        PlanRunner runner = new();

        PlanException ex = Assert.Throws<PlanException>(() => runner.Run(plan));

        Assert.Equal(1, ex.ExitCode);
        bool mentionsAKey =
            ex.Message.Contains("First", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("Second", StringComparison.OrdinalIgnoreCase);
        Assert.True(mentionsAKey, $"Expected message to name an unrecognized key, got: {ex.Message}");
    }

    // ── Step ordering with multiple bindings to earlier steps ─────────────────

    [Fact]
    public void RunAsync_LaterStepReferencesTwoEarlierStepsWithUnrecognizedKeys_ThrowsPlanException()
    {
        // When a step declares multiple named bindings and none of the keys are the
        // recognized "Value" key for System.Int32, PlanRunner must reject them with a
        // PlanException that names the unrecognized keys — not silently merge both pools.
        // Currently PlanRunner merges all binding values into one pool regardless of key,
        // so this test FAILS (no exception is thrown today).

        string json = $$"""
            {
              "assembly": "{{AssemblyPath}}",
              "steps": [
                { "name": "alpha", "type": "System.Int32", "count": 3, "seed": 20 },
                { "name": "beta",  "type": "System.Int32", "count": 3, "seed": 21 },
                {
                  "name": "gamma",
                  "type": "System.Int32",
                  "count": 6,
                  "seed": 22,
                  "bindings": {
                    "AlphaVal": { "$ref": "alpha[*]" },
                    "BetaVal":  { "$ref": "beta[*]" }
                  }
                }
              ],
              "output": { "format": "json", "file": null }
            }
            """;

        GenerationPlan plan = JsonSerializer.Deserialize<GenerationPlan>(json)!;
        PlanRunner runner = new();

        PlanException ex = Assert.Throws<PlanException>(() => runner.Run(plan));

        Assert.Equal(1, ex.ExitCode);
        // Message must name at least one of the unrecognized binding keys.
        bool mentionsAKey =
            ex.Message.Contains("AlphaVal", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("BetaVal", StringComparison.OrdinalIgnoreCase);
        Assert.True(mentionsAKey, $"Expected message to name an unrecognized key, got: {ex.Message}");
    }

    // ── Determinism with bindings ─────────────────────────────────────────────

    [Fact]
    public void RunAsync_SameSeedAndBindings_ProducesDeterministicResults()
    {
        string json = $$"""
            {
              "assembly": "{{AssemblyPath}}",
              "steps": [
                { "name": "source",   "type": "System.Int32", "count": 5, "seed": 99 },
                {
                  "name": "consumer",
                  "type": "System.Int32",
                  "count": 5,
                  "seed": 100,
                  "bindings": {
                    "Value": { "$ref": "source[*]" }
                  }
                }
              ],
              "output": { "format": "json", "file": null }
            }
            """;

        GenerationPlan plan = JsonSerializer.Deserialize<GenerationPlan>(json)!;
        PlanRunner runner = new();

        PlanResult first = runner.Run(plan);
        PlanResult second = runner.Run(plan);

        IReadOnlyList<JsonElement> firstConsumer = first.StepResults["consumer"];
        IReadOnlyList<JsonElement> secondConsumer = second.StepResults["consumer"];

        Assert.Equal(
            firstConsumer.Select(e => e.GetRawText()),
            secondConsumer.Select(e => e.GetRawText()));
    }

    [Fact]
    public void RunAsync_ConsumerWithValueBinding_ProducesValuesFromSourcePool()
    {
        // When a step binds "Value" to a prior step, all generated values must be
        // sampled from the source pool (not generated freely). This is the contract
        // that makes bindings meaningful: the binding constrains the value space.
        // The "Value" key is the one PlanRunner currently recognises for Int32 steps,
        // so this test validates the existing binding path produces in-pool results.

        string json = $$"""
            {
              "assembly": "{{AssemblyPath}}",
              "steps": [
                { "name": "source",   "type": "System.Int32", "count": 3, "seed": 55 },
                {
                  "name": "consumer",
                  "type": "System.Int32",
                  "count": 6,
                  "seed": 56,
                  "bindings": {
                    "Value": { "$ref": "source[*]" }
                  }
                }
              ],
              "output": { "format": "json", "file": null }
            }
            """;

        GenerationPlan plan = JsonSerializer.Deserialize<GenerationPlan>(json)!;
        PlanRunner runner = new();

        PlanResult result = runner.Run(plan);

        HashSet<int> sourcePool = result.StepResults["source"].Select(e => e.GetInt32()).ToHashSet();

        // Every consumer value must be drawn from the source pool.
        Assert.All(result.StepResults["consumer"], e => Assert.Contains(e.GetInt32(), sourcePool));
    }
}

public class RefResolverEdgeCaseTests
{
    // ── Nested property paths ─────────────────────────────────────────────────

    [Fact]
    public void Resolve_NestedPropertyPath_TraversesMultipleLevels()
    {
        JsonDocument doc = JsonDocument.Parse("""
            [
              {"Address": {"City": "Oslo"}},
              {"Address": {"City": "Bergen"}}
            ]
            """);

        IDictionary<string, IReadOnlyList<JsonElement>> priorResults = new Dictionary<string, IReadOnlyList<JsonElement>>
        {
            ["customers"] = doc.RootElement.EnumerateArray().ToList(),
        };

        IReadOnlyList<JsonElement> resolved = RefResolver.Resolve("customers[*].Address.City", priorResults);

        Assert.Equal(2, resolved.Count);
        Assert.Equal("Oslo", resolved[0].GetString());
        Assert.Equal("Bergen", resolved[1].GetString());
    }

    [Fact]
    public void Resolve_NestedPropertyPath_ThreeLevelsDeep_ReturnsLeafValue()
    {
        JsonDocument doc = JsonDocument.Parse("""
            [{"A": {"B": {"C": 42}}}]
            """);

        IDictionary<string, IReadOnlyList<JsonElement>> priorResults = new Dictionary<string, IReadOnlyList<JsonElement>>
        {
            ["items"] = doc.RootElement.EnumerateArray().ToList(),
        };

        IReadOnlyList<JsonElement> resolved = RefResolver.Resolve("items[*].A.B.C", priorResults);

        Assert.Equal(42, resolved[0].GetInt32());
    }

    [Fact]
    public void Resolve_ScalarRefWithMultipleStepResults_ThrowsRatherThanSilentlyUsingOnlyFirstElement()
    {
        // When a step produced multiple results and the ref uses "stepName.property"
        // (no array expansion [*]), RefResolver currently silently uses only stepResults[0]
        // and ignores the rest. This is a silent data-loss bug: users writing
        // "customers.Name" intend to retrieve from a single-result config step,
        // but if "customers" happened to produce N results only the first is visible.
        // The correct behavior is to throw InvalidOperationException naming the step,
        // because "stepName.property" implies exactly one result.
        // Currently Resolve silently returns the property from index 0, so this FAILS.

        JsonDocument doc = JsonDocument.Parse("""
            [{"Name": "Alice"}, {"Name": "Bob"}, {"Name": "Carol"}]
            """);
        IDictionary<string, IReadOnlyList<JsonElement>> priorResults = new Dictionary<string, IReadOnlyList<JsonElement>>
        {
            ["customers"] = doc.RootElement.EnumerateArray().ToList(),
        };

        // "customers.Name" has no [*] — implies a single-result step.
        // Having 3 results is ambiguous; Resolve must throw rather than silently pick index 0.
        Exception ex = Assert.Throws<InvalidOperationException>(() =>
            RefResolver.Resolve("customers.Name", priorResults));

        Assert.Contains("customers", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Resolve_NestedPropertyPath_MissingIntermediateProperty_ThrowsWithPropertyName()
    {
        JsonDocument doc = JsonDocument.Parse("""[{"Address": {"City": "Oslo"}}]""");

        IDictionary<string, IReadOnlyList<JsonElement>> priorResults = new Dictionary<string, IReadOnlyList<JsonElement>>
        {
            ["customers"] = doc.RootElement.EnumerateArray().ToList(),
        };

        Exception ex = Assert.Throws<InvalidOperationException>(() =>
            RefResolver.Resolve("customers[*].Address.PostCode", priorResults));

        Assert.Contains("PostCode", ex.Message);
    }
}

public class PlanRunnerNestedRefTests
{
    private static string AssemblyPath => typeof(PlanRunnerNestedRefTests).Assembly.Location.Replace("\\", "/");

    // ── End-to-end nested property ref through PlanRunner ─────────────────────
    //
    // These tests verify that PlanRunner supports $ref expressions that navigate
    // nested object properties (e.g. "step[*].Address.City"). This requires both
    // PlanRunner to support non-primitive types AND RefResolver to navigate nested paths.
    // The PlanRunner currently only supports System.Int32, so generating a step whose
    // serialised output has nested objects is not yet possible end-to-end.
    // These tests will FAIL until PlanRunner supports record/object types.

    [Fact(Skip = "Pending: generic type support via reflection; currently only System.Int32 and System.String are supported")]
    public void RunAsync_BindingToNestedPropertyPath_ResolvesTwoLevels()
    {
        // Goal: a "locations" step produces objects like {"City": "Oslo"},
        // and a later "items" step binds to "locations[*].Address.City".
        // PlanRunner must support the object type and nested ref path end-to-end.
        // This requires implementing generic type support via reflection or [Arbitrary] source generators.

        string json = $$"""
            {
              "assembly": "{{AssemblyPath}}",
              "steps": [
                {
                  "name": "locations",
                  "type": "Conjecture.Tool.Tests.Plan.TestModels.Location",
                  "count": 3,
                  "seed": 1
                },
                {
                  "name": "orders",
                  "type": "System.Int32",
                  "count": 3,
                  "seed": 2,
                  "bindings": {
                    "Value": { "$ref": "locations[*].CityCode" }
                  }
                }
              ],
              "output": { "format": "json", "file": null }
            }
            """;

        GenerationPlan plan = JsonSerializer.Deserialize<GenerationPlan>(json)!;
        PlanRunner runner = new();

        // FAILS: PlanRunner throws NotImplementedException for non-Int32 types
        PlanResult result = runner.Run(plan);

        Assert.Equal(3, result.StepResults["orders"].Count);
    }

    [Fact]
    public void RunAsync_BindingToNestedPropertyPath_ErrorMentionsMissingSegment()
    {
        // When a nested ref path segment does not exist on the serialised output object,
        // PlanRunner must surface a PlanException that names the missing property segment.
        // This tests the wrapping of InvalidOperationException from RefResolver into PlanException
        // when the property path is like "step[*].Address.NonExistentProp".

        string json = $$"""
            {
              "assembly": "{{AssemblyPath}}",
              "steps": [
                { "name": "source", "type": "System.Int32", "count": 2, "seed": 1 },
                {
                  "name": "consumer",
                  "type": "System.Int32",
                  "count": 2,
                  "seed": 2,
                  "bindings": {
                    "Value": { "$ref": "source[*].Nested.Property" }
                  }
                }
              ],
              "output": { "format": "json", "file": null }
            }
            """;

        // source[*] generates bare int JSON values (e.g. [3, 7]).
        // Accessing .Nested.Property on a bare integer element throws InvalidOperationException
        // from RefResolver because the element is not a JSON object.
        // PlanRunner currently wraps this InvalidOperationException as PlanException (exitCode=1).
        // The message should mention the problematic path segment.

        GenerationPlan plan = JsonSerializer.Deserialize<GenerationPlan>(json)!;
        PlanRunner runner = new();

        PlanException ex = Assert.Throws<PlanException>(() => runner.Run(plan));

        // Must mention the offending path segment so the user can debug their plan file.
        // Currently PlanRunner wraps RefResolver exceptions — but does NOT include the
        // binding name ("Value") or the step name ("consumer") in the message.
        // This assertion will FAIL until the wrapping message includes that context.
        Assert.Contains("consumer", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ── Ref resolution validation before type dispatch ───────────────────────────

    [Fact]
    public void RunAsync_UnsupportedTypeWithBinding_ResolveRefBeforeThrowing()
    {
        // When a step declares a binding to a missing prior step, PlanRunner must
        // validate the ref exists BEFORE checking if the step's type is supported.
        // This ensures ref resolution errors (PlanException with exit code 1) take
        // precedence over unsupported type errors (NotImplementedException).
        // The contract: validate all inputs before dispatching on type.
        string assemblyPath = typeof(PlanRunnerNestedRefTests).Assembly.Location.Replace("\\", "/");
        string json = $$"""
            {
              "assembly": "{{assemblyPath}}",
              "steps": [
                {
                  "name": "consumer",
                  "type": "System.Guid",
                  "count": 2,
                  "seed": 1,
                  "bindings": {
                    "Value": { "$ref": "nonexistent[*]" }
                  }
                }
              ],
              "output": { "format": "json", "file": null }
            }
            """;

        GenerationPlan plan = JsonSerializer.Deserialize<GenerationPlan>(json)!;
        PlanRunner runner = new();

        // Must throw PlanException (ref validation), not NotImplementedException (type dispatch)
        PlanException ex = Assert.Throws<PlanException>(() => runner.Run(plan));

        Assert.Equal(1, ex.ExitCode);
        Assert.Contains("undefined step", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}