// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace Conjecture.Analyzers.Tests;

public sealed class CON107Tests
{
    // Stub types so tests don't need external assembly references
    private const string Preamble = """
        using System;
        [AttributeUsage(AttributeTargets.Method)] class PropertyAttribute : Attribute {}

        """;

    // --- Fires on Guid.NewGuid() inside [Property] ---

    [Fact]
    public async Task GuidNewGuid_InsidePropertyMethod_EmitsCon107()
    {
        await VerifyAsync(Preamble + """
            class Tests {
                [Property]
                public void Foo() { Guid g = {|CON107:Guid.NewGuid()|}; }
            }
            """);
    }

    // --- Fires on DateTime.Now inside [Property] ---

    [Fact]
    public async Task DateTimeNow_InsidePropertyMethod_EmitsCon107()
    {
        await VerifyAsync(Preamble + """
            class Tests {
                [Property]
                public void Foo() { DateTime d = {|CON107:DateTime.Now|}; }
            }
            """);
    }

    // --- Fires on DateTime.UtcNow inside [Property] ---

    [Fact]
    public async Task DateTimeUtcNow_InsidePropertyMethod_EmitsCon107()
    {
        await VerifyAsync(Preamble + """
            class Tests {
                [Property]
                public void Foo() { DateTime d = {|CON107:DateTime.UtcNow|}; }
            }
            """);
    }

    // --- Fires on new Random() inside [Property] ---

    [Fact]
    public async Task NewRandom_InsidePropertyMethod_EmitsCon107()
    {
        await VerifyAsync(Preamble + """
            class Tests {
                [Property]
                public void Foo() { Random r = {|CON107:new Random()|}; }
            }
            """);
    }

    // --- Fires on Random.Shared inside [Property] ---

    [Fact]
    public async Task RandomShared_InsidePropertyMethod_EmitsCon107()
    {
        await VerifyAsync(Preamble + """
            class Tests {
                [Property]
                public void Foo() { Random r = {|CON107:Random.Shared|}; }
            }
            """);
    }

    // --- Fires on DateTimeOffset.Now inside [Property] ---

    [Fact]
    public async Task DateTimeOffsetNow_InsidePropertyMethod_EmitsCon107()
    {
        await VerifyAsync(Preamble + """
            class Tests {
                [Property]
                public void Foo() { DateTimeOffset d = {|CON107:DateTimeOffset.Now|}; }
            }
            """);
    }

    // --- Fires on DateTimeOffset.UtcNow inside [Property] ---

    [Fact]
    public async Task DateTimeOffsetUtcNow_InsidePropertyMethod_EmitsCon107()
    {
        await VerifyAsync(Preamble + """
            class Tests {
                [Property]
                public void Foo() { DateTimeOffset d = {|CON107:DateTimeOffset.UtcNow|}; }
            }
            """);
    }

    // --- Fires on Environment.TickCount inside [Property] ---

    [Fact]
    public async Task EnvironmentTickCount_InsidePropertyMethod_EmitsCon107()
    {
        await VerifyAsync(Preamble + """
            class Tests {
                [Property]
                public void Foo() { int t = {|CON107:Environment.TickCount|}; }
            }
            """);
    }

    // --- Fires on Environment.TickCount64 inside [Property] ---

    [Fact]
    public async Task EnvironmentTickCount64_InsidePropertyMethod_EmitsCon107()
    {
        await VerifyAsync(Preamble + """
            class Tests {
                [Property]
                public void Foo() { long t = {|CON107:Environment.TickCount64|}; }
            }
            """);
    }

    // --- Silent when same calls appear outside a [Property] method ---

    [Fact]
    public async Task NonDeterministicCalls_OutsidePropertyMethod_NoCon107()
    {
        await VerifyAsync(Preamble + """
            class Tests {
                public void Foo() {
                    Guid g = Guid.NewGuid();
                    DateTime d = DateTime.Now;
                    DateTime u = DateTime.UtcNow;
                    Random r = new Random();
                    Random s = Random.Shared;
                }
            }
            """);
    }

    // --- Silent when [Property] method has no non-deterministic calls ---

    [Fact]
    public async Task PropertyMethod_WithNonDeterministicCalls_NoCon107()
    {
        await VerifyAsync(Preamble + """
            class Tests {
                [Property]
                public void Foo(int x) { int y = x + 1; }
            }
            """);
    }

    // --- Diagnostic span points to the specific call site ---

    [Fact]
    public async Task GuidNewGuid_InsidePropertyMethod_Con107IsWarning()
    {
        await VerifyAsync(
            Preamble + """
            class Tests {
                [Property]
                public void Foo() { Guid g = {|#0:Guid.NewGuid()|}; }
            }
            """,
            new DiagnosticResult("CON107", DiagnosticSeverity.Warning).WithLocation(0));
    }

    // --- Helpers ---

    private static Task VerifyAsync(string source, params DiagnosticResult[] expected)
    {
        CSharpAnalyzerTest<CON107Analyzer, DefaultVerifier> test = new()
        {
            TestCode = source,
            ReferenceAssemblies = TestHelpers.EmptyNet10,
        };
        TestHelpers.AddRuntimeReferences(test.TestState.AdditionalReferences);
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }
}