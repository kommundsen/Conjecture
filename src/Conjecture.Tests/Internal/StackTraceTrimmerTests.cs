// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core.Internal;

namespace Conjecture.Tests.Internal;

public class StackTraceTrimmerTests
{
    private static string Frame(string qualifiedMethod) => $"   at {qualifiedMethod}";

    private static string Trace(params string[] frames) =>
        string.Join(Environment.NewLine, frames.Select(Frame));

    [Fact]
    public void Trim_RemovesConjectureCoreInternalFrames()
    {
        string trace = Trace(
            "Conjecture.Core.Internal.TestRunner.Run(ConjectureData data)",
            "MyTests.MyClass.MyTest()");

        string result = StackTraceTrimmer.Trim(trace);

        Assert.DoesNotContain("Conjecture.Core.Internal", result);
        Assert.Contains("MyTests.MyClass.MyTest", result);
    }

    [Fact]
    public void Trim_RemovesConjectureXunitInternalFrames()
    {
        string trace = Trace(
            "Conjecture.Xunit.Internal.PropertyTestCaseRunner.RunTestAsync()",
            "MyTests.MyClass.MyTest()");

        string result = StackTraceTrimmer.Trim(trace);

        Assert.DoesNotContain("Conjecture.Xunit.Internal", result);
        Assert.Contains("MyTests.MyClass.MyTest", result);
    }

    [Fact]
    public void Trim_RemovesSystemRuntimeMethodHandleFrames()
    {
        string trace = Trace(
            "System.RuntimeMethodHandle.InvokeMethod(Object target, Object[] arguments)",
            "MyTests.MyClass.MyTest()");

        string result = StackTraceTrimmer.Trim(trace);

        Assert.DoesNotContain("System.RuntimeMethodHandle", result);
        Assert.Contains("MyTests.MyClass.MyTest", result);
    }

    [Fact]
    public void Trim_RemovesSystemReflectionInternalFrames()
    {
        string trace = Trace(
            "System.Reflection.MethodBase.Invoke(Object obj, Object[] parameters)",
            "System.Reflection.MethodInvoker.Invoke(Object obj, Span`1 args)",
            "MyTests.MyClass.MyTest()");

        string result = StackTraceTrimmer.Trim(trace);

        Assert.DoesNotContain("System.Reflection.MethodBase", result);
        Assert.DoesNotContain("System.Reflection.MethodInvoker", result);
        Assert.Contains("MyTests.MyClass.MyTest", result);
    }

    [Fact]
    public void Trim_PreservesUserTestMethodFrames()
    {
        string trace = Trace(
            "Conjecture.Core.Internal.TestRunner.Run(ConjectureData data)",
            "UserNamespace.MyTests.MyTest_WhenX_ExpectsY(Int32 x)");

        string result = StackTraceTrimmer.Trim(trace);

        Assert.Contains("UserNamespace.MyTests.MyTest_WhenX_ExpectsY", result);
    }

    [Fact]
    public void Trim_PreservesXunitRunnerFrames()
    {
        string trace = Trace(
            "Conjecture.Core.Internal.TestRunner.Run(ConjectureData data)",
            "Xunit.Sdk.TestRunner.RunAsync()",
            "Xunit.Sdk.XunitTestCase.RunAsync()");

        string result = StackTraceTrimmer.Trim(trace);

        Assert.Contains("Xunit.Sdk.TestRunner", result);
        Assert.Contains("Xunit.Sdk.XunitTestCase", result);
    }

    [Fact]
    public void Trim_ReturnsEmptyString_ForNullInput()
    {
        string result = StackTraceTrimmer.Trim(null);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void Trim_ReturnsEmptyString_ForEmptyInput()
    {
        string result = StackTraceTrimmer.Trim(string.Empty);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void Trim_AllFramesFiltered_ReturnsEmptyString()
    {
        string trace = Trace(
            "Conjecture.Core.Internal.TestRunner.Run(ConjectureData data)",
            "Conjecture.Xunit.Internal.PropertyTestCaseRunner.RunTestAsync()",
            "System.RuntimeMethodHandle.InvokeMethod(Object target, Object[] args)");

        string result = StackTraceTrimmer.Trim(trace);

        Assert.Equal(string.Empty, result.Trim());
    }
}