// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace Conjecture.Analyzers.Tests;

public sealed class CON102Tests
{
    // Stub types — no external assembly required for [Property]
    private const string Preamble = """
        using System;
        using System.Threading.Tasks;
        [AttributeUsage(AttributeTargets.Method)] class PropertyAttribute : Attribute {}

        """;

    // --- Any blocking call inside [Property] → CON102 ---

    [Theory]
    [InlineData("{ {|CON102:Task.Delay(0).GetAwaiter().GetResult()|}; }")]
    [InlineData("{ int r = {|CON102:Task.FromResult(x).Result|}; }")]
    [InlineData("{ {|CON102:Task.Delay(0).Wait()|}; }")]
    public async Task BlockingCall_InsidePropertyMethod_EmitsCon102(string body)
    {
        await VerifyAnalyzerAsync(Preamble + $$"""
            class Tests {
                [Property]
                public void Foo(int x) {{body}}
            }
            """);
    }

    [Theory]
    [InlineData("{ {|#0:Task.Delay(0).GetAwaiter().GetResult()|}; }")]
    [InlineData("{ int r = {|#0:Task.FromResult(x).Result|}; }")]
    [InlineData("{ {|#0:Task.Delay(0).Wait()|}; }")]
    public async Task BlockingCall_InsidePropertyMethod_DiagnosticIsInfo(string body)
    {
        await VerifyAnalyzerAsync(
            Preamble + $$"""
            class Tests {
                [Property]
                public void Foo(int x) {{body}}
            }
            """,
            new DiagnosticResult("CON102", DiagnosticSeverity.Info).WithLocation(0));
    }

    // --- Same patterns outside [Property] → no diagnostic ---

    [Theory]
    [InlineData("{ Task.Delay(0).GetAwaiter().GetResult(); }")]
    [InlineData("{ int r = Task.FromResult(x).Result; }")]
    [InlineData("{ Task.Delay(0).Wait(); }")]
    public async Task BlockingCall_OutsidePropertyMethod_NoDiagnostic(string body)
    {
        await VerifyAnalyzerAsync(Preamble + $$"""
            class Tests {
                public void Foo(int x) {{body}}
            }
            """);
    }

    // --- Code fix: blocking call → async Task + await ---

    [Theory]
    [InlineData(
        "{ {|CON102:Task.Delay(0).GetAwaiter().GetResult()|}; }",
        "{ await Task.Delay(0); }")]
    [InlineData(
        "{ int r = {|CON102:Task.FromResult(x).Result|}; }",
        "{ int r = await Task.FromResult(x); }")]
    [InlineData(
        "{ {|CON102:Task.Delay(0).Wait()|}; }",
        "{ await Task.Delay(0); }")]
    public async Task CodeFix_BlockingCall_ConvertsToAsyncAwait(string body, string fixedBody)
    {
        string source = Preamble + $$"""
            class Tests {
                [Property]
                public void Foo(int x) {{body}}
            }
            """;
        string fixedSource = Preamble + $$"""
            class Tests {
                [Property]
                public async Task Foo(int x) {{fixedBody}}
            }
            """;

        await VerifyCodeFixAsync(source, fixedSource);
    }

    // --- Helpers ---

    private static Task VerifyAnalyzerAsync(string source, params DiagnosticResult[] expected)
    {
        CSharpAnalyzerTest<CON102Analyzer, DefaultVerifier> test = new()
        {
            TestCode = source,
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    private static Task VerifyCodeFixAsync(string source, string fixedSource)
    {
        CSharpCodeFixTest<CON102Analyzer, CON102CodeFix, DefaultVerifier> test = new()
        {
            TestCode = source,
            FixedCode = fixedSource,
        };
        return test.RunAsync();
    }
}
