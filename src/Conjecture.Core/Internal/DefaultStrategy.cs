// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Diagnostics.CodeAnalysis;

namespace Conjecture.Core.Internal;

internal sealed class DefaultStrategy<T> : Strategy<T>
{
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026",
        Justification = "DefaultStrategy is only reachable through RequiresUnreferencedCode-annotated paths.")]
    [UnconditionalSuppressMessage(
        "AOT",
        "IL3050",
        Justification = "DefaultStrategy is only reachable through RequiresDynamicCode-annotated paths.")]
    internal override T Generate(ConjectureData data) =>
        (T)SharedParameterStrategyResolver.GenerateValueForDefault(typeof(T), data);
}