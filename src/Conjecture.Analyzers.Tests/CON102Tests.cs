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

    // --- .GetAwaiter().GetResult() inside [Property] → CON102 ---

    [Fact]
    public async Task GetAwaiterGetResult_InsidePropertyMethod_EmitsCon102()
    {
        await VerifyAnalyzerAsync(Preamble + """
            class Tests {
                [Property]
                public void Foo(int x) { {|CON102:Task.Delay(0).GetAwaiter().GetResult()|}; }
            }
            """);
    }

    [Fact]
    public async Task GetAwaiterGetResult_InsidePropertyMethod_Con102IsInfo()
    {
        await VerifyAnalyzerAsync(
            Preamble + """
            class Tests {
                [Property]
                public void Foo(int x) { {|#0:Task.Delay(0).GetAwaiter().GetResult()|}; }
            }
            """,
            new DiagnosticResult("CON102", DiagnosticSeverity.Info).WithLocation(0));
    }

    // --- .Result on Task inside [Property] → CON102 ---

    [Fact]
    public async Task TaskResult_InsidePropertyMethod_EmitsCon102()
    {
        await VerifyAnalyzerAsync(Preamble + """
            class Tests {
                [Property]
                public void Foo(int x) { int r = {|CON102:Task.FromResult(x).Result|}; }
            }
            """);
    }

    [Fact]
    public async Task TaskResult_InsidePropertyMethod_Con102IsInfo()
    {
        await VerifyAnalyzerAsync(
            Preamble + """
            class Tests {
                [Property]
                public void Foo(int x) { int r = {|#0:Task.FromResult(x).Result|}; }
            }
            """,
            new DiagnosticResult("CON102", DiagnosticSeverity.Info).WithLocation(0));
    }

    // --- .Wait() on Task inside [Property] → CON102 ---

    [Fact]
    public async Task TaskWait_InsidePropertyMethod_EmitsCon102()
    {
        await VerifyAnalyzerAsync(Preamble + """
            class Tests {
                [Property]
                public void Foo(int x) { {|CON102:Task.Delay(0).Wait()|}; }
            }
            """);
    }

    [Fact]
    public async Task TaskWait_InsidePropertyMethod_Con102IsInfo()
    {
        await VerifyAnalyzerAsync(
            Preamble + """
            class Tests {
                [Property]
                public void Foo(int x) { {|#0:Task.Delay(0).Wait()|}; }
            }
            """,
            new DiagnosticResult("CON102", DiagnosticSeverity.Info).WithLocation(0));
    }

    // --- Same patterns outside [Property] → no diagnostic ---

    [Fact]
    public async Task GetAwaiterGetResult_OutsidePropertyMethod_NoCon102()
    {
        await VerifyAnalyzerAsync(Preamble + """
            class Tests {
                public void Foo(int x) { Task.Delay(0).GetAwaiter().GetResult(); }
            }
            """);
    }

    [Fact]
    public async Task TaskResult_OutsidePropertyMethod_NoCon102()
    {
        await VerifyAnalyzerAsync(Preamble + """
            class Tests {
                public void Foo(int x) { int r = Task.FromResult(x).Result; }
            }
            """);
    }

    [Fact]
    public async Task TaskWait_OutsidePropertyMethod_NoCon102()
    {
        await VerifyAnalyzerAsync(Preamble + """
            class Tests {
                public void Foo(int x) { Task.Delay(0).Wait(); }
            }
            """);
    }

    // --- Code fix: GetAwaiter().GetResult() → async Task + await ---

    [Fact]
    public async Task CodeFix_GetAwaiterGetResult_ConvertsToAsyncAwait()
    {
        string source = Preamble + """
            class Tests {
                [Property]
                public void Foo(int x) { {|CON102:Task.Delay(0).GetAwaiter().GetResult()|}; }
            }
            """;
        string fixedSource = Preamble + """
            class Tests {
                [Property]
                public async Task Foo(int x) { await Task.Delay(0); }
            }
            """;

        await VerifyCodeFixAsync(source, fixedSource);
    }

    [Fact]
    public async Task CodeFix_GetAwaiterGetResult_MethodReturnsTask()
    {
        string source = Preamble + """
            class Tests {
                [Property]
                public void Foo(int x) { {|CON102:Task.Delay(0).GetAwaiter().GetResult()|}; }
            }
            """;
        string fixedSource = Preamble + """
            class Tests {
                [Property]
                public async Task Foo(int x) { await Task.Delay(0); }
            }
            """;

        await VerifyCodeFixAsync(source, fixedSource);
    }

    // --- Code fix: .Result → async Task + await ---

    [Fact]
    public async Task CodeFix_TaskResult_ConvertsToAsyncAwait()
    {
        string source = Preamble + """
            class Tests {
                [Property]
                public void Foo(int x) { int r = {|CON102:Task.FromResult(x).Result|}; }
            }
            """;
        string fixedSource = Preamble + """
            class Tests {
                [Property]
                public async Task Foo(int x) { int r = await Task.FromResult(x); }
            }
            """;

        await VerifyCodeFixAsync(source, fixedSource);
    }

    // --- Code fix: .Wait() → async Task + await ---

    [Fact]
    public async Task CodeFix_TaskWait_ConvertsToAsyncAwait()
    {
        string source = Preamble + """
            class Tests {
                [Property]
                public void Foo(int x) { {|CON102:Task.Delay(0).Wait()|}; }
            }
            """;
        string fixedSource = Preamble + """
            class Tests {
                [Property]
                public async Task Foo(int x) { await Task.Delay(0); }
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