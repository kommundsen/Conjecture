// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Collections.Generic;

using Conjecture.Interactions;

using Conjecture.Abstractions.Interactions;

namespace Conjecture.Messaging;

/// <summary>Immutable record representing a single message interaction with a message bus.</summary>
public sealed record MessageInteraction(
    string Destination,
    ReadOnlyMemory<byte> Body,
    IReadOnlyDictionary<string, string> Headers,
    string MessageId,
    string? CorrelationId = null) : IInteraction;