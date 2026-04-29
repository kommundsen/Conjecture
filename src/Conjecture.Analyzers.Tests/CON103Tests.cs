// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace Conjecture.Analyzers.Tests;

public sealed class CON103Tests
{
    // --- Integers: inverted bounds ---

    [Fact]
    public async Task Integers_InvertedConstantBounds_EmitsCon103()
    {
        await VerifyAnalyzerAsync("""
            using Conjecture.Core;
            class Test { void M() { var s = {|CON103:Strategy.Integers(10, 5)|}; } }
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

    // --- Doubles: inverted bounds ---

    [Fact]
    public async Task Doubles_InvertedConstantBounds_EmitsCon103()
    {
        await VerifyAnalyzerAsync("""
            using Conjecture.Core;
            class Test { void M() { var s = {|CON103:Strategy.Doubles(1.0, 0.5)|}; } }
            """);
    }

    // --- Floats: inverted bounds ---

    [Fact]
    public async Task Floats_InvertedConstantBounds_EmitsCon103()
    {
        await VerifyAnalyzerAsync("""
            using Conjecture.Core;
            class Test { void M() { var s = {|CON103:Strategy.Floats(1f, 0f)|}; } }
            """);
    }

    // --- Strings: inverted bounds (named parameters) ---

    [Fact]
    public async Task Strings_InvertedNamedBounds_EmitsCon103()
    {
        await VerifyAnalyzerAsync("""
            using Conjecture.Core;
            class Test { void M() { var s = {|CON103:Strategy.Strings(minLength: 10, maxLength: 5)|}; } }
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

    [Fact]
    public async Task CodeFix_SwapsArguments_ForIntegers()
    {
        await VerifyCodeFixAsync(
            """
            using Conjecture.Core;
            class Test { void M() { var s = {|CON103:Strategy.Integers(10, 5)|}; } }
            """,
            """
            using Conjecture.Core;
            class Test { void M() { var s = Strategy.Integers(5, 10); } }
            """);
    }

    [Fact]
    public async Task CodeFix_SwapsArguments_ForDoubles()
    {
        await VerifyCodeFixAsync(
            """
            using Conjecture.Core;
            class Test { void M() { var s = {|CON103:Strategy.Doubles(1.0, 0.5)|}; } }
            """,
            """
            using Conjecture.Core;
            class Test { void M() { var s = Strategy.Doubles(0.5, 1.0); } }
            """);
    }

    [Fact]
    public async Task CodeFix_SwapsArguments_ForStringsWithNamedParams()
    {
        await VerifyCodeFixAsync(
            """
            using Conjecture.Core;
            class Test { void M() { var s = {|CON103:Strategy.Strings(minLength: 10, maxLength: 5)|}; } }
            """,
            """
            using Conjecture.Core;
            class Test { void M() { var s = Strategy.Strings(minLength: 5, maxLength: 10); } }
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