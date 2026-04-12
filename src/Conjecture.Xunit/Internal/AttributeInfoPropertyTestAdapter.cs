// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core;

using Xunit.Abstractions;

namespace Conjecture.Xunit.Internal;

/// <summary>
/// Adapts xUnit v2's <see cref="IAttributeInfo"/> to <see cref="IPropertyTest"/> and
/// <see cref="IReproductionExport"/>, allowing attribute properties to be read through
/// the shared interfaces without coupling to a concrete <c>PropertyAttribute</c> type.
/// </summary>
internal sealed class AttributeInfoPropertyTestAdapter(IAttributeInfo info) : IPropertyTest, IReproductionExport
{
    public int MaxExamples => info.GetNamedArgument<int>("MaxExamples");
    public ulong Seed => info.GetNamedArgument<ulong>("Seed");
    public bool UseDatabase => info.GetNamedArgument<bool>("UseDatabase");
    public int MaxStrategyRejections => info.GetNamedArgument<int>("MaxStrategyRejections");
    public int DeadlineMs => info.GetNamedArgument<int>("DeadlineMs");
    public bool Targeting => info.GetNamedArgument<bool>("Targeting");
    public double TargetingProportion => info.GetNamedArgument<double>("TargetingProportion");
    public bool ExportReproOnFailure => info.GetNamedArgument<bool>("ExportReproOnFailure");
    public string ReproOutputPath => info.GetNamedArgument<string>("ReproOutputPath") ?? ".conjecture/repros/";
}