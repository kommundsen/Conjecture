// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core;
using Conjecture.NUnit.Internal;

using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;
using NUnit.Framework.Internal.Builders;
using NUnit.Framework.Internal.Commands;

using Conjecture.Abstractions.Testing;

namespace Conjecture.NUnit;

/// <summary>Marks a method as a Conjecture property-based test (NUnit).</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class PropertyAttribute : global::NUnit.Framework.NUnitAttribute, ITestBuilder, IWrapTestMethod, IPropertyTest
{
    /// <summary>Maximum number of examples to generate. Defaults to 100.</summary>
    public int MaxExamples { get; set; } = 100;

    /// <summary>Optional fixed seed for deterministic runs. 0 means use a random seed.</summary>
    public ulong Seed { get; set; }

    /// <summary>Whether to use the example database. Defaults to <see langword="true"/>.</summary>
    public bool Database { get; set; } = true;

    /// <summary>Maximum number of times a strategy may reject a value. Defaults to 5.</summary>
    public int MaxStrategyRejections { get; set; } = 5;

    /// <summary>Deadline for each test run in milliseconds. 0 means no deadline.</summary>
    public int DeadlineMs { get; set; }

    /// <summary>Whether to run a targeting phase after generation. Defaults to <see langword="true"/>.</summary>
    public bool Targeting { get; set; } = true;

    /// <summary>Fraction of MaxExamples budget allocated to the targeting phase. Defaults to 0.5.</summary>
    public double TargetingProportion { get; set; } = 0.5;

    /// <inheritdoc/>
    IEnumerable<TestMethod> ITestBuilder.BuildFrom(IMethodInfo method, Test? suite)
    {
        NUnitTestCaseBuilder builder = new();
        // Supply default args so NUnitTestCaseBuilder doesn't mark the test NotRunnable.
        // PropertyTestCommand.Execute ignores these and generates values via strategy resolver.
        IParameterInfo[] paramInfos = method.GetParameters();
        object?[] dummyArgs = new object?[paramInfos.Length];
        for (int i = 0; i < paramInfos.Length; i++)
        {
            Type paramType = paramInfos[i].ParameterInfo.ParameterType;
            dummyArgs[i] = paramType.IsValueType ? Activator.CreateInstance(paramType) : null;
        }
        yield return builder.BuildTestMethod(method, suite, new TestCaseParameters(dummyArgs));
    }

    /// <inheritdoc/>
    TestCommand ICommandWrapper.Wrap(TestCommand command) => new PropertyTestCommand(command, this);
}