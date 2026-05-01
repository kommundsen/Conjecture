// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace Conjecture.Analyzers.Tests;

public sealed class CON109Tests
{
    // Stub types so tests don't need external assembly references.
    // IStrategyProvider and From<T> are stubbed; [Arbitrary] is imported from Conjecture.Core.
    private const string Preamble = """
        using System;
        using Conjecture.Core;
        [AttributeUsage(AttributeTargets.Method)] class PropertyAttribute : Attribute {}

        """;

    // --- [Property] parameter of custom type with no strategy → CON109 ---

    [Theory]
    [InlineData(
        "struct MyPoint { public int X; public int Y; }",
        "MyPoint {|CON109:p|}")]
    [InlineData(
        "class Widget { }",
        "Widget {|CON109:w|}")]
    public async Task PropertyParam_CustomType_EmitsCon109(string typeDecl, string param)
    {
        await VerifyAsync(Preamble + $$"""
            {{typeDecl}}
            class Tests {
                [Property]
                public bool Foo({{param}}) => true;
            }
            """);
    }

    // --- Strategies resolved via [From<T>] or [Arbitrary] → no diagnostic ---

    [Fact]
    public async Task PropertyParam_WithFromAttribute_NoCon109()
    {
        await VerifyAsync(Preamble + """
            struct MyPoint { public int X; public int Y; }
            class MyPointStrategy : IStrategyProvider { }
            class Tests {
                [Property]
                public bool Foo([From<MyPointStrategy>] MyPoint p) => true;
            }
            """);
    }

    [Fact]
    public async Task PropertyParam_TypeWithArbitraryAttribute_NoCon109()
    {
        await VerifyAsync(Preamble + """
            [Arbitrary] class Person { }
            class Tests {
                [Property]
                public bool Foo(Person p) => true;
            }
            """);
    }

    // --- Built-in / known types → no diagnostic ---

    [Theory]
    [InlineData("int x", "x >= 0")]
    [InlineData("bool b", "b || !b")]
    [InlineData("string s", "s is not null")]
    [InlineData("double d", "double.IsFinite(d) || !double.IsFinite(d)")]
    [InlineData("System.Int32 x", "x >= 0")]
    public async Task PropertyParam_BuiltInType_NoCon109(string param, string body)
    {
        await VerifyAsync(Preamble + $$"""
            class Tests {
                [Property]
                public bool Foo({{param}}) => {{body}};
            }
            """);
    }

    // --- Methods without [Property] → no diagnostic ---

    [Fact]
    public async Task NonPropertyMethod_CustomStructParam_NoCon109()
    {
        await VerifyAsync(Preamble + """
            struct MyPoint { public int X; public int Y; }
            class Tests {
                public bool Foo(MyPoint p) => true;
            }
            """);
    }

    // --- Diagnostic span and metadata ---

    [Fact]
    public async Task PropertyParam_CustomStruct_Con109IsWarning()
    {
        await VerifyAsync(
            Preamble + """
            struct MyPoint { public int X; public int Y; }
            class Tests {
                [Property]
                public bool Foo(MyPoint {|#0:p|}) => true;
            }
            """,
            new DiagnosticResult("CON109", DiagnosticSeverity.Warning).WithLocation(0));
    }

    [Fact]
    public async Task PropertyParam_CustomStruct_Con109MessageContainsParamAndType()
    {
        await VerifyAsync(
            Preamble + """
            struct MyPoint { public int X; public int Y; }
            class Tests {
                [Property]
                public bool Foo(MyPoint {|#0:p|}) => true;
            }
            """,
            new DiagnosticResult("CON109", DiagnosticSeverity.Warning)
                .WithLocation(0)
                .WithArguments("p", "MyPoint"));
    }

    // --- Helpers ---

    private static Task VerifyAsync(string source, params DiagnosticResult[] expected)
    {
        CSharpAnalyzerTest<CON109Analyzer, DefaultVerifier> test = new()
        {
            TestCode = source,
            ReferenceAssemblies = TestHelpers.EmptyNet10,
        };
        TestHelpers.AddRuntimeReferences(test.TestState.AdditionalReferences);
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }
}
