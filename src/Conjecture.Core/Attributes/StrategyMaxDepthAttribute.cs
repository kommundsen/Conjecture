// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;

namespace Conjecture.Core;

/// <summary>Caps the recursive generation depth for a self-referential or mutually recursive type to <paramref name="maxDepth"/>.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class StrategyMaxDepthAttribute(int maxDepth) : Attribute
{
    /// <summary>Gets the maximum generation depth.</summary>
    public int MaxDepth { get; } = maxDepth;
}