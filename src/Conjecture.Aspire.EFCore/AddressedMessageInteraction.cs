// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Interactions;
using Conjecture.Messaging;

namespace Conjecture.Aspire.EFCore;

/// <summary>Wraps a <see cref="MessageInteraction"/> as an <see cref="IAddressedInteraction"/> with a given resource name.</summary>
internal sealed class AddressedMessageInteraction(string resourceName, MessageInteraction inner)
    : IAddressedInteraction
{
    /// <inheritdoc/>
    public string ResourceName { get; } = resourceName;

    /// <summary>The underlying message interaction.</summary>
    public MessageInteraction Inner { get; } = inner;
}
