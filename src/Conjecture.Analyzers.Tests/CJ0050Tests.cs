// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace Conjecture.Analyzers.Tests;

public sealed class CJ0050Tests
{
    // Types defined at file scope — CJ0050Analyzer checks type.Name == "Strategy", not namespace.
    // Using a namespace + trailing `using` would cause CS1529 under the new test framework.
    private const string Preamble = """
        using System;
        using System.Collections.Generic;
        public class Strategy<T> {
            public Strategy<T> Where(Func<T, bool> predicate) => this;
            public Strategy<T> Positive => this;
        }
        public static class Generate {
            public static Strategy<int> Integers() => new();
            public static Strategy<string> Strings() => new();
            public static Strategy<List<T>> Lists<T>() => new();
        }
        """;

    // --- .Where(x => x > 0) on Strategy<int> → CJ0050 ---

    [Fact]
    public async Task WhereXGreaterThan0_OnStrategyInt_EmitsCJ0050()
    {
        await VerifyAnalyzerAsync(Preamble + """
            class Tests {
                void Foo() { Strategy<int> s = {|CJ0050:Generate.Integers().Where(x => x > 0)|}; }
            }
            """);
    }

    // --- .Where(x => x < 0) on Strategy<int> → CJ0050 ---

    [Fact]
    public async Task WhereXLessThan0_OnStrategyInt_EmitsCJ0050()
    {
        await VerifyAnalyzerAsync(Preamble + """
            class Tests {
                void Foo() { Strategy<int> s = {|CJ0050:Generate.Integers().Where(x => x < 0)|}; }
            }
            """);
    }

    // --- .Where(x => x != 0) on Strategy<int> → CJ0050 ---

    [Fact]
    public async Task WhereXNotEqualTo0_OnStrategyInt_EmitsCJ0050()
    {
        await VerifyAnalyzerAsync(Preamble + """
            class Tests {
                void Foo() { Strategy<int> s = {|CJ0050:Generate.Integers().Where(x => x != 0)|}; }
            }
            """);
    }

    // --- .Where(x => x.Length > 0) on Strategy<string> → CJ0050 ---

    [Fact]
    public async Task WhereXLengthGreaterThan0_OnStrategyString_EmitsCJ0050()
    {
        await VerifyAnalyzerAsync(Preamble + """
            class Tests {
                void Foo() { Strategy<string> s = {|CJ0050:Generate.Strings().Where(x => x.Length > 0)|}; }
            }
            """);
    }

    // --- .Where(x => x.Count > 0) on Strategy<List<int>> → CJ0050 ---

    [Fact]
    public async Task WhereXCountGreaterThan0_OnStrategyList_EmitsCJ0050()
    {
        await VerifyAnalyzerAsync(Preamble + """
            class Tests {
                void Foo() { Strategy<List<int>> s = {|CJ0050:Generate.Lists<int>().Where(x => x.Count > 0)|}; }
            }
            """);
    }

    // --- .Where(x => x > 1) on Strategy<int> → no CJ0050 (custom predicate) ---

    [Fact]
    public async Task WhereCustomPredicate_OnStrategyInt_NoCJ0050()
    {
        await VerifyAnalyzerAsync(Preamble + """
            class Tests {
                void Foo() { Strategy<int> s = Generate.Integers().Where(x => x > 1); }
            }
            """);
    }

    // --- .Where(x => x > 0) on IEnumerable<int> (plain LINQ) → no CJ0050 ---

    [Fact]
    public async Task WhereXGreaterThan0_OnIEnumerableInt_NoCJ0050()
    {
        // Use fully-qualified names to avoid placing `using` directives after the Preamble's
        // type declarations (which would cause CS1529 under the new test framework).
        await VerifyAnalyzerAsync(Preamble + """
            class Tests {
                void Foo() {
                    System.Collections.Generic.IEnumerable<int> source
                        = new System.Collections.Generic.List<int>();
                    System.Collections.Generic.IEnumerable<int> result
                        = System.Linq.Enumerable.Where(source, x => x > 0);
                }
            }
            """);
    }

    // --- Severity is Info ---

    [Fact]
    public async Task CJ0050_IsInfoSeverity()
    {
        await VerifyAnalyzerAsync(
            Preamble + """
            class Tests {
                void Foo() { Strategy<int> s = {|#0:Generate.Integers().Where(x => x > 0)|}; }
            }
            """,
            new DiagnosticResult("CJ0050", DiagnosticSeverity.Info).WithLocation(0));
    }

    // --- Code fix: .Where(x => x > 0) → .Positive ---

    [Fact]
    public async Task CodeFix_WherePositive_ReplacesWithPositiveProperty()
    {
        await VerifyCodeFixAsync(
            Preamble + """
            class Tests {
                void Foo() { Strategy<int> s = {|CJ0050:Generate.Integers().Where(x => x > 0)|}; }
            }
            """,
            Preamble + """
            class Tests {
                void Foo() { Strategy<int> s = Generate.Integers().Positive; }
            }
            """);
    }

    // --- Helpers ---

    private static Task VerifyAnalyzerAsync(string source, params DiagnosticResult[] expected)
    {
        CSharpAnalyzerTest<CJ0050Analyzer, DefaultVerifier> test = new()
        {
            TestCode = source,
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    private static Task VerifyCodeFixAsync(string source, string fixedSource)
    {
        CSharpCodeFixTest<CJ0050Analyzer, CJ0050CodeFix, DefaultVerifier> test = new()
        {
            TestCode = source,
            FixedCode = fixedSource,
        };
        return test.RunAsync();
    }
}