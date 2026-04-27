// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

namespace Conjecture.EFCore;

/// <summary>Thrown when a roundtrip assertion fails.</summary>
public sealed class RoundtripAssertionException(string message) : DbInvariantException(message)
{
}