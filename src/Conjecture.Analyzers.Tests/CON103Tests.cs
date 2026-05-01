// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace Conjecture.Analyzers.Tests;

public sealed class CON103Tests
{
    // --- Inverted bounds → CON103 ---

    [Theory]
    [InlineData("{|CON103:Strategy.Integers(10, 5)|}")]
    [InlineData("{|CON103:Strategy.Doubles(1.0, 0.5)|}")]
    [InlineData("{|CON103:Strategy.Floats(1f, 0f)|}")]
    [InlineData("{|CON103:Strategy.Strings(minLength: 10, maxLength: 5)|}")]
    public async Task InvertedBounds_EmitsCon103(string callExpr)
    {
        await VerifyAnalyzerAsync($$"""
            using Conjecture.Core;
            class Test { void M() { var s = {{callExpr}}; } }
            """);
    }

    [Fact]
    public async Task Integers_InvertedConstantBounds_Con103IsError()
    {
        await VerifyAnalyzerAsync(
            """
            using Conjecture.Core;
            class Test { void M() { var s = {|#0:Strategy.Integers(10, 5)|}; } }
            """,
            new DiagnosticResult("CON103", DiagnosticSeverity.Error).WithLocation(0));
    }

    // --- Valid bounds → no diagnostic ---

    [Fact]
    public async Task Integers_ValidBounds_NoCon103()
    {
        await VerifyAnalyzerAsync("""
            using Conjecture.Core;
            class Test { void M() { var s = Strategy.Integers(0, 100); } }
            """);
    }

    [Fact]
    public async Task Integers_NonConstantArgs_NoCon103()
    {
        await VerifyAnalyzerAsync("""
            using Conjecture.Core;
            class Test { void M(int a, int b) { var s = Strategy.Integers(a, b); } }
            """);
    }

    [Fact]
    public async Task Strings_ValidBounds_NoCon103()
    {
        await VerifyAnalyzerAsync("""
            using Conjecture.Core;
            class Test { void M() { var s = Strategy.Strings(minLength: 0, maxLength: 20); } }
            """);
    }

    // --- Code fix: swaps arguments ---

    [Theory]
    [InlineData("{|CON103:Strategy.Integers(10, 5)|}", "Strategy.Integers(5, 10)")]
    [InlineData("{|CON103:Strategy.Doubles(1.0, 0.5)|}", "Strategy.Doubles(0.5, 1.0)")]
    [InlineData(
        "{|CON103:Strategy.Strings(minLength: 10, maxLength: 5)|}",
        "Strategy.Strings(minLength: 5, maxLength: 10)")]
    public async Task CodeFix_InvertedBounds_SwapsArguments(string sourceExpr, string fixedExpr)
    {
        await VerifyCodeFixAsync(
            $$"""
            using Conjecture.Core;
            class Test { void M() { var s = {{sourceExpr}}; } }
            """,
            $$"""
            using Conjecture.Core;
            class Test { void M() { var s = {{fixedExpr}}; } }
            """);
    }

    // --- Helpers ---

    private static Task VerifyAnalyzerAsync(string source, params DiagnosticResult[] expected)
    {
        CSharpAnalyzerTest<CON103Analyzer, DefaultVerifier> test = new()
        {
            TestCode = source,
            ReferenceAssemblies = TestHelpers.EmptyNet10,
        };
        TestHelpers.AddRuntimeReferences(test.TestState.AdditionalReferences);
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    private static Task VerifyCodeFixAsync(string source, string fixedSource)
    {
        CSharpCodeFixTest<CON103Analyzer, CON103CodeFix, DefaultVerifier> test = new()
        {
            TestCode = source,
            FixedCode = fixedSource,
            ReferenceAssemblies = TestHelpers.EmptyNet10,
        };
        TestHelpers.AddRuntimeReferences(test.TestState.AdditionalReferences);
        TestHelpers.AddRuntimeReferences(test.FixedState.AdditionalReferences);
        return test.RunAsync();
    }
}
