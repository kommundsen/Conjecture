// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace Conjecture.Analyzers.Tests;

public sealed class CON104Tests
{
    // --- Assume.That(false) literal → CON104 ---

    [Fact]
    public async Task AssumeThat_FalseLiteral_EmitsCon104()
    {
        await VerifyAsync("""
            using Conjecture.Core;
            class Tests { void M() { {|CON104:Assume.That(false)|}; } }
            """);
    }

    [Fact]
    public async Task AssumeThat_FalseLiteral_Con104IsWarning()
    {
        await VerifyAsync(
            """
            using Conjecture.Core;
            class Tests { void M() { {|#0:Assume.That(false)|}; } }
            """,
            new DiagnosticResult("CON104", DiagnosticSeverity.Warning).WithLocation(0));
    }

    // --- Assume.That(true) → no diagnostic ---

    [Fact]
    public async Task AssumeThat_TrueLiteral_NoCon104()
    {
        await VerifyAsync("""
            using Conjecture.Core;
            class Tests { void M() { Assume.That(true); } }
            """);
    }

    // --- Assume.That(variable) → no diagnostic ---

    [Fact]
    public async Task AssumeThat_VariableArgument_NoCon104()
    {
        await VerifyAsync("""
            using Conjecture.Core;
            class Tests { void M(bool condition) { Assume.That(condition); } }
            """);
    }

    // --- Helpers ---

    private static Task VerifyAsync(string source, params DiagnosticResult[] expected)
    {
        CSharpAnalyzerTest<CON104Analyzer, DefaultVerifier> test = new()
        {
            TestCode = source,
            ReferenceAssemblies = TestHelpers.EmptyNet10,
        };
        TestHelpers.AddRuntimeReferences(test.TestState.AdditionalReferences);
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }
}