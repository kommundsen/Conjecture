// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace Conjecture.Analyzers.Tests;

public sealed class CON110Tests
{
    // Stub types so tests don't need external assembly references.
    private const string Preamble = """
        using System;
        using System.Threading.Tasks;
        [AttributeUsage(AttributeTargets.Method)] class PropertyAttribute : Attribute {}

        """;

    // --- async [Property] method with no await → CON110 ---

    [Theory]
    [InlineData("Task<bool>", "return x > 0;")]
    [InlineData("Task", "")]
    public async Task AsyncProperty_NoAwait_EmitsCon110(string returnType, string body)
    {
        await VerifyAsync(Preamble + $$"""
            class Tests {
                [Property]
                public {|CON110:async|} {{returnType}} Foo(int x) { {{body}} }
            }
            """);
    }

    // --- Diagnostic is Info severity ---

    [Fact]
    public async Task AsyncTaskBool_PropertyMethodNoAwait_Con110IsInfo()
    {
        await VerifyAsync(
            Preamble + """
            class Tests {
                [Property]
                public {|#0:async|} Task<bool> Foo(int x) { return x > 0; }
            }
            """,
            new DiagnosticResult("CON110", DiagnosticSeverity.Info).WithLocation(0));
    }

    // --- Diagnostic message contains the method name ---

    [Fact]
    public async Task AsyncTaskBool_PropertyMethodNoAwait_Con110MessageContainsMethodName()
    {
        await VerifyAsync(
            Preamble + """
            class Tests {
                [Property]
                public {|#0:async|} Task<bool> Foo(int x) { return x > 0; }
            }
            """,
            new DiagnosticResult("CON110", DiagnosticSeverity.Info)
                .WithLocation(0)
                .WithArguments("Foo"));
    }

    // --- Diagnostic span is on the async keyword token ---

    [Fact]
    public async Task AsyncTask_PropertyMethodNoAwait_Con110SpanOnAsyncKeyword()
    {
        await VerifyAsync(
            Preamble + """
            class Tests {
                [Property]
                public {|#0:async|} Task Bar(int x) { }
            }
            """,
            new DiagnosticResult("CON110", DiagnosticSeverity.Info).WithLocation(0));
    }

    // --- Silent when async [Property] method contains at least one await ---

    [Theory]
    [InlineData("{ await Task.Yield(); return x > 0; }")]
    [InlineData("{ bool result = await Task.FromResult(x > 0); return result; }")]
    public async Task AsyncTaskBool_PropertyMethod_WithAwait_NoCon110(string body)
    {
        await VerifyAsync(Preamble + $$"""
            class Tests {
                [Property]
                public async Task<bool> Foo(int x) {{body}}
            }
            """);
    }

    // --- Silent for non-async [Property] methods ---

    [Theory]
    [InlineData("bool", "x > 0")]
    [InlineData("Task<bool>", "Task.FromResult(x > 0)")]
    public async Task NonAsync_PropertyMethod_NoCon110(string returnType, string body)
    {
        await VerifyAsync(Preamble + $$"""
            class Tests {
                [Property]
                public {{returnType}} Foo(int x) => {{body}};
            }
            """);
    }

    // --- Silent for async methods without [Property] ---

    [Theory]
    [InlineData("Task<bool> Foo(int x)", "{ return Task.FromResult(x > 0).Result; }")]
    [InlineData("Task Bar()", "{ return; }")]
    public async Task AsyncNonProperty_NoCon110(string signature, string body)
    {
        await VerifyAsync(Preamble + $$"""
            class Tests {
                public async {{signature}} {{body}}
            }
            """);
    }

    // --- Helpers ---

    private static Task VerifyAsync(string source, params DiagnosticResult[] expected)
    {
        CSharpAnalyzerTest<CON110Analyzer, DefaultVerifier> test = new()
        {
            TestCode = source,
            ReferenceAssemblies = TestHelpers.EmptyNet10,
        };
        TestHelpers.AddRuntimeReferences(test.TestState.AdditionalReferences);
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }
}
