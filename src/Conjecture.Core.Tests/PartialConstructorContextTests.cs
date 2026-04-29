// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core;

namespace Conjecture.Core.Tests;

public class PartialConstructorContextTests
{
    [Fact]
    public void Current_WithNoScopeActive_ThrowsInvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(static () => _ = PartialConstructorContext.Current);
    }

    [Fact]
    public void Current_InsideUseScope_ReturnsProvidedContext()
    {
        IGenerationContext ctx = new StubGenerationContext();

        using IDisposable scope = PartialConstructorContext.Use(ctx);

        Assert.Equal(ctx, PartialConstructorContext.Current);
    }

    [Fact]
    public void Current_AfterUseScopeDisposed_ThrowsInvalidOperationException()
    {
        IGenerationContext ctx = new StubGenerationContext();
        IDisposable scope = PartialConstructorContext.Use(ctx);

        scope.Dispose();

        Assert.Throws<InvalidOperationException>(static () => _ = PartialConstructorContext.Current);
    }

    [Fact]
    public void Current_InnerNestedScope_SeesInnerContext()
    {
        IGenerationContext outer = new StubGenerationContext();
        IGenerationContext inner = new StubGenerationContext();

        using IDisposable outerScope = PartialConstructorContext.Use(outer);
        using IDisposable innerScope = PartialConstructorContext.Use(inner);

        Assert.Equal(inner, PartialConstructorContext.Current);
    }

    [Fact]
    public void Current_AfterInnerScopeDisposed_RestoredToOuterContext()
    {
        IGenerationContext outer = new StubGenerationContext();
        IGenerationContext inner = new StubGenerationContext();

        using IDisposable outerScope = PartialConstructorContext.Use(outer);
        IDisposable innerScope = PartialConstructorContext.Use(inner);

        innerScope.Dispose();

        Assert.Equal(outer, PartialConstructorContext.Current);
    }

    [Fact]
    public async Task Current_TaskRunStartedOutsideScope_DoesNotSeeAmbientContext()
    {
        // Task is started BEFORE the scope is opened — it must not see the context
        // set later by the parent. This verifies sibling-task isolation: AsyncLocal
        // values do not flow backwards to tasks already in flight.
        Task<bool> taskStartedBeforeScope = Task.Run(static async () =>
        {
            await Task.Yield();
            try
            {
                _ = PartialConstructorContext.Current;
                return false; // should not reach here
            }
            catch (InvalidOperationException)
            {
                return true;
            }
        });

        IGenerationContext ctx = new StubGenerationContext();
        using IDisposable scope = PartialConstructorContext.Use(ctx);

        bool threwOutsideScope = await taskStartedBeforeScope;
        Assert.True(threwOutsideScope);
    }

    [Fact]
    public async Task Current_TaskRunStartedInsideScope_SeesAmbientContext()
    {
        // With AsyncLocal, child tasks inherit the parent's value. A Task.Run
        // launched *inside* the scope should therefore see the same context.
        // This test distinguishes AsyncLocal from [ThreadStatic] behaviour.
        IGenerationContext ctx = new StubGenerationContext();

        using IDisposable scope = PartialConstructorContext.Use(ctx);

        IGenerationContext contextSeenInsideTask = await Task.Run(() =>
            // Capture inside the task — no static capture so lambda is not static.
            PartialConstructorContext.Current);

        Assert.Equal(ctx, contextSeenInsideTask);
    }

    private sealed class StubGenerationContext : IGenerationContext
    {
        public T Generate<T>(Strategy<T> strategy) => throw new NotSupportedException();
        public void Assume(bool condition) { }
        public void Target(double observation, string label = "default") { }
    }
}