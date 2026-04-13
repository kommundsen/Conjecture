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

    // --- Fires on [Property] parameter of custom struct with no [Arbitrary] and no [From<T>] ---

    [Fact]
    public async Task PropertyParam_CustomStructNoArbitraryNoFrom_EmitsCon109()
    {
        await VerifyAsync(Preamble + """
            struct MyPoint { public int X; public int Y; }
            class Tests {
                [Property]
                public bool Foo(MyPoint {|CON109:p|}) => true;
            }
            """);
    }

    // --- Fires on [Property] parameter of custom class with no [Arbitrary] and no [From<T>] ---

    [Fact]
    public async Task PropertyParam_CustomClassNoArbitraryNoFrom_EmitsCon109()
    {
        await VerifyAsync(Preamble + """
            class Widget { }
            class Tests {
                [Property]
                public bool Foo(Widget {|CON109:w|}) => true;
            }
            """);
    }

    // --- Silent when parameter has [From<TStrategy>] ---

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

    // --- Silent when parameter type is decorated with [Arbitrary] ---

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

    // --- Silent for built-in int parameter ---

    [Fact]
    public async Task PropertyParam_BuiltInInt_NoCon109()
    {
        await VerifyAsync(Preamble + """
            class Tests {
                [Property]
                public bool Foo(int x) => x >= 0;
            }
            """);
    }

    // --- Silent for built-in bool parameter ---

    [Fact]
    public async Task PropertyParam_BuiltInBool_NoCon109()
    {
        await VerifyAsync(Preamble + """
            class Tests {
                [Property]
                public bool Foo(bool b) => b || !b;
            }
            """);
    }

    // --- Silent for built-in string parameter ---

    [Fact]
    public async Task PropertyParam_BuiltInString_NoCon109()
    {
        await VerifyAsync(Preamble + """
            class Tests {
                [Property]
                public bool Foo(string s) => s is not null;
            }
            """);
    }

    // --- Silent for built-in double parameter ---

    [Fact]
    public async Task PropertyParam_BuiltInDouble_NoCon109()
    {
        await VerifyAsync(Preamble + """
            class Tests {
                [Property]
                public bool Foo(double d) => double.IsFinite(d) || !double.IsFinite(d);
            }
            """);
    }

    // --- Silent for fully-qualified primitive (System.Int32) ---

    [Fact]
    public async Task PropertyParam_FullyQualifiedInt_NoCon109()
    {
        await VerifyAsync(Preamble + """
            class Tests {
                [Property]
                public bool Foo(System.Int32 x) => x >= 0;
            }
            """);
    }

    // --- Silent for methods without [Property] ---

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

    // --- Diagnostic span points to the parameter name ---

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

    // --- Diagnostic message contains parameter name and type name ---

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