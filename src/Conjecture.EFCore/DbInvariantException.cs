// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;

namespace Conjecture.EFCore;

/// <summary>
/// Base type for all Conjecture.EFCore invariant assertions. Catch this to handle any DB-shape
/// invariant failure (Roundtrip, Migration, or future composite invariants) uniformly.
/// </summary>
public class DbInvariantException : Exception
{
    /// <summary>Initializes a new instance with the specified message.</summary>
    public DbInvariantException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance with the specified message and inner exception.</summary>
    public DbInvariantException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}