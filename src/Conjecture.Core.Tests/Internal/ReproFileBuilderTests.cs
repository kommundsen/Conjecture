// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Collections.Generic;

using Conjecture.Core.Internal;

namespace Conjecture.Core.Tests.Internal;

public class ReproFileBuilderTests
{
    private static ReproContext MakeContext(
        string testClassName = "MyTests",
        string methodName = "MyProperty",
        bool isAsync = false,
        IEnumerable<(string Name, object? Value, Type Type)>? parameters = null,
        ulong seed = 0xDEADBEEFUL,
        int exampleCount = 100,
        int shrinkCount = 3,
        TestFramework framework = TestFramework.Xunit,
        DateTimeOffset? generatedAt = null)
    {
        return new(
            testClassName,
            methodName,
            isAsync,
            parameters ?? [(
                "n",
                (object?)42,
                typeof(int)
            )],
            seed,
            exampleCount,
            shrinkCount,
            framework,
            generatedAt ?? new DateTimeOffset(2026, 4, 10, 12, 0, 0, TimeSpan.Zero));
    }

    // --- xUnit sync ---

    [Fact]
    public void Build_SyncXunitIntParam_ContainsFactAttribute()
    {
        ReproContext ctx = MakeContext();

        string result = ReproFileBuilder.Build(ctx);

        Assert.Contains("[Fact]", result);
    }

    [Fact]
    public void Build_SyncXunitIntParam_ContainsXunitUsing()
    {
        ReproContext ctx = MakeContext();

        string result = ReproFileBuilder.Build(ctx);

        Assert.Contains("using Xunit;", result);
    }

    [Fact]
    public void Build_SyncXunitIntParam_ContainsIntVariableDeclaration()
    {
        ReproContext ctx = MakeContext();

        string result = ReproFileBuilder.Build(ctx);

        Assert.Contains("int n = 42", result);
    }

    [Fact]
    public void Build_SyncXunitIntParam_ContainsMethodCall()
    {
        ReproContext ctx = MakeContext(methodName: "MyProperty");

        string result = ReproFileBuilder.Build(ctx);

        Assert.Contains("MyProperty(", result);
    }

    [Fact]
    public void Build_SyncXunit_ReturnTypeIsVoid()
    {
        ReproContext ctx = MakeContext(isAsync: false);

        string result = ReproFileBuilder.Build(ctx);

        Assert.Contains("void ", result);
        Assert.DoesNotContain("async", result);
        Assert.DoesNotContain("await", result);
    }

    // --- async ---

    [Fact]
    public void Build_AsyncProperty_EmitsAsyncTask()
    {
        ReproContext ctx = MakeContext(isAsync: true);

        string result = ReproFileBuilder.Build(ctx);

        Assert.Contains("async Task", result);
    }

    [Fact]
    public void Build_AsyncProperty_EmitsAwait()
    {
        ReproContext ctx = MakeContext(isAsync: true);

        string result = ReproFileBuilder.Build(ctx);

        Assert.Contains("await ", result);
    }

    [Fact]
    public void Build_AsyncProperty_DoesNotContainVoidReturnType()
    {
        ReproContext ctx = MakeContext(isAsync: true);

        string result = ReproFileBuilder.Build(ctx);

        Assert.DoesNotContain("void ", result);
    }

    // --- seed ---

    [Fact]
    public void Build_WithSeed_ContainsSeedHeaderComment()
    {
        ReproContext ctx = MakeContext(seed: 0xDEADBEEFUL);

        string result = ReproFileBuilder.Build(ctx);

        Assert.Contains("// Seed: 0xDEADBEEF", result);
    }

    [Fact]
    public void Build_WithSeed_ContainsSeedInPropertyAttribute()
    {
        ReproContext ctx = MakeContext(seed: 0xDEADBEEFUL);

        string result = ReproFileBuilder.Build(ctx);

        Assert.Contains("[Property(Seed = 0xDEADBEEF)]", result);
    }

    [Fact]
    public void Build_WithZeroSeed_ContainsZeroSeedInBothPlaces()
    {
        ReproContext ctx = MakeContext(seed: 0UL);

        string result = ReproFileBuilder.Build(ctx);

        Assert.Contains("// Seed: 0x0", result);
        Assert.Contains("[Property(Seed = 0x0)]", result);
    }

    // --- platform header ---

    [Fact]
    public void Build_Always_ContainsPlatformOsInfo()
    {
        ReproContext ctx = MakeContext();

        string result = ReproFileBuilder.Build(ctx);

        Assert.Contains("OS:", result);
    }

    [Fact]
    public void Build_Always_ContainsDotNetVersionInfo()
    {
        ReproContext ctx = MakeContext();

        string result = ReproFileBuilder.Build(ctx);

        Assert.Contains(".NET:", result);
    }

