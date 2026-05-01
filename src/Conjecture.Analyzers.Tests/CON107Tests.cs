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

    // --- Any non-deterministic call inside [Property] → CON107 ---

    [Theory]
    [InlineData("Guid g = {|CON107:Guid.NewGuid()|};")]
    [InlineData("DateTime d = {|CON107:DateTime.Now|};")]
    [InlineData("DateTime d = {|CON107:DateTime.UtcNow|};")]
    [InlineData("Random r = {|CON107:new Random()|};")]
    [InlineData("Random r = {|CON107:Random.Shared|};")]
    [InlineData("DateTimeOffset d = {|CON107:DateTimeOffset.Now|};")]
    [InlineData("DateTimeOffset d = {|CON107:DateTimeOffset.UtcNow|};")]
    [InlineData("int t = {|CON107:Environment.TickCount|};")]
    [InlineData("long t = {|CON107:Environment.TickCount64|};")]
    public async Task NonDeterministicCall_InsidePropertyMethod_EmitsCon107(string statement)
    {
        await VerifyAsync(Preamble + $$"""
            class Tests {
                [Property]
                public void Foo() { {{statement}} }
            }
            """);
    }

    // --- Same calls outside [Property] → no diagnostic ---

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

    // --- [Property] method with no non-deterministic calls → no diagnostic ---

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
