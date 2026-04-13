// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace Conjecture.Analyzers.Tests;

public sealed class CON105Tests
{
    private const string Preamble = """
        using System;
        using Conjecture.Core;
        [AttributeUsage(AttributeTargets.Method)] class PropertyAttribute : Attribute {}

        """;

    // --- [Property] param of type Person where Person has [Arbitrary] -> CON105 info ---

    [Fact]
    public async Task PropertyParam_TypeWithArbitrary_EmitsCon105()
    {
        await VerifyAsync(Preamble + """
            [Arbitrary] class Person { }
            class Tests {
                [Property]
                public bool Foo({|CON105:Person p|}) => true;
            }
            """);
    }

    [Fact]
    public async Task PropertyParam_TypeWithArbitrary_Con105IsInfo()
    {
        await VerifyAsync(
            Preamble + """
            [Arbitrary] class Person { }
            class Tests {
                [Property]
                public bool Foo({|#0:Person p|}) => true;
            }
            """,
            new DiagnosticResult("CON105", DiagnosticSeverity.Info).WithLocation(0));
    }

    // --- [Property] param with [From<PersonArbitrary>] -> no diagnostic ---

    [Fact]
    public async Task PropertyParam_WithFromAttribute_NoCon105()
    {
        await VerifyAsync(Preamble + """
            [Arbitrary] class Person { }
            class PersonArbitrary : IStrategyProvider { }
            class Tests {
                [Property]
                public bool Foo([From<PersonArbitrary>] Person p) => true;
            }
            """);
    }

    // --- Non-[Property] method -> no diagnostic ---

    [Fact]
    public async Task NonPropertyMethod_NoCon105()
    {
        await VerifyAsync(Preamble + """
            [Arbitrary] class Person { }
            class Tests {
                public bool Foo(Person p) => true;
            }
            """);
    }

    // --- Type without [Arbitrary] -> no diagnostic ---

    [Fact]
    public async Task PropertyParam_TypeWithoutArbitrary_NoCon105()
    {
        await VerifyAsync(Preamble + """
            class Person { }
            class Tests {
                [Property]
                public bool Foo(Person p) => true;
            }
            """);
    }

    // --- Helpers ---

    private static Task VerifyAsync(string source, params DiagnosticResult[] expected)
    {
        CSharpAnalyzerTest<CON105Analyzer, DefaultVerifier> test = new()
        {
            TestCode = source,
            ReferenceAssemblies = TestHelpers.EmptyNet10,
        };
        TestHelpers.AddRuntimeReferences(test.TestState.AdditionalReferences);
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }
}