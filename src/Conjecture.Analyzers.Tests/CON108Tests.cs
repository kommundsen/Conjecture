// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace Conjecture.Analyzers.Tests;

public sealed class CON108Tests
{
    // Preamble stubs PropertyAttribute and the named strategy classes.
    // Assume and From<T> come from Conjecture.Core via AddRuntimeReferences.
    private const string Preamble = """
        using System;
        using Conjecture.Core;
        [AttributeUsage(AttributeTargets.Method)] class PropertyAttribute : Attribute {}
        class PositiveInts : IStrategyProvider {}
        class NegativeInts : IStrategyProvider {}
        class NonNegativeInts : IStrategyProvider {}

        """;

    // --- Fires: [From<PositiveInts>] int x with Assume.That(x > 0) ---

    [Fact]
    public async Task PositiveInts_AssumeGreaterThanZero_EmitsCon108()
    {
        await VerifyAsync(Preamble + """
            class Tests {
                [Property]
                public bool Foo([From<PositiveInts>] int x)
                {
                    {|CON108:Assume.That(x > 0)|};
                    return x > 0;
                }
            }
            """);
    }

    // --- Fires: [From<NegativeInts>] int x with Assume.That(x < 0) ---

    [Fact]
    public async Task NegativeInts_AssumeLessThanZero_EmitsCon108()
    {
        await VerifyAsync(Preamble + """
            class Tests {
                [Property]
                public bool Foo([From<NegativeInts>] int x)
                {
                    {|CON108:Assume.That(x < 0)|};
                    return x < 0;
                }
            }
            """);
    }

    // --- Diagnostic severity and message ---

    [Fact]
    public async Task PositiveInts_AssumeGreaterThanZero_Con108IsWarning()
    {
        await VerifyAsync(
            Preamble + """
            class Tests {
                [Property]
                public bool Foo([From<PositiveInts>] int x)
                {
                    {|#0:Assume.That(x > 0)|};
                    return x > 0;
                }
            }
            """,
            new DiagnosticResult("CON108", DiagnosticSeverity.Warning).WithLocation(0));
    }

    [Fact]
    public async Task PositiveInts_AssumeGreaterThanZero_Con108MessageContainsParamAndStrategy()
    {
        await VerifyAsync(
            Preamble + """
            class Tests {
                [Property]
                public bool Foo([From<PositiveInts>] int x)
                {
                    {|#0:Assume.That(x > 0)|};
                    return x > 0;
                }
            }
            """,
            new DiagnosticResult("CON108", DiagnosticSeverity.Warning)
                .WithLocation(0)
                .WithArguments("x", "PositiveInts"));
    }

    // --- Silent: [From<PositiveInts>] int x with Assume.That(x > 5) (more restrictive) ---

    [Fact]
    public async Task PositiveInts_AssumeGreaterThanFive_NoCon108()
    {
        await VerifyAsync(Preamble + """
            class Tests {
                [Property]
                public bool Foo([From<PositiveInts>] int x)
                {
                    Assume.That(x > 5);
                    return x > 5;
                }
            }
            """);
    }

    // --- Fires: [From<PositiveInts>] int x with Assume.That(x >= 1) (x > 0 implies x >= 1) ---

    [Fact]
    public async Task PositiveInts_AssumeGreaterThanOrEqualOne_EmitsCon108()
    {
        await VerifyAsync(Preamble + """
            class Tests {
                [Property]
                public bool Foo([From<PositiveInts>] int x)
                {
                    {|CON108:Assume.That(x >= 1)|};
                    return x >= 1;
                }
            }
            """);
    }

    // --- Silent: custom provider type — unknown strategy ---

    [Fact]
    public async Task CustomProvider_NoCon108()
    {
        await VerifyAsync(Preamble + """
            class MyProvider : IStrategyProvider {}
            class Tests {
                [Property]
                public bool Foo([From<MyProvider>] int x)
                {
                    Assume.That(x > 0);
                    return x > 0;
                }
            }
            """);
    }

    // --- Silent: Assume.That(x > 0) without [From<T>] ---

    [Fact]
    public async Task NoFromAttribute_NoCon108()
    {
        await VerifyAsync(Preamble + """
            class Tests {
                [Property]
                public bool Foo(int x)
                {
                    Assume.That(x > 0);
                    return x > 0;
                }
            }
            """);
    }

    // --- Silent: outside a [Property] method ---

    [Fact]
    public async Task OutsidePropertyMethod_NoCon108()
    {
        await VerifyAsync(Preamble + """
            class Tests {
                public bool Foo([From<PositiveInts>] int x)
                {
                    Assume.That(x > 0);
                    return x > 0;
                }
            }
            """);
    }

    // --- Helpers ---

    private static Task VerifyAsync(string source, params DiagnosticResult[] expected)
    {
        CSharpAnalyzerTest<CON108Analyzer, DefaultVerifier> test = new()
        {
            TestCode = source,
            ReferenceAssemblies = TestHelpers.EmptyNet10,
        };
        TestHelpers.AddRuntimeReferences(test.TestState.AdditionalReferences);
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }
}