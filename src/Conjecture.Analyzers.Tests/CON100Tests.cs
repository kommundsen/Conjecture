// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace Conjecture.Analyzers.Tests;

public sealed class CON100Tests
{
    // Stub types so tests don't need external assembly references
    private const string Preamble = """
        using System;
        [AttributeUsage(AttributeTargets.Method)] class PropertyAttribute : Attribute {}
        static class Assert {
            public static void Equal<T>(T a, T b) {}
            public static void True(bool c) {}
        }
        static class FluentExtensions {
            public static FluentObj Should(this object o) => new FluentObj();
        }
        class FluentObj { public void Be(object v) {} }

        """;

    // --- Void [Property] method with Assert.Equal -> CON100 ---

    [Fact]
    public async Task VoidPropertyMethod_WithAssertEqual_EmitsCon100()
    {
        await VerifyAsync(Preamble + """
            class Tests {
                [Property]
                public void Foo(int x) { {|CON100:Assert.Equal(x, x)|}; }
            }
            """);
    }

    [Fact]
    public async Task VoidPropertyMethod_WithAssertEqual_Con100IsWarning()
    {
        await VerifyAsync(
            Preamble + """
            class Tests {
                [Property]
                public void Foo(int x) { {|#0:Assert.Equal(x, x)|}; }
            }
            """,
            new DiagnosticResult("CON100", DiagnosticSeverity.Warning).WithLocation(0));
    }

    // --- Void [Property] method with Assert.True -> CON100 ---

    [Fact]
    public async Task VoidPropertyMethod_WithAssertTrue_EmitsCon100()
    {
        await VerifyAsync(Preamble + """
            class Tests {
                [Property]
                public void Foo(int x) { {|CON100:Assert.True(x > 0)|}; }
            }
            """);
    }

    // --- Void [Property] method with Fluent Assertions -> CON100 ---

    [Fact]
    public async Task VoidPropertyMethod_WithFluentShould_EmitsCon100()
    {
        await VerifyAsync(Preamble + """
            class Tests {
                [Property]
                public void Foo(int x) { {|CON100:x.Should()|}.Be(x); }
            }
            """);
    }

    // --- Non-[Property] method with Assert.Equal -> no diagnostic ---

    [Fact]
    public async Task NonPropertyMethod_WithAssertEqual_NoCon100()
    {
        await VerifyAsync(Preamble + """
            class Tests {
                public void Foo(int x) { Assert.Equal(x, x); }
            }
            """);
    }

    // --- Helpers ---

    private static Task VerifyAsync(string source, params DiagnosticResult[] expected)
    {
        CSharpAnalyzerTest<CON100Analyzer, DefaultVerifier> test = new()
        {
            TestCode = source,
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }
}