    [Fact]
    public void Build_Always_ContainsArchitectureInfo()
    {
        ReproContext ctx = MakeContext();

        string result = ReproFileBuilder.Build(ctx);

        Assert.Contains("Arch:", result);
    }

    // --- NUnit ---

    [Fact]
    public void Build_NUnitFramework_EmitsTestAttribute()
    {
        ReproContext ctx = MakeContext(framework: TestFramework.NUnit);

        string result = ReproFileBuilder.Build(ctx);

        Assert.Contains("[Test]", result);
    }

    [Fact]
    public void Build_NUnitFramework_EmitsNUnitUsing()
    {
        ReproContext ctx = MakeContext(framework: TestFramework.NUnit);

        string result = ReproFileBuilder.Build(ctx);

        Assert.Contains("using NUnit.Framework;", result);
    }

    [Fact]
    public void Build_NUnitFramework_DoesNotEmitFactAttribute()
    {
        ReproContext ctx = MakeContext(framework: TestFramework.NUnit);

        string result = ReproFileBuilder.Build(ctx);

        Assert.DoesNotContain("[Fact]", result);
    }

    // --- MSTest ---

    [Fact]
    public void Build_MSTestFramework_EmitsTestMethodAttribute()
    {
        ReproContext ctx = MakeContext(framework: TestFramework.MSTest);

        string result = ReproFileBuilder.Build(ctx);

        Assert.Contains("[TestMethod]", result);
    }

    [Fact]
    public void Build_MSTestFramework_EmitsMSTestUsing()
    {
        ReproContext ctx = MakeContext(framework: TestFramework.MSTest);

        string result = ReproFileBuilder.Build(ctx);

        Assert.Contains("using Microsoft.VisualStudio.TestTools.UnitTesting;", result);
    }

    [Fact]
    public void Build_MSTestFramework_DoesNotEmitFactAttribute()
    {
        ReproContext ctx = MakeContext(framework: TestFramework.MSTest);

        string result = ReproFileBuilder.Build(ctx);

        Assert.DoesNotContain("[Fact]", result);
    }

    // --- StateMachine ---

    private static StateMachineReproContext MakeStateMachineContext(
        string testClassName = "MyStateMachineTests",
        string methodName = "MyStateMachineProperty",
        string sutTypeName = "MyStack",
        IReadOnlyList<(string Label, object? State, Type StateType)>? commands = null,
        string? violatedInvariant = "stack never empty",
        ulong seed = 0xDEADBEEFUL,
        int exampleCount = 100,
        int shrinkCount = 3,
        TestFramework framework = TestFramework.Xunit,
        DateTimeOffset? generatedAt = null)
    {
        return new(
            testClassName,
            methodName,
            sutTypeName,
            commands ?? [("Push", (object?)1, typeof(int)), ("Pop", (object?)0, typeof(int))],
            violatedInvariant,
            seed,
            exampleCount,
            shrinkCount,
            framework,
            generatedAt ?? new DateTimeOffset(2026, 4, 10, 12, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void BuildStateMachine_EmitsCommandsInOrder()
    {
        StateMachineReproContext ctx = MakeStateMachineContext();

        string result = ReproFileBuilder.BuildStateMachine(ctx);

        int pushIndex = result.IndexOf("sut.Push();", StringComparison.Ordinal);
        int popIndex = result.IndexOf("sut.Pop();", StringComparison.Ordinal);
        Assert.True(pushIndex >= 0, "Expected 'sut.Push();' in output");
        Assert.True(popIndex >= 0, "Expected 'sut.Pop();' in output");
        Assert.True(pushIndex < popIndex, "Expected 'sut.Push();' to appear before 'sut.Pop();'");
    }

    [Fact]
    public void BuildStateMachine_EachCommandHasStateComment()
    {
        StateMachineReproContext ctx = MakeStateMachineContext();

        string result = ReproFileBuilder.BuildStateMachine(ctx);

        Assert.Contains("// State: 1", result);
        Assert.Contains("// State: 0", result);
    }

    [Fact]
    public void BuildStateMachine_EmitsViolatedInvariantComment()
    {
        StateMachineReproContext ctx = MakeStateMachineContext(violatedInvariant: "stack never empty");

        string result = ReproFileBuilder.BuildStateMachine(ctx);

        Assert.Contains("// Invariant violated: stack never empty", result);
    }

    [Fact]
    public void BuildStateMachine_OmitsInvariantComment_WhenNull()
    {
        StateMachineReproContext ctx = MakeStateMachineContext(violatedInvariant: null);

        string result = ReproFileBuilder.BuildStateMachine(ctx);

        Assert.DoesNotContain("// Invariant violated:", result);
    }
}