// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace Conjecture.Analyzers.Tests;

public sealed class CON111Tests
{
    private const string Preamble = """
        using System;
        using Conjecture.Core;
        [AttributeUsage(AttributeTargets.Method)] class PropertyAttribute : Attribute {}

        """;

    // --- Fires: Target.Maximize inside a non-[Property] method ---

    [Fact]
    public async Task Target_Maximize_NonPropertyMethod_EmitsCon111()
    {
        await VerifyAsync(Preamble + """
            class Tests {
                public void Foo(double x) { {|CON111:Target.Maximize(x)|}; }
            }
            """);
    }

    // --- Fires: Target.Minimize inside a non-[Property] method ---

    [Fact]
    public async Task Target_Minimize_NonPropertyMethod_EmitsCon111()
    {
        await VerifyAsync(Preamble + """
            class Tests {
                public void Foo(double x) { {|CON111:Target.Minimize(x)|}; }
            }
            """);
    }

    // --- Fires: Target.Maximize with no enclosing MethodDeclarationSyntax (static constructor) ---

    [Fact]
    public async Task Target_Maximize_NoEnclosingMethod_EmitsCon111()
    {
        await VerifyAsync(Preamble + """
            class Tests {
                static Tests() { {|CON111:Target.Maximize(1.0)|}; }
            }
            """);
    }

    // --- Silent: Target.Maximize inside a [Property] method ---

    [Fact]
    public async Task Target_Maximize_PropertyMethod_NoCon111()
    {
        await VerifyAsync(Preamble + """
            class Tests {
                [Property]
                public bool Foo(double x) { Target.Maximize(x); return x > 0; }
            }
            """);
    }

    // --- Silent: Target.Minimize inside a [Property] method ---

    [Fact]
    public async Task Target_Minimize_PropertyMethod_NoCon111()
    {
        await VerifyAsync(Preamble + """
            class Tests {
                [Property]
                public bool Foo(double x) { Target.Minimize(x); return x < 0; }
            }
            """);
    }

    // --- Diagnostic severity is Warning ---

    [Fact]
    public async Task Target_Maximize_NonPropertyMethod_Con111IsWarning()
    {
        await VerifyAsync(
            Preamble + """
            class Tests {
                public void Foo(double x) { {|#0:Target.Maximize(x)|}; }
            }
            """,
            new DiagnosticResult("CON111", DiagnosticSeverity.Warning).WithLocation(0));
    }

    // --- Diagnostic message contains the method name ---

    [Fact]
    public async Task Target_Maximize_NonPropertyMethod_Con111MessageContainsMethodName()
    {
        await VerifyAsync(
            Preamble + """
            class Tests {
                public void Foo(double x) { {|#0:Target.Maximize(x)|}; }
            }
            """,
            new DiagnosticResult("CON111", DiagnosticSeverity.Warning)
                .WithLocation(0)
                .WithArguments("Maximize"));
    }

    [Fact]
    public async Task Target_Minimize_NonPropertyMethod_Con111MessageContainsMethodName()
    {
        await VerifyAsync(
            Preamble + """
            class Tests {
                public void Foo(double x) { {|#0:Target.Minimize(x)|}; }
            }
            """,
            new DiagnosticResult("CON111", DiagnosticSeverity.Warning)
                .WithLocation(0)
                .WithArguments("Minimize"));
    }

    // --- Silent: user-defined Target type (not Conjecture.Core.Target) ---

    [Fact]
    public async Task UserDefinedTarget_Maximize_NonPropertyMethod_NoCon111()
    {
        await VerifyAsync("""
            using System;
            [AttributeUsage(AttributeTargets.Method)] class PropertyAttribute : Attribute {}
            static class Target { public static void Maximize(double x) { } }

            class Tests {
                public void Foo(double x) { Target.Maximize(x); }
            }
            """);
    }

    // --- Helpers ---

    private static Task VerifyAsync(string source, params DiagnosticResult[] expected)
    {
        CSharpAnalyzerTest<CON111Analyzer, DefaultVerifier> test = new()
        {
            TestCode = source,
            ReferenceAssemblies = TestHelpers.EmptyNet10,
        };
        TestHelpers.AddRuntimeReferences(test.TestState.AdditionalReferences);
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }
}