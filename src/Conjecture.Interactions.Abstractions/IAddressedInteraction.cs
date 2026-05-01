// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.ComponentModel;

namespace Conjecture.Abstractions.Interactions;

/// <summary>An <see cref="IInteraction"/> that carries a resource name used for routing.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IAddressedInteraction : IInteraction
{
    /// <summary>The name of the resource this interaction is addressed to.</summary>
    string ResourceName { get; }
}
