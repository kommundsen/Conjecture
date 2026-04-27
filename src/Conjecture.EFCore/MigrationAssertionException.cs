// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;

namespace Conjecture.EFCore;

/// <summary>Thrown when a migration idempotency assertion fails.</summary>
public sealed class MigrationAssertionException : Exception
{
    /// <summary>Initializes a new instance with <paramref name="message"/>.</summary>
    public MigrationAssertionException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance with <paramref name="message"/> and <paramref name="innerException"/>.</summary>
    public MigrationAssertionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}