// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace Conjecture.Analyzers.Tests;

public sealed class CON101Tests
{
    // --- Equality on unbounded integer strategy ---

    [Fact]
    public async Task Integers_EqualityPredicate_EmitsCon101()
    {
        await VerifyAsync("""
            using Conjecture.Core;
            class Test { void M() { var s = {|CON101:Generate.Integers<int>().Where(x => x == 42)|}; } }
            """);
    }

    [Fact]
    public async Task Integers_EqualityPredicate_Con101IsWarning()
    {
        await VerifyAsync(
            """
            using Conjecture.Core;
            class Test { void M() { var s = {|#0:Generate.Integers<int>().Where(x => x == 42)|}; } }
            """,
            new DiagnosticResult("CON101", DiagnosticSeverity.Warning).WithLocation(0));
    }

    // --- Boolean equality ---

    [Fact]
    public async Task Booleans_EqualityToTrue_EmitsCon101()
    {
        await VerifyAsync("""
            using Conjecture.Core;
            class Test { void M() { var s = {|CON101:Generate.Booleans().Where(b => b == true)|}; } }
            """);
    }

    // --- False literal predicate ---

    [Fact]
    public async Task Where_FalseLiteralPredicate_EmitsCon101()
    {
        await VerifyAsync("""
            using Conjecture.Core;
            class Test { void M() { var s = {|CON101:Generate.Integers<int>().Where(x => false)|}; } }
            """);
    }

    // --- Complex predicates: no diagnostic ---

    [Fact]
    public async Task Where_ComplexPredicate_NoCon101()
    {
        await VerifyAsync("""
            using Conjecture.Core;
            class Test { void M() { var s = Generate.Integers<int>().Where(x => x > 0 && x < 100); } }
            """);
    }

    // --- Helpers ---

    private static Task VerifyAsync(string source, params DiagnosticResult[] expected)
    {
        CSharpAnalyzerTest<CON101Analyzer, DefaultVerifier> test = new()
        {
            TestCode = source,
            ReferenceAssemblies = TestHelpers.EmptyNet10,
        };
        TestHelpers.AddRuntimeReferences(test.TestState.AdditionalReferences);
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }
}