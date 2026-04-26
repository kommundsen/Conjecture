// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

namespace Conjecture.Aspire;

/// <summary>Represents a single interaction with an Aspire-hosted resource.</summary>
public readonly record struct Interaction(
    string ResourceName,
    string Method,
    string Path,
    object? Body);